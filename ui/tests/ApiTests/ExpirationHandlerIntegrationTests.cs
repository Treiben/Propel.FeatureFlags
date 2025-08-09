using FeatureRabbit.Flags.Cache.Redis;
using FeatureRabbit.Flags.Core;
using FeatureRabbit.Flags.Persistence.PostgresSQL;
using FeatureRabbit.Management.Api.Endpoints;
using Microsoft.Extensions.Logging;
using Npgsql;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace ApiTests.ExpirationHandlerTests;

public class ExpirationHandler_SuccessfulRetrieval : IClassFixture<ExpirationHandlerFixture>
{
	private readonly ExpirationHandlerFixture _fixture;

	public ExpirationHandler_SuccessfulRetrieval(ExpirationHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_FlagsExpiringWithinDefaultPeriod_ThenReturnsAll()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var expiringFlag1 = _fixture.CreateTestFlag("expiring-1", FeatureFlagStatus.Enabled);
		expiringFlag1.ExpirationDate = DateTime.UtcNow.AddDays(3);
		
		var expiringFlag2 = _fixture.CreateTestFlag("expiring-2", FeatureFlagStatus.Disabled);
		expiringFlag2.ExpirationDate = DateTime.UtcNow.AddDays(5);
		
		var notExpiringFlag = _fixture.CreateTestFlag("not-expiring", FeatureFlagStatus.Enabled);
		notExpiringFlag.ExpirationDate = DateTime.UtcNow.AddDays(30);

		await _fixture.Repository.CreateAsync(expiringFlag1);
		await _fixture.Repository.CreateAsync(expiringFlag2);
		await _fixture.Repository.CreateAsync(notExpiringFlag);

		// Act
		var result = await _fixture.Handler.HandleAsync();

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		okResult.Value.Count.ShouldBe(2);
		okResult.Value.ShouldContain(f => f.Key == "expiring-1");
		okResult.Value.ShouldContain(f => f.Key == "expiring-2");
		okResult.Value.ShouldNotContain(f => f.Key == "not-expiring");
	}

	[Fact]
	public async Task If_CustomDaysPeriod_ThenReturnsCorrectFlags()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag1 = _fixture.CreateTestFlag("flag-1", FeatureFlagStatus.Enabled);
		flag1.ExpirationDate = DateTime.UtcNow.AddDays(1);
		
		var flag2 = _fixture.CreateTestFlag("flag-2", FeatureFlagStatus.Enabled);
		flag2.ExpirationDate = DateTime.UtcNow.AddDays(15);
		
		var flag3 = _fixture.CreateTestFlag("flag-3", FeatureFlagStatus.Enabled);
		flag3.ExpirationDate = DateTime.UtcNow.AddDays(25);

		await _fixture.Repository.CreateAsync(flag1);
		await _fixture.Repository.CreateAsync(flag2);
		await _fixture.Repository.CreateAsync(flag3);

		// Act
		var result = await _fixture.Handler.HandleAsync(20);

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		okResult.Value.Count.ShouldBe(2);
		okResult.Value.ShouldContain(f => f.Key == "flag-1");
		okResult.Value.ShouldContain(f => f.Key == "flag-2");
		okResult.Value.ShouldNotContain(f => f.Key == "flag-3");
	}

	[Fact]
	public async Task If_NoExpiringFlags_ThenReturnsEmptyList()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var futureFlag = _fixture.CreateTestFlag("future-flag", FeatureFlagStatus.Enabled);
		futureFlag.ExpirationDate = DateTime.UtcNow.AddDays(365);
		
		var noExpirationFlag = _fixture.CreateTestFlag("no-expiration", FeatureFlagStatus.Enabled);
		// No expiration date set

		await _fixture.Repository.CreateAsync(futureFlag);
		await _fixture.Repository.CreateAsync(noExpirationFlag);

		// Act
		var result = await _fixture.Handler.HandleAsync();

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		okResult.Value.Count.ShouldBe(0);
	}

	[Fact]
	public async Task If_FlagsWithDifferentStatuses_ThenReturnsAllExpiring()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var enabledFlag = _fixture.CreateTestFlag("enabled-expiring", FeatureFlagStatus.Enabled);
		enabledFlag.ExpirationDate = DateTime.UtcNow.AddDays(2);
		
		var disabledFlag = _fixture.CreateTestFlag("disabled-expiring", FeatureFlagStatus.Disabled);
		disabledFlag.ExpirationDate = DateTime.UtcNow.AddDays(4);
		
		var scheduledFlag = _fixture.CreateTestFlag("scheduled-expiring", FeatureFlagStatus.Scheduled);
		scheduledFlag.ExpirationDate = DateTime.UtcNow.AddDays(6);
		
		var percentageFlag = _fixture.CreateTestFlag("percentage-expiring", FeatureFlagStatus.Percentage);
		percentageFlag.ExpirationDate = DateTime.UtcNow.AddDays(1);

		await _fixture.Repository.CreateAsync(enabledFlag);
		await _fixture.Repository.CreateAsync(disabledFlag);
		await _fixture.Repository.CreateAsync(scheduledFlag);
		await _fixture.Repository.CreateAsync(percentageFlag);

		// Act
		var result = await _fixture.Handler.HandleAsync();

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		okResult.Value.Count.ShouldBe(4);
		okResult.Value.ShouldContain(f => f.Key == "enabled-expiring");
		okResult.Value.ShouldContain(f => f.Key == "disabled-expiring");
		okResult.Value.ShouldContain(f => f.Key == "scheduled-expiring");
		okResult.Value.ShouldContain(f => f.Key == "percentage-expiring");
	}
}

public class ExpirationHandler_PermanentFlags : IClassFixture<ExpirationHandlerFixture>
{
	private readonly ExpirationHandlerFixture _fixture;

	public ExpirationHandler_PermanentFlags(ExpirationHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_PermanentFlagsExpiring_ThenExcludesFromResults()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var regularFlag = _fixture.CreateTestFlag("regular-expiring", FeatureFlagStatus.Enabled);
		regularFlag.ExpirationDate = DateTime.UtcNow.AddDays(3);
		regularFlag.IsPermanent = false;
		
		var permanentFlag = _fixture.CreateTestFlag("permanent-expiring", FeatureFlagStatus.Enabled);
		permanentFlag.ExpirationDate = DateTime.UtcNow.AddDays(2);
		permanentFlag.IsPermanent = true;

		await _fixture.Repository.CreateAsync(regularFlag);
		await _fixture.Repository.CreateAsync(permanentFlag);

		// Act
		var result = await _fixture.Handler.HandleAsync();

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		okResult.Value.Count.ShouldBe(1);
		okResult.Value.ShouldContain(f => f.Key == "regular-expiring");
		okResult.Value.ShouldNotContain(f => f.Key == "permanent-expiring");
	}

	[Fact]
	public async Task If_MixOfPermanentAndRegularFlags_ThenOnlyReturnsRegular()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var regular1 = _fixture.CreateTestFlag("regular-1", FeatureFlagStatus.Enabled);
		regular1.ExpirationDate = DateTime.UtcNow.AddDays(1);
		regular1.IsPermanent = false;
		
		var permanent1 = _fixture.CreateTestFlag("permanent-1", FeatureFlagStatus.Disabled);
		permanent1.ExpirationDate = DateTime.UtcNow.AddDays(2);
		permanent1.IsPermanent = true;
		
		var regular2 = _fixture.CreateTestFlag("regular-2", FeatureFlagStatus.Percentage);
		regular2.ExpirationDate = DateTime.UtcNow.AddDays(5);
		regular2.IsPermanent = false;
		
		var permanent2 = _fixture.CreateTestFlag("permanent-2", FeatureFlagStatus.UserTargeted);
		permanent2.ExpirationDate = DateTime.UtcNow.AddDays(3);
		permanent2.IsPermanent = true;

		await _fixture.Repository.CreateAsync(regular1);
		await _fixture.Repository.CreateAsync(permanent1);
		await _fixture.Repository.CreateAsync(regular2);
		await _fixture.Repository.CreateAsync(permanent2);

		// Act
		var result = await _fixture.Handler.HandleAsync();

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		okResult.Value.Count.ShouldBe(2);
		okResult.Value.ShouldContain(f => f.Key == "regular-1");
		okResult.Value.ShouldContain(f => f.Key == "regular-2");
		okResult.Value.ShouldNotContain(f => f.Key == "permanent-1");
		okResult.Value.ShouldNotContain(f => f.Key == "permanent-2");
	}
}

public class ExpirationHandler_EdgeCases : IClassFixture<ExpirationHandlerFixture>
{
	private readonly ExpirationHandlerFixture _fixture;

	public ExpirationHandler_EdgeCases(ExpirationHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_FlagExpiringToday_ThenIncludesInResults()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var todayFlag = _fixture.CreateTestFlag("expiring-today", FeatureFlagStatus.Enabled);
		todayFlag.ExpirationDate = DateTime.UtcNow.AddHours(12);

		await _fixture.Repository.CreateAsync(todayFlag);

		// Act
		var result = await _fixture.Handler.HandleAsync();

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		okResult.Value.Count.ShouldBe(1);
		okResult.Value[0].Key.ShouldBe("expiring-today");
	}

	[Fact]
	public async Task If_FlagExpiringExactlyAtCutoff_ThenIncludesInResults()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var cutoffFlag = _fixture.CreateTestFlag("cutoff-flag", FeatureFlagStatus.Enabled);
		cutoffFlag.ExpirationDate = DateTime.UtcNow.AddDays(7);

		await _fixture.Repository.CreateAsync(cutoffFlag);

		// Act
		var result = await _fixture.Handler.HandleAsync(7);

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		okResult.Value.Count.ShouldBe(1);
		okResult.Value[0].Key.ShouldBe("cutoff-flag");
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(30)]
	[InlineData(365)]
	public async Task If_DifferentDaysPeriods_ThenReturnsCorrectFlags(int days)
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flagWithinPeriod = _fixture.CreateTestFlag($"within-{days}", FeatureFlagStatus.Enabled);
		flagWithinPeriod.ExpirationDate = DateTime.UtcNow.AddDays(Math.Max(1, days - 1));
		
		var flagOutsidePeriod = _fixture.CreateTestFlag($"outside-{days}", FeatureFlagStatus.Enabled);
		flagOutsidePeriod.ExpirationDate = DateTime.UtcNow.AddDays(days + 10);

		await _fixture.Repository.CreateAsync(flagWithinPeriod);
		await _fixture.Repository.CreateAsync(flagOutsidePeriod);

		// Act
		var result = await _fixture.Handler.HandleAsync(days);

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		if (days > 0)
		{
			okResult.Value.Count.ShouldBe(1);
			okResult.Value[0].Key.ShouldBe($"within-{days}");
		}
		else
		{
			okResult.Value.Count.ShouldBe(0);
		}
	}

	[Fact]
	public async Task If_AlreadyExpiredFlags_ThenIncludesInResults()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var expiredFlag = _fixture.CreateTestFlag("already-expired", FeatureFlagStatus.Enabled);
		expiredFlag.ExpirationDate = DateTime.UtcNow.AddDays(-1);

		await _fixture.Repository.CreateAsync(expiredFlag);

		// Act
		var result = await _fixture.Handler.HandleAsync();

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		okResult.Value.Count.ShouldBe(1);
		okResult.Value[0].Key.ShouldBe("already-expired");
	}
}

public class ExpirationHandler_DataTransformation : IClassFixture<ExpirationHandlerFixture>
{
	private readonly ExpirationHandlerFixture _fixture;

	public ExpirationHandler_DataTransformation(ExpirationHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_ExpiringFlags_ThenReturnsCorrectDto()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag = _fixture.CreateTestFlag("complex-flag", FeatureFlagStatus.UserTargeted);
		flag.ExpirationDate = DateTime.UtcNow.AddDays(3);
		flag.TargetingRules = new List<TargetingRule>
		{
			new TargetingRule
			{
				Attribute = "country",
				Operator = TargetingOperator.In,
				Values = new List<string> { "US" },
				Variation = "us-version"
			}
		};
		flag.EnabledUsers = new List<string> { "user1", "user2" };
		flag.Variations = new Dictionary<string, object>
		{
			{ "on", "premium" },
			{ "off", "basic" }
		};
		flag.Tags = new Dictionary<string, string>
		{
			{ "team", "backend" },
			{ "priority", "high" }
		};

		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync();

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		okResult.Value.Count.ShouldBe(1);
		var dto = okResult.Value[0];
		
		dto.Key.ShouldBe("complex-flag");
		dto.Status.ShouldBe(FeatureFlagStatus.UserTargeted.ToString());
		dto.TargetingRules.Count.ShouldBe(1);
		dto.EnabledUsers.Count.ShouldBe(2);
		dto.Variations.Count.ShouldBe(2);
		dto.Tags.Count.ShouldBe(2);
		dto.ExpirationDate.ShouldNotBeNull();
	}

	[Fact]
	public async Task If_FlagsWithTimeWindows_ThenConvertsTimeSpanToTimeOnly()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag = _fixture.CreateTestFlag("time-window-flag", FeatureFlagStatus.TimeWindow);
		flag.ExpirationDate = DateTime.UtcNow.AddDays(5);
		flag.WindowStartTime = TimeSpan.FromHours(9);
		flag.WindowEndTime = TimeSpan.FromHours(17);
		flag.TimeZone = "America/New_York";
		flag.WindowDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Friday };

		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync();

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		
		okResult.Value.Count.ShouldBe(1);
		var dto = okResult.Value[0];
		
		dto.WindowStartTime.ShouldBe(new TimeOnly(9, 0));
		dto.WindowEndTime.ShouldBe(new TimeOnly(17, 0));
		dto.TimeZone.ShouldBe("America/New_York");
		dto.WindowDays.ShouldContain(DayOfWeek.Monday);
		dto.WindowDays.ShouldContain(DayOfWeek.Friday);
	}
}

public class ExpirationHandler_ErrorScenarios : IClassFixture<ExpirationHandlerFixture>
{
	private readonly ExpirationHandlerFixture _fixture;

	public ExpirationHandler_ErrorScenarios(ExpirationHandlerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task If_DatabaseConnectionFails_ThenLogsErrorAndReturns500()
	{
		// Arrange
		var badConnectionString = "Host=nonexistent;Database=fake;Username=fake;Password=fake";
		var badRepository = new PostgreSQLFeatureFlagRepository(badConnectionString, new Mock<ILogger<PostgreSQLFeatureFlagRepository>>().Object);
		var badHandler = new ExpirationHandler(badRepository, _fixture.MockLogger.Object);

		// Act
		var result = await badHandler.HandleAsync();

		// Assert
		result.ShouldBeOfType<StatusCodeHttpResult>();
		var statusResult = (StatusCodeHttpResult)result;
		statusResult.StatusCode.ShouldBe(500);
	}

	[Fact]
	public async Task If_NegativeDays_ThenHandlesGracefully()
	{
		// Arrange
		await _fixture.ClearAllData();
		
		var flag = _fixture.CreateTestFlag("test-flag", FeatureFlagStatus.Enabled);
		flag.ExpirationDate = DateTime.UtcNow.AddDays(-5);
		await _fixture.Repository.CreateAsync(flag);

		// Act
		var result = await _fixture.Handler.HandleAsync(-10);

		// Assert
		result.ShouldBeOfType<Ok<List<FeatureFlagDto>>>();
		var okResult = (Ok<List<FeatureFlagDto>>)result;
		okResult.Value.Count.ShouldBe(0);
	}
}

public class ExpirationHandlerFixture : IAsyncLifetime
{
	private readonly PostgreSqlContainer _postgresContainer;
	private readonly RedisContainer _redisContainer;
	
	public ExpirationHandler Handler { get; private set; } = null!;
	public PostgreSQLFeatureFlagRepository Repository { get; private set; } = null!;
	public RedisFeatureFlagCache Cache { get; private set; } = null!;
	public Mock<ILogger<ExpirationHandler>> MockLogger { get; }
	
	private IConnectionMultiplexer _redisConnection = null!;

	public ExpirationHandlerFixture()
	{
		_postgresContainer = new PostgreSqlBuilder()
			.WithImage("postgres:15-alpine")
			.WithDatabase("featureflags")
			.WithUsername("postgres")
			.WithPassword("postgres")
			.WithPortBinding(5432, true)
			.Build();

		_redisContainer = new RedisBuilder()
			.WithImage("redis:7-alpine")
			.WithPortBinding(6379, true)
			.Build();

		MockLogger = new Mock<ILogger<ExpirationHandler>>();
	}

	public async Task InitializeAsync()
	{
		// Start containers
		await _postgresContainer.StartAsync();
		await _redisContainer.StartAsync();

		// Setup PostgreSQL
		var postgresConnectionString = _postgresContainer.GetConnectionString();
		await CreatePostgresTables(postgresConnectionString);
		Repository = new PostgreSQLFeatureFlagRepository(postgresConnectionString, new Mock<ILogger<PostgreSQLFeatureFlagRepository>>().Object);

		// Setup Redis
		var redisConnectionString = _redisContainer.GetConnectionString();
		_redisConnection = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
		Cache = new RedisFeatureFlagCache(_redisConnection, new Mock<ILogger<RedisFeatureFlagCache>>().Object);

		// Setup Handler
		Handler = new ExpirationHandler(Repository, MockLogger.Object);
	}

	public async Task DisposeAsync()
	{
		await DisposeContainers();
	}

	public async Task DisposeContainers()
	{
		_redisConnection?.Dispose();
		await _postgresContainer.DisposeAsync();
		await _redisContainer.DisposeAsync();
	}

	private async Task CreatePostgresTables(string connectionString)
	{
		const string createTableSql = @"
		CREATE TABLE feature_flags (
			key VARCHAR(255) PRIMARY KEY,
			name VARCHAR(500) NOT NULL,
			description TEXT NOT NULL,
			status INTEGER NOT NULL,
			created_at TIMESTAMP NOT NULL,
			updated_at TIMESTAMP NOT NULL,
			created_by VARCHAR(255) NOT NULL,
			updated_by VARCHAR(255) NOT NULL,
			expiration_date TIMESTAMP NULL,
			scheduled_enable_date TIMESTAMP NULL,
			scheduled_disable_date TIMESTAMP NULL,
			window_start_time TIME NULL,
			window_end_time TIME NULL,
			time_zone VARCHAR(100) NULL,
			window_days JSONB NOT NULL DEFAULT '[]',
			percentage_enabled INTEGER NOT NULL DEFAULT 0,
			targeting_rules JSONB NOT NULL DEFAULT '[]',
			enabled_users JSONB NOT NULL DEFAULT '[]',
			disabled_users JSONB NOT NULL DEFAULT '[]',
			variations JSONB NOT NULL DEFAULT '{}',
			default_variation VARCHAR(255) NOT NULL DEFAULT 'off',
			tags JSONB NOT NULL DEFAULT '{}',
			is_permanent BOOLEAN NOT NULL DEFAULT false
		);

		CREATE INDEX ix_feature_flags_status ON feature_flags(status);
		CREATE INDEX ix_feature_flags_expiration_date ON feature_flags(expiration_date) WHERE expiration_date IS NOT NULL;
		CREATE INDEX ix_feature_flags_created_at ON feature_flags(created_at);
		CREATE INDEX ix_feature_flags_tags ON feature_flags USING GIN(tags);
	";

		using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		using var command = new NpgsqlCommand(createTableSql, connection);
		await command.ExecuteNonQueryAsync();
	}

	public FeatureFlag CreateTestFlag(string key, FeatureFlagStatus status)
	{
		return new FeatureFlag
		{
			Key = key,
			Name = $"Test Flag {key}",
			Description = "Test flag for integration tests",
			Status = status,
			CreatedAt = DateTime.UtcNow,
			UpdatedAt = DateTime.UtcNow,
			CreatedBy = "integration-test",
			UpdatedBy = "integration-test",
			DefaultVariation = "off",
			TargetingRules = new List<TargetingRule>(),
			EnabledUsers = new List<string>(),
			DisabledUsers = new List<string>(),
			Variations = new Dictionary<string, object>(),
			Tags = new Dictionary<string, string>(),
			WindowDays = new List<DayOfWeek>(),
			PercentageEnabled = 0,
			IsPermanent = false
		};
	}

	public async Task ClearAllData()
	{
		var connectionString = _postgresContainer.GetConnectionString();
		using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync();
		using var command = new NpgsqlCommand("DELETE FROM feature_flags", connection);
		await command.ExecuteNonQueryAsync();

		await Cache.ClearAsync();
		MockLogger.Reset();
	}
}