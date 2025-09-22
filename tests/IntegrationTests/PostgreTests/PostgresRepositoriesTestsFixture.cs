using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Helpers;
using Propel.FeatureFlags.Infrastructure.PostgresSql;
using System.Text.Json;
using Testcontainers.PostgreSql;

namespace FeatureFlags.IntegrationTests.PostgreTests;

public class PostgresRepositoriesTestsFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _container;
	public ClientApplicationRepository EvaluationRepository { get; private set; } = null!;
	public PostgresRepositoriesTestsFixture()
	{
		_container = new PostgreSqlBuilder()
			.WithImage("postgres:15-alpine")
			.WithDatabase("feature_flags_test")
			.WithUsername("test_user")
			.WithPassword("test_password")
			.WithPortBinding(5432, true)
			.Build();
	}

	public async Task InitializeAsync()
	{
		await _container.StartAsync();

		var connectionString = _container.GetConnectionString();

		var dbInitializer = new PostgreSQLDatabaseInitializer(connectionString, 
			new Mock<ILogger<PostgreSQLDatabaseInitializer>>().Object);
		var initialized = await dbInitializer.InitializeAsync();
		if (!initialized)
			throw new InvalidOperationException("Failed to initialize PostgreSQL database for feature flags");

		EvaluationRepository = new ClientApplicationRepository(connectionString, new Mock<ILogger<ClientApplicationRepository>>().Object);
	}

	public async Task DisposeAsync()
	{
		await _container.DisposeAsync();
	}

	public async Task ClearAllData()
	{
		var connectionString = _container.GetConnectionString();
		using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		using var command = new NpgsqlCommand("DELETE FROM feature_flags", connection);
		await command.ExecuteNonQueryAsync();
	}

	public async Task SaveAsync(FlagEvaluationConfiguration flag,
		string name, string description)
	{
		await PosgreDbHelpers.CreateFlagAsync(_container, flag, name, description);
	}
}

public static class PosgreDbHelpers
{
	public async static Task CreateFlagAsync(PostgreSqlContainer container, FlagEvaluationConfiguration flag,
		string name, string description)
	{
		var connectionString = container.GetConnectionString();
		using var connection = new NpgsqlConnection(connectionString);
		using var command = new NpgsqlCommand(@"INSERT INTO feature_flags (
                key, application_name, application_version, scope,
				name, description, evaluation_modes, 
                scheduled_enable_date, scheduled_disable_date,
                window_start_time, window_end_time, time_zone, window_days,
				targeting_rules, variations, default_variation,
                user_percentage_enabled, enabled_users, disabled_users,
                tenant_percentage_enabled, enabled_tenants, disabled_tenants
            ) VALUES (
                @key, @application_name, @application_version, @scope,
				@name, @description, @evaluation_modes,
                @scheduled_enable_date, @scheduled_disable_date,
                @window_start_time, @window_end_time, @time_zone, @window_days,
				@targeting_rules, @variations, @default_variation,
                @user_percentage_enabled, @enabled_users, @disabled_users,
                @tenant_percentage_enabled, @enabled_tenants, @disabled_tenants            
            );", connection);

		command.AddFlagFieldsAsync(flag, name, description);

		await connection.OpenAsync();
		await command.ExecuteNonQueryAsync();
	}

	private static void AddFlagFieldsAsync(this NpgsqlCommand command, FlagEvaluationConfiguration flag,
		string name, string description)
	{
		// Flag identity parameters
		command.Parameters.AddWithValue("key", flag.Identifier.Key);
		command.Parameters.AddWithValue("application_name", (object?)flag.Identifier.ApplicationName ?? DBNull.Value);
		command.Parameters.AddWithValue("application_version", (object?)flag.Identifier.ApplicationVersion ?? DBNull.Value);
		command.Parameters.AddWithValue("scope", (int)flag.Identifier.Scope);

		command.Parameters.AddWithValue("name", name);
		command.Parameters.AddWithValue("description", description);


		// Schedule parameters - handle domain model defaults
		if (flag.Schedule.EnableOn == DateTime.MinValue)
			command.Parameters.AddWithValue("scheduled_enable_date", DBNull.Value);
		else
			command.Parameters.AddWithValue("scheduled_enable_date", flag.Schedule.EnableOn);

		if (flag.Schedule.DisableOn == DateTime.MaxValue)
			command.Parameters.AddWithValue("scheduled_disable_date", DBNull.Value);
		else
			command.Parameters.AddWithValue("scheduled_disable_date", flag.Schedule.DisableOn);

		// Operational window parameters
		command.Parameters.AddWithValue("window_start_time", flag.OperationalWindow.StartOn);
		command.Parameters.AddWithValue("window_end_time", flag.OperationalWindow.StopOn);
		command.Parameters.AddWithValue("time_zone", flag.OperationalWindow.TimeZone);

		// Access control parameters
		command.Parameters.AddWithValue("user_percentage_enabled", flag.UserAccessControl.RolloutPercentage);
		command.Parameters.AddWithValue("tenant_percentage_enabled", flag.TenantAccessControl.RolloutPercentage);

		// Variations
		command.Parameters.AddWithValue("default_variation", flag.Variations.DefaultVariation);

		// JSONB parameters require explicit type specification
		var evaluationModesParam = command.Parameters.Add("evaluation_modes", NpgsqlDbType.Jsonb);
		evaluationModesParam.Value = JsonSerializer.Serialize(flag.ActiveEvaluationModes.Modes.Select(m => (int)m), JsonDefaults.JsonOptions);

		var windowDaysParam = command.Parameters.Add("window_days", NpgsqlDbType.Jsonb);
		windowDaysParam.Value = JsonSerializer.Serialize(flag.OperationalWindow.DaysActive.Select(d => (int)d), JsonDefaults.JsonOptions);

		var targetingRulesParam = command.Parameters.Add("targeting_rules", NpgsqlDbType.Jsonb);
		targetingRulesParam.Value = JsonSerializer.Serialize(flag.TargetingRules, JsonDefaults.JsonOptions);

		var enabledUsersParam = command.Parameters.Add("enabled_users", NpgsqlDbType.Jsonb);
		enabledUsersParam.Value = JsonSerializer.Serialize(flag.UserAccessControl.Allowed, JsonDefaults.JsonOptions);

		var disabledUsersParam = command.Parameters.Add("disabled_users", NpgsqlDbType.Jsonb);
		disabledUsersParam.Value = JsonSerializer.Serialize(flag.UserAccessControl.Blocked, JsonDefaults.JsonOptions);

		var enabledTenantsParam = command.Parameters.Add("enabled_tenants", NpgsqlDbType.Jsonb);
		enabledTenantsParam.Value = JsonSerializer.Serialize(flag.TenantAccessControl.Allowed, JsonDefaults.JsonOptions);

		var disabledTenantsParam = command.Parameters.Add("disabled_tenants", NpgsqlDbType.Jsonb);
		disabledTenantsParam.Value = JsonSerializer.Serialize(flag.TenantAccessControl.Blocked, JsonDefaults.JsonOptions);

		var variationsParam = command.Parameters.Add("variations", NpgsqlDbType.Jsonb);
		variationsParam.Value = JsonSerializer.Serialize(flag.Variations.Values, JsonDefaults.JsonOptions);
	}
}
