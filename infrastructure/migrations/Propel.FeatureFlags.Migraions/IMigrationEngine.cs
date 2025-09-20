using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Propel.FeatureFlags.Migrations;

public interface IMigrationEngine
{
	Task<MigrationResult> MigrateAsync(CancellationToken cancellationToken = default);
	Task<MigrationResult> RollbackAsync(int steps = 1, CancellationToken cancellationToken = default);
	Task ShowStatusAsync(CancellationToken cancellationToken = default);
	Task<MigrationResult> ValidateAsync(CancellationToken cancellationToken = default);
	Task<MigrationResult> BaselineAsync(CancellationToken cancellationToken = default);
}

public class MigrationEngine(
	ILogger<IMigrationEngine> logger,
	IConfiguration configuration,
	IMigrationRepository repository) : IMigrationEngine
{
	private readonly string _scriptsPath = Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "Migrations");

	public async Task<MigrationResult> MigrateAsync(CancellationToken cancellationToken = default)
	{
		var stopwatch = Stopwatch.StartNew();
		var result = new MigrationResult();

		try
		{
			logger.LogInformation("Starting database migration...");

			// Ensure database and migration table exist
			await EnsureDatabaseSetupAsync(cancellationToken);

			// Load all migration files
			var allMigrations = LoadMigrationFiles();
			var appliedMigrations = await repository.GetAppliedMigrationsAsync(cancellationToken);

			var pendingMigrations = allMigrations
				.Where(m => !appliedMigrations.Contains(m.Version))
				.OrderBy(m => m.GetSortableVersion())
				.ToList();

			if (!pendingMigrations.Any())
			{
				logger.LogInformation("No pending migrations found");
				result.Success = true;
				result.Message = "Database is up to date";
				return result;
			}

			logger.LogInformation("Found {Count} pending migrations", pendingMigrations.Count);

			foreach (var migration in pendingMigrations)
			{
				logger.LogInformation("Applying migration {Version}: {Description}",
					migration.Version, migration.Description);

				var success = await repository.ExecuteSqlAsync(migration.SqlScript, cancellationToken);
				if (!success)
				{
					result.Errors.Add($"Failed to apply migration {migration.Version}");
					throw new InvalidOperationException($"Migration {migration.Version} failed");
				}

				await repository.RecordMigrationAsync(migration.Version, migration.Description, cancellationToken);
				result.MigrationsApplied++;

				logger.LogInformation("Successfully applied migration {Version}", migration.Version);
			}

			result.Success = true;
			result.Message = $"Successfully applied {result.MigrationsApplied} migrations";
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Migration failed: {Message}", ex.Message);
			result.Success = false;
			result.Message = ex.Message;
			result.Errors.Add(ex.Message);
		}
		finally
		{
			stopwatch.Stop();
			result.Duration = stopwatch.Elapsed;
			logger.LogInformation("Migration completed in {Duration}ms", stopwatch.ElapsedMilliseconds);
		}

		return result;
	}

	public async Task<MigrationResult> RollbackAsync(int steps = 1, CancellationToken cancellationToken = default)
	{
		var stopwatch = Stopwatch.StartNew();
		var result = new MigrationResult();

		try
		{
			logger.LogInformation("Starting rollback of {Steps} migration(s)...", steps);

			var appliedMigrations = await repository.GetAppliedMigrationsAsync(cancellationToken);
			var allMigrations = LoadMigrationFiles();

			var migrationsToRollback = appliedMigrations
				.OrderByDescending(v => GetSortableVersion(v))
				.Take(steps)
				.ToList();

			if (!migrationsToRollback.Any())
			{
				result.Message = "No migrations to rollback";
				result.Success = true;
				return result;
			}

			foreach (var version in migrationsToRollback)
			{
				var migration = allMigrations.FirstOrDefault(m => m.Version == version);
				if (migration?.RollbackScript == null)
				{
					throw new InvalidOperationException($"No rollback script found for migration {version}");
				}

				logger.LogInformation("Rolling back migration {Version}", version);

				var success = await repository.ExecuteSqlAsync(migration.RollbackScript, cancellationToken);
				if (!success)
				{
					result.Errors.Add($"Failed to rollback migration {version}");
					throw new InvalidOperationException($"Rollback of migration {version} failed");
				}

				await repository.RemoveMigrationAsync(version, cancellationToken);
				result.MigrationsApplied++;

				logger.LogInformation("Successfully rolled back migration {Version}", version);
			}

			result.Success = true;
			result.Message = $"Successfully rolled back {result.MigrationsApplied} migrations";
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Rollback failed: {Message}", ex.Message);
			result.Success = false;
			result.Message = ex.Message;
			result.Errors.Add(ex.Message);
		}
		finally
		{
			stopwatch.Stop();
			result.Duration = stopwatch.Elapsed;
		}

		return result;
	}

	public async Task ShowStatusAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			var databaseExists = await repository.DatabaseExistsAsync(cancellationToken);
			if (!databaseExists)
			{
				logger.LogWarning("Database does not exist");
				return;
			}

			var allMigrations = LoadMigrationFiles();
			var appliedMigrations = await repository.GetAppliedMigrationsAsync(cancellationToken);

			Console.WriteLine("\n=== Migration Status ===");
			Console.WriteLine($"Database: {configuration.GetConnectionString("DefaultConnection")}");
			Console.WriteLine($"Total migrations: {allMigrations.Count}");
			Console.WriteLine($"Applied migrations: {appliedMigrations.Count}");
			Console.WriteLine($"Pending migrations: {allMigrations.Count - appliedMigrations.Count}");
			Console.WriteLine();

			Console.WriteLine("Migration History:");
			Console.WriteLine("Version".PadRight(15) + "Status".PadRight(10) + "Description");
			Console.WriteLine(new string('-', 60));

			foreach (var migration in allMigrations.OrderBy(m => m.GetSortableVersion()))
			{
				var status = appliedMigrations.Contains(migration.Version) ? "Applied" : "Pending";
				Console.WriteLine($"{migration.Version.PadRight(15)}{status.PadRight(10)}{migration.Description}");
			}
			Console.WriteLine();
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to show status: {Message}", ex.Message);
		}
	}

	public async Task<MigrationResult> ValidateAsync(CancellationToken cancellationToken = default)
	{
		var result = new MigrationResult();

		try
		{
			var migrations = LoadMigrationFiles();
			var errors = new List<string>();

			// Check for duplicate versions
			var duplicateVersions = migrations.GroupBy(m => m.Version)
				.Where(g => g.Count() > 1)
				.Select(g => g.Key);

			foreach (var version in duplicateVersions)
			{
				errors.Add($"Duplicate migration version: {version}");
			}

			// Check version naming convention
			var versionPattern = new Regex(@"^V\d+_\d+_\d+__\w+$");
			foreach (var migration in migrations)
			{
				if (!versionPattern.IsMatch(Path.GetFileNameWithoutExtension(migration.FileName)))
				{
					errors.Add($"Invalid migration filename format: {migration.FileName}");
				}
			}

			// Check for missing rollback scripts
			foreach (var migration in migrations)
			{
				if (string.IsNullOrEmpty(migration.RollbackScript))
				{
					logger.LogWarning("Migration {Version} has no rollback script", migration.Version);
				}
			}

			result.Success = !errors.Any();
			result.Errors = errors;
			result.Message = result.Success ? "All migrations are valid" : $"Found {errors.Count} validation errors";

			if (errors.Any())
			{
				foreach (var error in errors)
				{
					logger.LogError("Validation error: {Error}", error);
				}
			}
		}
		catch (Exception ex)
		{
			result.Success = false;
			result.Message = ex.Message;
			result.Errors.Add(ex.Message);
		}

		return result;
	}

	public async Task<MigrationResult> BaselineAsync(CancellationToken cancellationToken = default)
	{
		var result = new MigrationResult();

		try
		{
			await EnsureDatabaseSetupAsync(cancellationToken);

			var migrations = LoadMigrationFiles();
			foreach (var migration in migrations)
			{
				await repository.RecordMigrationAsync(migration.Version,
					$"BASELINE: {migration.Description}", cancellationToken);
			}

			result.Success = true;
			result.Message = $"Baseline completed. Marked {migrations.Count} migrations as applied.";
			result.MigrationsApplied = migrations.Count;

			logger.LogInformation("Database baseline completed successfully");
		}
		catch (Exception ex)
		{
			result.Success = false;
			result.Message = ex.Message;
			result.Errors.Add(ex.Message);
			logger.LogError(ex, "Baseline failed: {Message}", ex.Message);
		}

		return result;
	}

	private async Task EnsureDatabaseSetupAsync(CancellationToken cancellationToken)
	{
		var createDatabaseIfNotExists = configuration.GetValue("Migration:CreateDatabaseIfNotExists", true);

		if (createDatabaseIfNotExists)
		{
			var databaseExists = await repository.DatabaseExistsAsync(cancellationToken);
			if (!databaseExists)
			{
				logger.LogInformation("Creating database...");
				await repository.CreateDatabaseAsync(cancellationToken);
			}
		}

		var migrationTableExists = await repository.MigrationTableExistsAsync(cancellationToken);
		if (!migrationTableExists)
		{
			logger.LogInformation("Creating migration tracking table...");
			await repository.CreateMigrationTableAsync(cancellationToken);
		}
	}

	private List<Migration> LoadMigrationFiles()
	{
		var migrations = new List<Migration>();

		if (!Directory.Exists(_scriptsPath))
		{
			logger.LogWarning("Migration scripts directory not found: {Path}", _scriptsPath);
			return migrations;
		}

		var sqlFiles = Directory.GetFiles(_scriptsPath, "V*.sql", SearchOption.TopDirectoryOnly)
			.OrderBy(f => f)
			.ToList();

		foreach (var file in sqlFiles)
		{
			try
			{
				var migration = ParseMigrationFile(file);
				migrations.Add(migration);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to parse migration file: {File}", file);
				throw;
			}
		}

		return migrations;
	}

	private Migration ParseMigrationFile(string filePath)
	{
		var fileName = Path.GetFileName(filePath);
		var content = File.ReadAllText(filePath);

		// Parse version and description from filename: V1_0_0__initial_schema.sql
		var match = Regex.Match(fileName, @"^(V\d+_\d+_\d+)__(.+)\.sql$");
		if (!match.Success)
		{
			throw new InvalidOperationException($"Invalid migration filename format: {fileName}");
		}

		var version = match.Groups[1].Value;
		var description = match.Groups[2].Value.Replace('_', ' ');

		// Look for rollback script
		var rollbackPath = Path.Combine(Path.GetDirectoryName(filePath)!, "Rollback", fileName);
		string? rollbackScript = null;
		if (File.Exists(rollbackPath))
		{
			rollbackScript = File.ReadAllText(rollbackPath);
		}

		return new Migration
		{
			Version = version,
			Description = description,
			FileName = fileName,
			SqlScript = content,
			RollbackScript = rollbackScript
		};
	}

	private static string GetSortableVersion(string version)
	{
		var parts = version.TrimStart('V', 'v').Split('_');
		return string.Join(".", parts.Select(p => p.PadLeft(3, '0')));
	}
}
