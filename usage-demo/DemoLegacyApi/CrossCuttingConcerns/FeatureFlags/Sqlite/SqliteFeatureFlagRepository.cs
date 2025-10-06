using DemoLegacyApi.CrossCuttingConcerns.FeatureFlags.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DemoLegacyApi.CrossCuttingConcerns.FeatureFlags.Sqlite
{
	public sealed class SqliteFeatureFlagRepository : IFeatureFlagRepository
	{
		private readonly FeatureFlagsDbContext _context;
		private readonly ILogger<SqliteFeatureFlagRepository> _logger;

		public SqliteFeatureFlagRepository(FeatureFlagsDbContext context, ILogger<SqliteFeatureFlagRepository> logger)
		{
			_context = context ?? throw new ArgumentNullException(nameof(context));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_logger.LogDebug("SQLite Feature Flag Repository initialized with Entity Framework Core");
		}

		public async Task<EvaluationOptions?> GetEvaluationOptionsAsync(FlagIdentifier identifier, CancellationToken cancellationToken = default)
		{
			_logger.LogDebug("Getting feature flag with key: {Key}, Scope: {Scope}, Application: {Application}",
				identifier.Key, identifier.Scope, identifier.ApplicationName);

			try
			{
				var query = _context.FeatureFlags.AsQueryable();

				// Build query based on identifier
				query = query.Where(f => f.Key == identifier.Key && f.Scope == (int)identifier.Scope);

				if (identifier.Scope != Scope.Global)
				{
					query = query.Where(f => f.ApplicationName == identifier.ApplicationName);

					if (!string.IsNullOrWhiteSpace(identifier.ApplicationVersion))
					{
						query = query.Where(f => f.ApplicationVersion == identifier.ApplicationVersion);
					}
					else
					{
						query = query.Where(f => f.ApplicationVersion == null);
					}
				}

				var entity = await query.FirstOrDefaultAsync(cancellationToken);

				if (entity == null)
				{
					_logger.LogDebug("Feature flag with key {Key} not found within application {Application} scope",
						identifier.Key, identifier.ApplicationName);
					return null;
				}

				var flag = entity.ToEvaluationOptions(identifier);
				_logger.LogDebug("Retrieved feature flag: {Key} with evaluation modes {Modes}",
					flag.Key, string.Join(",", flag.ModeSet.Modes));
				return flag;
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				_logger.LogError(ex, "Error retrieving feature flag with key {Key} for {Application}",
					identifier.Key, identifier.ApplicationName);
				throw;
			}
		}

		public async Task CreateApplicationFlagAsync(FlagIdentifier identifier, EvaluationMode activeMode, string name, string description, CancellationToken cancellationToken = default)
		{

			if (identifier.Scope == Scope.Global)
			{
				throw new InvalidOperationException("Only application-level flags are allowed to be created from client applications. Global flags are outside of application domain and must be created by management tools.");
			}

			_logger.LogDebug("Creating feature flag with key: {Key} for application: {Application}",
				identifier.Key, identifier.ApplicationName);

			var applicationName = identifier.ApplicationName;
			if (string.IsNullOrEmpty(identifier.ApplicationName))
			{
				applicationName = ApplicationInfo.Name;
			}

			var applicationVersion = identifier.ApplicationVersion;
			if (string.IsNullOrEmpty(identifier.ApplicationVersion))
			{
				applicationVersion = ApplicationInfo.Version ?? "1.0.0.0";
			}

			try
			{
				// Check if flag already exists
				var existingFlag = await _context.FeatureFlags
					.FirstOrDefaultAsync(f =>
						f.Key == identifier.Key &&
						f.ApplicationName == applicationName &&
						f.ApplicationVersion == applicationVersion,
						cancellationToken);

				if (existingFlag != null)
				{
					_logger.LogWarning("Feature flag with key '{Key}' already exists in scope '{Scope}' for application '{ApplicationName}'. Nothing to add there.",
						identifier.Key, identifier.Scope, identifier.ApplicationName);
					return;
				}

				// Create new flag entity
				var flagEntity = new FeatureFlagEntity
				{
					Key = identifier.Key,
					ApplicationName = applicationName,
					ApplicationVersion = applicationVersion,
					Scope = (int)Scope.Application,
					Name = name,
					Description = description,
					EvaluationModes = JsonSerializer.Serialize(new List<int> { (int)activeMode }, JsonDefaults.JsonOptions)
				};

				_context.FeatureFlags.Add(flagEntity);

				// Create metadata record
				var metadataEntity = new FeatureFlagMetadataEntity
				{
					Id = Guid.NewGuid(),
					FlagKey = identifier.Key,
					ApplicationName = applicationName,
					ApplicationVersion = applicationVersion,
					ExpirationDate = DateTimeOffset.UtcNow.AddDays(30),
					IsPermanent = false
				};

				_context.FeatureFlagsMetadata.Add(metadataEntity);

				// Create audit record
				var auditEntity = new FeatureFlagAuditEntity
				{
					Id = Guid.NewGuid(),
					FlagKey = identifier.Key,
					ApplicationName = applicationName,
					ApplicationVersion = applicationVersion,
					Action = "flag-created",
					Actor = "Application",
					Timestamp = DateTimeOffset.UtcNow,
					Notes = "Auto-registered by the application"
				};

				_context.FeatureFlagsAudit.Add(auditEntity);

				await _context.SaveChangesAsync(cancellationToken);

				_logger.LogDebug("Successfully created feature flag: {Key}", identifier.Key);
			}
			catch (Exception ex) when (ex is not OperationCanceledException && ex is not ApplicationFlagException)
			{
				_logger.LogError(ex, "Error creating feature flag with key {Key} {Scope} {Application} {Version}",
					identifier.Key, identifier.Scope, identifier.ApplicationName, identifier.ApplicationVersion);

				throw new ApplicationFlagException("Error creating feature flag", ex,
					identifier.Key, identifier.Scope, identifier.ApplicationName, identifier.ApplicationVersion);
			}
		}
	}
}