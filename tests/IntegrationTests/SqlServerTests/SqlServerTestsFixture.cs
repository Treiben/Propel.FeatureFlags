using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.SqlServer.Extensions;
using System.Data;
using System.Text.Json;
using Testcontainers.MsSql;

namespace FeatureFlags.IntegrationTests.SqlServerTests;

public class SqlServerTestsFixture : IAsyncLifetime
{
	private readonly MsSqlContainer _container;
	public IServiceProvider Services { get; private set; } = null!;
	public IFeatureFlagRepository FeatureFlagRepository => Services.GetRequiredService<IFeatureFlagRepository>();
	public IDashboardRepository DashboardRepository => Services.GetRequiredService<IDashboardRepository>();

	public SqlServerTestsFixture()
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
		var connectionString = await StartContainer();

		var services = new ServiceCollection();

		services.AddLogging();

		services.AddFeatureFlagPersistence(connectionString);
		services.AddDashboardPersistence(new PropelOptions
		{
			Database = new DatabaseOptions
			{
				Provider = DatabaseProvider.SqlServer,
				ConnectionString = connectionString
			}
		});

		Services = services.BuildServiceProvider();

		await Services.EnsureFeatureFlagDatabase();
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

	public async Task SaveFlagAsync(FlagEvaluationConfiguration flag,
		string name, string description)
	{
		await SqlServerDbHelpers.CreateFlagAsync(_container, flag, name, description);
	}

	private async Task<string> StartContainer()
	{
		await _container.StartAsync();

		var connectionString = _container.GetConnectionString();
		return connectionString;
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
			command.Parameters.AddWithValue("ScheduledEnableDate", (DateTimeOffset)flag.Schedule.EnableOn);

		if (flag.Schedule.DisableOn == DateTime.MaxValue.ToUniversalTime())
			command.Parameters.AddWithValue("ScheduledDisableDate", DBNull.Value);
		else
			command.Parameters.AddWithValue("ScheduledDisableDate", (DateTimeOffset)flag.Schedule.DisableOn);

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
