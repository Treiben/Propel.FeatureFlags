using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using Propel.FeatureFlags.Infrastructure.SqlServer;
using Propel.FeatureFlags.Migrations.SqlServer;
using System.Data;
using System.Text.Json;
using Testcontainers.MsSql;

namespace FeatureFlags.IntegrationTests.SqlServerTests;

public class SqlServerRepositoriesTestsFixture : IAsyncLifetime
{
	private readonly MsSqlContainer _container;
	public ClientApplicationRepository EvaluationRepository { get; private set; } = null!;
	public SqlServerMigrationRepository MigrationRepository { get; private set; }

	public SqlServerRepositoriesTestsFixture()
	{
		_container = new MsSqlBuilder()
			.WithImage("mcr.microsoft.com/mssql/server:2022-latest")
			.WithPassword("StrongP@ssw0rd!")
			.WithEnvironment("ACCEPT_EULA", "Y")
			.WithEnvironment("SA_PASSWORD", "StrongP@ssw0rd!")
			.WithPortBinding(1433, true)
			.Build();
	}

	public async Task InitializeAsync()
	{
		await _container.StartAsync();

		var connectionString = _container.GetConnectionString();

		var dbInitializer = new SqlServerDatabaseInitializer(connectionString,
			new Mock<ILogger<SqlServerDatabaseInitializer>>().Object);

		var initialized = await dbInitializer.InitializeAsync();
		if (!initialized)
			throw new InvalidOperationException("Failed to initialize sql server database for feature flags");

		EvaluationRepository = new ClientApplicationRepository(connectionString, 
			new Mock<ILogger<ClientApplicationRepository>>().Object);

		var migrationOptions = new SqlMigrationOptions
		{
			Connection = connectionString,
		};
		MigrationRepository = new SqlServerMigrationRepository(migrationOptions, new Mock<ILogger<SqlServerMigrationRepository>>().Object);
	}

	public async Task DisposeAsync()
	{
		await _container.DisposeAsync();
	}

	public async Task ClearAllData()
	{
		var connectionString = _container.GetConnectionString();
		using var connection = new SqlConnection(connectionString);
		await connection.OpenAsync();
		using var command = new SqlCommand("DELETE FROM FeatureFlags", connection);
		await command.ExecuteNonQueryAsync();
	}

	public async Task SaveAsync(FlagEvaluationConfiguration flag,
		string name, string description)
	{
		await SqlServerDbHelpers.CreateFlagAsync(_container, flag, name, description);
	}
}

public static class SqlServerDbHelpers
{
	public async static Task CreateFlagAsync(MsSqlContainer container, FlagEvaluationConfiguration flag,
		string name, string description)
	{
		var connectionString = container.GetConnectionString();
		using var connection = new SqlConnection(connectionString);
		using var command = new SqlCommand(@"INSERT INTO FeatureFlags (
                [Key], ApplicationName, ApplicationVersion, Scope,
				Name, Description, EvaluationModes, 
                ScheduledEnableDate, ScheduledDisableDate,
                WindowStartTime, WindowEndTime, TimeZone, WindowDays,
				TargetingRules, Variations, DefaultVariation,
                UserPercentageEnabled, EnabledUsers, DisabledUsers,
                TenantPercentageEnabled, EnabledTenants, DisabledTenants
            ) VALUES (
                @Key, @ApplicationName, @ApplicationVersion, @Scope,
				@Name, @Description, @EvaluationModes,
                @ScheduledEnableDate, @ScheduledDisableDate,
                @WindowStartTime, @WindowEndTime, @TimeZone, @WindowDays,
				@TargetingRules, @Variations, @DefaultVariation,
                @UserPercentageEnabled, @EnabledUsers, @DisabledUsers,
                @TenantPercentageEnabled, @EnabledTenants, @DisabledTenants            
            );", connection);

		command.AddFlagFieldsAsync(flag, name, description);

		await connection.OpenAsync();
		await command.ExecuteNonQueryAsync();
	}

	private static void AddFlagFieldsAsync(this SqlCommand command, FlagEvaluationConfiguration flag,
		string name, string description)
	{
		// Flag identity parameters
		command.Parameters.AddWithValue("Key", flag.Identifier.Key);
		command.Parameters.AddWithValue("ApplicationName", (object?)flag.Identifier.ApplicationName ?? DBNull.Value);
		command.Parameters.AddWithValue("ApplicationVersion", (object?)flag.Identifier.ApplicationVersion ?? DBNull.Value);
		command.Parameters.AddWithValue("Scope", (int)flag.Identifier.Scope);

		command.Parameters.AddWithValue("Name", name);
		command.Parameters.AddWithValue("Description", description);

		command.Parameters.AddWithValue("EvaluationModes",
			JsonSerializer.Serialize(flag.ActiveEvaluationModes.Modes.Select(m => (int)m), JsonDefaults.JsonOptions));

		// Schedule parameters - handle domain model defaults
		if (flag.Schedule.EnableOn == DateTime.MinValue.ToUniversalTime())
			command.Parameters.AddWithValue("ScheduledEnableDate", DBNull.Value);
		else
			command.Parameters.AddWithValue("ScheduledEnableDate", flag.Schedule.EnableOn);

		if (flag.Schedule.DisableOn == DateTime.MaxValue.ToUniversalTime())
			command.Parameters.AddWithValue("ScheduledDisableDate", DBNull.Value);
		else
			command.Parameters.AddWithValue("ScheduledDisableDate", flag.Schedule.DisableOn);

		// Operational window parameters
		command.Parameters.AddWithValue("WindowStartTime", flag.OperationalWindow.StartOn);
		command.Parameters.AddWithValue("WindowEndTime", flag.OperationalWindow.StopOn);
		command.Parameters.AddWithValue("TimeZone", flag.OperationalWindow.TimeZone);
		command.Parameters.AddWithValue("WindowDays", 
			JsonSerializer.Serialize(flag.OperationalWindow.DaysActive.Select(d => (int)d), JsonDefaults.JsonOptions));


		// Variations
		command.Parameters.AddWithValue("DefaultVariation", flag.Variations.DefaultVariation);
		command.Parameters.AddWithValue("Variations", 
			JsonSerializer.Serialize(flag.Variations.Values, JsonDefaults.JsonOptions));

		// Targeting rules
		command.Parameters.AddWithValue("TargetingRules", 
			JsonSerializer.Serialize(flag.TargetingRules, JsonDefaults.JsonOptions));

		// Access control parameters
		command.Parameters.AddWithValue("UserPercentageEnabled", flag.UserAccessControl.RolloutPercentage);
		command.Parameters.AddWithValue("EnabledUsers", 
			JsonSerializer.Serialize(flag.UserAccessControl.Allowed, JsonDefaults.JsonOptions));
		command.Parameters.AddWithValue("DisabledUsers", 
			JsonSerializer.Serialize(flag.UserAccessControl.Blocked, JsonDefaults.JsonOptions));

		// Access control parameters
		command.Parameters.AddWithValue("TenantPercentageEnabled", flag.TenantAccessControl.RolloutPercentage);
		command.Parameters.AddWithValue("EnabledTenants", 
			JsonSerializer.Serialize(flag.TenantAccessControl.Allowed, JsonDefaults.JsonOptions));
		command.Parameters.AddWithValue("DisabledTenants", 
			JsonSerializer.Serialize(flag.TenantAccessControl.Blocked, JsonDefaults.JsonOptions));


	}
}
