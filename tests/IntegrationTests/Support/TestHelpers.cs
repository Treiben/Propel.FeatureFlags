using Npgsql;
using Propel.FeatureFlags.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatureFlags.IntegrationTests.Support;

public static class TestHelpers
{
	public static async Task CreatePostgresTables(string connectionString)
	{
		const string createTableSql = @"
		CREATE TABLE feature_flags (
			key VARCHAR(255) PRIMARY KEY,
			name VARCHAR(500) NOT NULL,
			description TEXT NOT NULL,
			evaluation_modes JSONB NOT NULL DEFAULT '[]',
			created_at TIMESTAMP NOT NULL,
			updated_at TIMESTAMP NULL,
			created_by VARCHAR(255) NOT NULL,
			updated_by VARCHAR(255) NULL,
			expiration_date TIMESTAMP NULL,
			scheduled_enable_date TIMESTAMPTZ,
			scheduled_disable_date TIMESTAMPTZ,
			window_start_time TIME NULL,
			window_end_time TIME NULL,
			time_zone VARCHAR(100) NULL,
			window_days JSONB NOT NULL DEFAULT '[]',
			percentage_enabled INTEGER NOT NULL DEFAULT 0,
			targeting_rules JSONB NOT NULL DEFAULT '[]',
			enabled_users JSONB NOT NULL DEFAULT '[]',
			disabled_users JSONB NOT NULL DEFAULT '[]',
			enabled_tenants JSONB NOT NULL DEFAULT '[]',
			disabled_tenants JSONB NOT NULL DEFAULT '[]',
			tenant_percentage_enabled INTEGER NOT NULL DEFAULT 0,
			variations JSONB NOT NULL DEFAULT '{}',
			default_variation VARCHAR(255) NOT NULL DEFAULT 'off',
			tags JSONB NOT NULL DEFAULT '{}',
			is_permanent BOOLEAN NOT NULL DEFAULT false
		);

		CREATE INDEX ix_feature_flags_evaluation_modes ON feature_flags USING GIN(evaluation_modes);
		CREATE INDEX ix_feature_flags_expiration_date ON feature_flags(expiration_date) WHERE expiration_date IS NOT NULL;
		CREATE INDEX ix_feature_flags_created_at ON feature_flags(created_at);
		CREATE INDEX ix_feature_flags_tags ON feature_flags USING GIN(tags);
	";

		using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		using var command = new NpgsqlCommand(createTableSql, connection);
		await command.ExecuteNonQueryAsync();
	}

	public static FeatureFlag CreateTestFlag(string key, FlagEvaluationMode evaluationMode)
	{
		return new FeatureFlag
		{
			Key = key,
			Name = $"Test Flag {key}",
			Description = "Test flag for integration tests",
			EvaluationModeSet = new FlagEvaluationModeSet([evaluationMode]),
			AuditRecord = new FlagAuditRecord(createdAt: DateTime.UtcNow, createdBy: "integration-test"),
		};
	}
}
