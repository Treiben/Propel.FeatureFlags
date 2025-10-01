// ===== COMMAND LINE INTERFACE =====

using System.CommandLine;
using System.Text.Json;

namespace FeatureFlags.CLI
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand("Feature Flag Management CLI")
            {
                Description = "Manage feature flags from the command line"
            };

            // Create commands
            rootCommand.AddCommand(CreateListCommand());
            rootCommand.AddCommand(CreateGetCommand());
            rootCommand.AddCommand(CreateSetCommand());
            rootCommand.AddCommand(CreateEnableCommand());
            rootCommand.AddCommand(CreateDisableCommand());
            rootCommand.AddCommand(CreateScheduleCommand());
            rootCommand.AddCommand(CreatePercentageCommand());
            rootCommand.AddCommand(CreateExportCommand());
            rootCommand.AddCommand(CreateImportCommand());
            rootCommand.AddCommand(CreateValidateCommand());

            return await rootCommand.InvokeAsync(args);
        }

        private static Command CreateListCommand()
        {
            var command = new Command("list", "List all feature flags")
            {
                new Option<string?>("--filter", "Filter flags by status"),
                new Option<string?>("--tag", "Filter flags by tag"),
                new Option<bool>("--json", "Output as JSON")
            };

            command.SetHandler(async (filter, tag, json) =>
            {
                var client = CreateApiClient();
                var flags = await client.GetFlagsAsync(filter, tag);
                
                if (json)
                {
                    Console.WriteLine(JsonSerializer.Serialize(flags, new JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    await DisplayFlagsTable(flags);
                }
            }, command.Options[0] as Option<string?>, command.Options[1] as Option<string?>, command.Options[2] as Option<bool>);

            return command;
        }

        private static Command CreateGetCommand()
        {
            var command = new Command("get", "Get details of a specific feature flag")
            {
                new Argument<string>("key", "Feature flag key"),
                new Option<bool>("--json", "Output as JSON")
            };

            command.SetHandler(async (key, json) =>
            {
                var client = CreateApiClient();
                var flag = await client.GetFlagAsync(key);
                
                if (flag == null)
                {
                    Console.WriteLine($"Feature flag '{key}' not found");
                    Environment.Exit(1);
                    return;
                }

                if (json)
                {
                    Console.WriteLine(JsonSerializer.Serialize(flag, new JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    await DisplayFlagDetails(flag);
                }
            }, command.Arguments[0] as Argument<string>, command.Options[0] as Option<bool>);

            return command;
        }

        private static Command CreateSetCommand()
        {
            var command = new Command("set", "Create or update a feature flag")
            {
                new Argument<string>("key", "Feature flag key"),
                new Option<string>("--name", "Display name") { IsRequired = true },
                new Option<string>("--description", "Description"),
                new Option<string>("--status", "Status (Enabled, Disabled, Scheduled, Percentage, UserTargeted)"),
                new Option<string>("--tags", "Tags as JSON object"),
                new Option<DateTime?>("--expires", "Expiration date"),
                new Option<bool>("--permanent", "Mark as permanent flag")
            };

            command.SetHandler(async (key, name, description, status, tags, expires, permanent) =>
            {
                var client = CreateApiClient();
                
                var tagDict = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(tags))
                {
                    tagDict = JsonSerializer.Deserialize<Dictionary<string, string>>(tags) ?? new();
                }

                var request = new CreateFeatureFlagRequest
                {
                    Key = key,
                    Name = name,
                    Description = description ?? "",
                    Status = Enum.TryParse<FeatureFlagStatus>(status, out var statusEnum) ? statusEnum : FeatureFlagStatus.Disabled,
                    Tags = tagDict,
                    ExpirationDate = expires,
                    IsPermanent = permanent
                };

                var result = await client.CreateOrUpdateFlagAsync(request);
                Console.WriteLine($"Feature flag '{key}' created/updated successfully");
                
            }, command.Arguments[0] as Argument<string>, 
               command.Options[0] as Option<string>, 
               command.Options[1] as Option<string>, 
               command.Options[2] as Option<string>, 
               command.Options[3] as Option<string>, 
               command.Options[4] as Option<DateTime?>, 
               command.Options[5] as Option<bool>);

            return command;
        }

        private static Command CreateEnableCommand()
        {
            var command = new Command("enable", "Enable a feature flag")
            {
                new Argument<string>("key", "Feature flag key"),
                new Option<string>("--reason", "Reason for enabling")
            };

            command.SetHandler(async (key, reason) =>
            {
                var client = CreateApiClient();
                await client.EnableFlagAsync(key, reason);
                Console.WriteLine($"Feature flag '{key}' enabled");
            }, command.Arguments[0] as Argument<string>, command.Options[0] as Option<string>);

            return command;
        }

        private static Command CreateDisableCommand()
        {
            var command = new Command("disable", "Disable a feature flag")
            {
                new Argument<string>("key", "Feature flag key"),
                new Option<string>("--reason", "Reason for disabling")
            };

            command.SetHandler(async (key, reason) =>
            {
                var client = CreateApiClient();
                await client.DisableFlagAsync(key, reason);
                Console.WriteLine($"Feature flag '{key}' disabled");
            }, command.Arguments[0] as Argument<string>, command.Options[0] as Option<string>);

            return command;
        }

        private static Command CreateScheduleCommand()
        {
            var command = new Command("schedule", "Schedule a feature flag")
            {
                new Argument<string>("key", "Feature flag key"),
                new Option<DateTime>("--enable-at", "When to enable the flag") { IsRequired = true },
                new Option<DateTime?>("--disable-at", "When to disable the flag")
            };

            command.SetHandler(async (key, enableAt, disableAt) =>
            {
                var client = CreateApiClient();
                await client.ScheduleFlagAsync(key, enableAt, disableAt);
                Console.WriteLine($"Feature flag '{key}' scheduled for {enableAt}");
            }, command.Arguments[0] as Argument<string>, 
               command.Options[0] as Option<DateTime>, 
               command.Options[1] as Option<DateTime?>);

            return command;
        }

        private static Command CreatePercentageCommand()
        {
            var command = new Command("percentage", "Set percentage rollout for a feature flag")
            {
                new Argument<string>("key", "Feature flag key"),
                new Argument<int>("percentage", "Percentage (0-100)")
            };

            command.SetHandler(async (key, percentage) =>
            {
                if (percentage < 0 || percentage > 100)
                {
                    Console.WriteLine("Percentage must be between 0 and 100");
                    Environment.Exit(1);
                    return;
                }

                var client = CreateApiClient();
                await client.SetPercentageAsync(key, percentage);
                Console.WriteLine($"Feature flag '{key}' set to {percentage}% rollout");
            }, command.Arguments[0] as Argument<string>, command.Arguments[1] as Argument<int>);

            return command;
        }

        private static Command CreateExportCommand()
        {
            var command = new Command("export", "Export feature flags to a file")
            {
                new Option<string>("--output", "Output file path (default: flags.json)"),
                new Option<string?>("--filter", "Filter flags by status"),
                new Option<bool>("--include-audit", "Include audit information")
            };

            command.SetHandler(async (output, filter, includeAudit) =>
            {
                var client = CreateApiClient();
                var flags = await client.GetFlagsAsync(filter);
                
                var exportData = new
                {
                    ExportedAt = DateTime.UtcNow,
                    Version = "1.0",
                    Flags = flags,
                    IncludesAudit = includeAudit
                };

                var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
                var filePath = output ?? "flags.json";
                
                await File.WriteAllTextAsync(filePath, json);
                Console.WriteLine($"Exported {flags.Count} feature flags to {filePath}");
                
            }, command.Options[0] as Option<string>, 
               command.Options[1] as Option<string?>, 
               command.Options[2] as Option<bool>);

            return command;
        }

        private static Command CreateImportCommand()
        {
            var command = new Command("import", "Import feature flags from a file")
            {
                new Argument<string>("file", "Input file path"),
                new Option<bool>("--dry-run", "Preview changes without applying them"),
                new Option<bool>("--update-existing", "Update existing flags")
            };

            command.SetHandler(async (file, dryRun, updateExisting) =>
            {
                if (!File.Exists(file))
                {
                    Console.WriteLine($"File '{file}' not found");
                    Environment.Exit(1);
                    return;
                }

                var json = await File.ReadAllTextAsync(file);
                var importData = JsonSerializer.Deserialize<ImportData>(json);
                
                if (importData?.Flags == null)
                {
                    Console.WriteLine("Invalid import file format");
                    Environment.Exit(1);
                    return;
                }

                var client = CreateApiClient();
                
                Console.WriteLine($"Importing {importData.Flags.Count} feature flags...");
                
                if (dryRun)
                {
                    Console.WriteLine("DRY RUN - No changes will be made");
                }

                foreach (var flag in importData.Flags)
                {
                    try
                    {
                        if (!dryRun)
                        {
                            if (updateExisting)
                            {
                                await client.CreateOrUpdateFlagAsync(flag);
                            }
                            else
                            {
                                await client.CreateFlagAsync(flag);
                            }
                        }
                        
                        Console.WriteLine($"✓ {flag.Key} - {flag.Name}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"✗ {flag.Key} - Error: {ex.Message}");
                    }
                }
                
            }, command.Arguments[0] as Argument<string>, 
               command.Options[0] as Option<bool>, 
               command.Options[1] as Option<bool>);

            return command;
        }

        private static Command CreateValidateCommand()
        {
            var command = new Command("validate", "Validate feature flag configuration")
            {
                new Option<string?>("--config", "Configuration file to validate"),
                new Option<bool>("--check-unused", "Check for unused flags"),
                new Option<bool>("--check-expired", "Check for expired flags")
            };

            command.SetHandler(async (config, checkUnused, checkExpired) =>
            {
                var client = CreateApiClient();
                var flags = await client.GetFlagsAsync();
                
                var issues = new List<string>();

                // Check for flags without expiration dates
                var permanentFlags = flags.Where(f => !f.ExpirationDate.HasValue && !f.IsPermanent).ToList();
                if (permanentFlags.Any())
                {
                    issues.Add($"⚠️  {permanentFlags.Count} flags without expiration dates: {string.Join(", ", permanentFlags.Select(f => f.Key))}");
                }

                // Check for expired flags
                if (checkExpired)
                {
                    var expiredFlags = flags.Where(f => f.ExpirationDate.HasValue && f.ExpirationDate < DateTime.UtcNow).ToList();
                    if (expiredFlags.Any())
                    {
                        issues.Add($"🚨 {expiredFlags.Count} expired flags: {string.Join(", ", expiredFlags.Select(f => f.Key))}");
                    }
                }

                // Check for flags expiring soon
                var soonToExpire = flags.Where(f => f.ExpirationDate.HasValue && 
                                               f.ExpirationDate < DateTime.UtcNow.AddDays(7) && 
                                               f.ExpirationDate > DateTime.UtcNow).ToList();
                if (soonToExpire.Any())
                {
                    issues.Add($"⏰ {soonToExpire.Count} flags expiring within 7 days: {string.Join(", ", soonToExpire.Select(f => f.Key))}");
                }

                if (issues.Any())
                {
                    Console.WriteLine("Validation Issues Found:");
                    foreach (var issue in issues)
                    {
                        Console.WriteLine(issue);
                    }
                    Environment.Exit(1);
                }
                else
                {
                    Console.WriteLine("✅ All feature flags pass validation");
                }
                
            }, command.Options[0] as Option<string?>, 
               command.Options[1] as Option<bool>, 
               command.Options[2] as Option<bool>);

            return command;
        }

        private static FeatureFlagApiClient CreateApiClient()
        {
            var apiUrl = Environment.GetEnvironmentVariable("FEATURE_FLAG_API_URL") ?? "http://localhost:5000";
            var apiKey = Environment.GetEnvironmentVariable("FEATURE_FLAG_API_KEY");
            
            return new FeatureFlagApiClient(apiUrl, apiKey);
        }

        private static async Task DisplayFlagsTable(List<FeatureFlagDto> flags)
        {
            if (!flags.Any())
            {
                Console.WriteLine("No feature flags found");
                return;
            }

            Console.WriteLine($"{"Key",-30} {"Name",-25} {"Status",-12} {"Updated",-20}");
            Console.WriteLine(new string('-', 87));
            
            foreach (var flag in flags)
            {
                var updatedAt = flag.UpdatedAt.ToString("yyyy-MM-dd HH:mm");
                Console.WriteLine($"{flag.Key,-30} {TruncateString(flag.Name, 25),-25} {flag.Status,-12} {updatedAt,-20}");
            }
        }

        private static async Task DisplayFlagDetails(FeatureFlagDto flag)
        {
            Console.WriteLine($"Feature Flag: {flag.Name}");
            Console.WriteLine($"Key: {flag.Key}");
            Console.WriteLine($"Status: {flag.Status}");
            Console.WriteLine($"Description: {flag.Description}");
            Console.WriteLine($"Created: {flag.CreatedAt:yyyy-MM-dd HH:mm} by {flag.CreatedBy}");
            Console.WriteLine($"Updated: {flag.UpdatedAt:yyyy-MM-dd HH:mm} by {flag.UpdatedBy}");
            
            if (flag.ExpirationDate.HasValue)
                Console.WriteLine($"Expires: {flag.ExpirationDate:yyyy-MM-dd HH:mm}");
            
            if (flag.Status == "Percentage")
                Console.WriteLine($"Percentage: {flag.PercentageEnabled}%");
            
            if (flag.ScheduledEnableDate.HasValue)
                Console.WriteLine($"Scheduled Enable: {flag.ScheduledEnableDate:yyyy-MM-dd HH:mm}");
            
            if (flag.ScheduledDisableDate.HasValue)
                Console.WriteLine($"Scheduled Disable: {flag.ScheduledDisableDate:yyyy-MM-dd HH:mm}");
            
            if (flag.Tags.Any())
            {
                Console.WriteLine("Tags:");
                foreach (var tag in flag.Tags)
                {
                    Console.WriteLine($"  {tag.Key}: {tag.Value}");
                }
            }
        }

        private static string TruncateString(string str, int maxLength)
        {
            if (str.Length <= maxLength) return str;
            return str.Substring(0, maxLength - 3) + "...";
        }
    }

    // ===== API CLIENT FOR CLI =====
    public class FeatureFlagApiClient
    {
        private readonly HttpClient _httpClient;

        public FeatureFlagApiClient(string baseUrl, string? apiKey = null)
        {
            _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
            
            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            }
        }

        public async Task<List<FeatureFlagDto>> GetFlagsAsync(string? filter = null, string? tag = null)
        {
            var query = "";
            if (!string.IsNullOrEmpty(filter))
                query += $"?status={filter}";
            if (!string.IsNullOrEmpty(tag))
                query += string.IsNullOrEmpty(query) ? $"?tag={tag}" : $"&tag={tag}";

            var response = await _httpClient.GetAsync($"/api/feature-flags{query}");
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<FeatureFlagDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }

        public async Task<FeatureFlagDto?> GetFlagAsync(string key)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/feature-flags/{key}");
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;
                
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<FeatureFlagDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return null;
            }
        }

        public async Task<FeatureFlagDto> CreateFlagAsync(CreateFeatureFlagRequest request)
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/api/feature-flags", content);
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FeatureFlagDto>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }

        public async Task<FeatureFlagDto> CreateOrUpdateFlagAsync(CreateFeatureFlagRequest request)
        {
            var existing = await GetFlagAsync(request.Key);
            if (existing != null)
            {
                return await UpdateFlagAsync(request.Key, request);
            }
            else
            {
                return await CreateFlagAsync(request);
            }
        }

        public async Task<FeatureFlagDto> UpdateFlagAsync(string key, CreateFeatureFlagRequest request)
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PutAsync($"/api/feature-flags/{key}", content);
            response.EnsureSuccessStatusCode();
            
            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FeatureFlagDto>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }

        public async Task EnableFlagAsync(string key, string? reason = null)
        {
            var request = new { Reason = reason };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"/api/feature-flags/{key}/enable", content);
            response.EnsureSuccessStatusCode();
        }

        public async Task DisableFlagAsync(string key, string? reason = null)
        {
            var request = new { Reason = reason };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"/api/feature-flags/{key}/disable", content);
            response.EnsureSuccessStatusCode();
        }

        public async Task ScheduleFlagAsync(string key, DateTime enableDate, DateTime? disableDate = null)
        {
            var request = new { EnableDate = enableDate, DisableDate = disableDate };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"/api/feature-flags/{key}/schedule", content);
            response.EnsureSuccessStatusCode();
        }

        public async Task SetPercentageAsync(string key, int percentage)
        {
            var request = new { Percentage = percentage };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"/api/feature-flags/{key}/percentage", content);
            response.EnsureSuccessStatusCode();
        }
    }

    public class ImportData
    {
        public DateTime ExportedAt { get; set; }
        public string Version { get; set; } = string.Empty;
        public List<CreateFeatureFlagRequest> Flags { get; set; } = new();
        public bool IncludesAudit { get; set; }
    }
}

// ===== DEVOPS POWERSHELL SCRIPTS =====

/*
# PowerShell script for deployment automation
# File: Deploy-FeatureFlags.ps1

param(
    [Parameter(Mandatory=$true)]
    [string]$Environment,
    
    [Parameter(Mandatory=$true)]
    [string]$ConfigFile,
    
    [Parameter(Mandatory=$false)]
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Configuration
$ApiUrl = switch ($Environment) {
    "dev" { "https://featureflags-dev.yourcompany.com" }
    "staging" { "https://featureflags-staging.yourcompany.com" }
    "prod" { "https://featureflags-prod.yourcompany.com" }
    default { throw "Unknown environment: $Environment" }
}

$ApiKey = $env:FEATURE_FLAG_API_KEY
if (-not $ApiKey) {
    throw "FEATURE_FLAG_API_KEY environment variable is required"
}

Write-Host "Deploying feature flags to $Environment environment..." -ForegroundColor Green

# Validate configuration file
if (-not (Test-Path $ConfigFile)) {
    throw "Configuration file not found: $ConfigFile"
}

$Config = Get-Content $ConfigFile | ConvertFrom-Json

Write-Host "Found $($Config.flags.Count) feature flags in configuration" -ForegroundColor Yellow

# Deploy each flag
foreach ($Flag in $Config.flags) {
    try {
        $DryRunFlag = if ($DryRun) { "--dry-run" } else { "" }
        
        $Command = "ff-cli set $($Flag.key) --name '$($Flag.name)' --description '$($Flag.description)' --status $($Flag.status) $DryRunFlag"
        
        if ($Flag.tags) {
            $TagsJson = $Flag.tags | ConvertTo-Json -Compress
            $Command += " --tags '$TagsJson'"
        }
        
        if ($Flag.expirationDate) {
            $Command += " --expires '$($Flag.expirationDate)'"
        }
        
        Write-Host "Executing: $Command" -ForegroundColor Cyan
        
        if (-not $DryRun) {
            Invoke-Expression $Command
        }
        
        Write-Host "✓ $($Flag.key) deployed successfully" -ForegroundColor Green
    }
    catch {
        Write-Host "✗ Failed to deploy $($Flag.key): $($_.Exception.Message)" -ForegroundColor Red
        throw
    }
}

Write-Host "Deployment completed successfully!" -ForegroundColor Green
*/

// ===== MONITORING AND ALERTING =====

namespace FeatureFlags.Monitoring
{
    public interface IFeatureFlagMonitor
    {
        Task CheckFlagHealthAsync(CancellationToken cancellationToken = default);
        Task<HealthReport> GetHealthReportAsync(CancellationToken cancellationToken = default);
    }

    public class FeatureFlagMonitor : IFeatureFlagMonitor
    {
        private readonly IFeatureFlagRepository _repository;
        private readonly ILogger<FeatureFlagMonitor> _logger;

        public FeatureFlagMonitor(IFeatureFlagRepository repository, ILogger<FeatureFlagMonitor> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task CheckFlagHealthAsync(CancellationToken cancellationToken = default)
        {
            var flags = await _repository.GetAllAsync(cancellationToken);
            var now = DateTime.UtcNow;

            // Check for expired flags
            var expiredFlags = flags.Where(f => f.ExpirationDate.HasValue && f.ExpirationDate < now && f.Status != FeatureFlagStatus.Disabled).ToList();
            if (expiredFlags.Any())
            {
                _logger.LogWarning("Found {Count} expired but still active flags: {Flags}", 
                    expiredFlags.Count, string.Join(", ", expiredFlags.Select(f => f.Key)));
            }

            // Check for flags without expiration
            var noExpirationFlags = flags.Where(f => !f.ExpirationDate.HasValue && !f.IsPermanent).ToList();
            if (noExpirationFlags.Any())
            {
                _logger.LogWarning("Found {Count} temporary flags without expiration dates: {Flags}", 
                    noExpirationFlags.Count, string.Join(", ", noExpirationFlags.Select(f => f.Key)));
            }

            // Check for scheduled flags that should be enabled/disabled
            var overdueScheduled = flags.Where(f => f.Status == FeatureFlagStatus.Scheduled && 
                                               f.ScheduledEnableDate.HasValue && 
                                               f.ScheduledEnableDate < now).ToList();
            if (overdueScheduled.Any())
            {
                _logger.LogError("Found {Count} scheduled flags that should be enabled: {Flags}", 
                    overdueScheduled.Count, string.Join(", ", overdueScheduled.Select(f => f.Key)));
            }
        }

        public async Task<HealthReport> GetHealthReportAsync(CancellationToken cancellationToken = default)
        {
            var flags = await _repository.GetAllAsync(cancellationToken);
            var now = DateTime.UtcNow;

            return new HealthReport
            {
                TotalFlags = flags.Count,
                EnabledFlags = flags.Count(f => f.Status == FeatureFlagStatus.Enabled),
                DisabledFlags = flags.Count(f => f.Status == FeatureFlagStatus.Disabled),
                ScheduledFlags = flags.Count(f => f.Status == FeatureFlagStatus.Scheduled),
                PercentageFlags = flags.Count(f => f.Status == FeatureFlagStatus.Percentage),
                ExpiredFlags = flags.Count(f => f.ExpirationDate.HasValue && f.ExpirationDate < now),
                ExpiringWithin7Days = flags.Count(f => f.ExpirationDate.HasValue && 
                                                      f.ExpirationDate < now.AddDays(7) && 
                                                      f.ExpirationDate > now),
                FlagsWithoutExpiration = flags.Count(f => !f.ExpirationDate.HasValue && !f.IsPermanent),
                PermanentFlags = flags.Count(f => f.IsPermanent),
                GeneratedAt = now
            };
        }
    }

    public class HealthReport
    {
        public int TotalFlags { get; set; }
        public int EnabledFlags { get; set; }
        public int DisabledFlags { get; set; }
        public int ScheduledFlags { get; set; }
        public int PercentageFlags { get; set; }
        public int ExpiredFlags { get; set; }
        public int ExpiringWithin7Days { get; set; }
        public int FlagsWithoutExpiration { get; set; }
        public int PermanentFlags { get; set; }
        public DateTime GeneratedAt { get; set; }

        public bool IsHealthy => ExpiredFlags == 0 && FlagsWithoutExpiration == 0;
    }

    // Health check for ASP.NET Core
    public class FeatureFlagHealthCheck : IHealthCheck
    {
        private readonly IFeatureFlagMonitor _monitor;

        public FeatureFlagHealthCheck(IFeatureFlagMonitor monitor)
        {
            _monitor = monitor;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var report = await _monitor.GetHealthReportAsync(cancellationToken);
                
                var data = new Dictionary<string, object>
                {
                    ["totalFlags"] = report.TotalFlags,
                    ["enabledFlags"] = report.EnabledFlags,
                    ["expiredFlags"] = report.ExpiredFlags,
                    ["flagsWithoutExpiration"] = report.FlagsWithoutExpiration
                };

                if (!report.IsHealthy)
                {
                    var issues = new List<string>();
                    if (report.ExpiredFlags > 0)
                        issues.Add($"{report.ExpiredFlags} expired flags");
                    if (report.FlagsWithoutExpiration > 0)
                        issues.Add($"{report.FlagsWithoutExpiration} flags without expiration");

                    return HealthCheckResult.Degraded($"Issues found: {string.Join(", ", issues)}", data: data);
                }

                return HealthCheckResult.Healthy("All feature flags are healthy", data);
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Failed to check feature flag health", ex);
            }
        }
    }
}

// ===== MIGRATION UTILITIES =====

namespace FeatureFlags.Migration
{
    public interface IFeatureFlagMigration
    {
        Task<MigrationResult> MigrateFromLaunchDarklyAsync(string exportFilePath, CancellationToken cancellationToken = default);
        Task<MigrationResult> MigrateFromSplitIoAsync(string exportFilePath, CancellationToken cancellationToken = default);
        Task<MigrationResult> MigrateFromConfigurationAsync(IConfiguration configuration, CancellationToken cancellationToken = default);
    }

    public class FeatureFlagMigrationService : IFeatureFlagMigration
    {
        private readonly IFeatureFlagRepository _repository;
        private readonly ILogger<FeatureFlagMigrationService> _logger;

        public FeatureFlagMigrationService(IFeatureFlagRepository repository, ILogger<FeatureFlagMigrationService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<MigrationResult> MigrateFromLaunchDarklyAsync(string exportFilePath, CancellationToken cancellationToken = default)
        {
            var result = new MigrationResult();
            
            try
            {
                var json = await File.ReadAllTextAsync(exportFilePath, cancellationToken);
                var ldData = JsonSerializer.Deserialize<LaunchDarklyExport>(json);
                
                if (ldData?.Flags == null)
                {
                    result.Errors.Add("Invalid LaunchDarkly export format");
                    return result;
                }

                foreach (var ldFlag in ldData.Flags)
                {
                    try
                    {
                        var flag = new FeatureFlag
                        {
                            Key = ldFlag.Key,
                            Name = ldFlag.Name,
                            Description = ldFlag.Description ?? "",
                            Status = ldFlag.On ? FeatureFlagStatus.Enabled : FeatureFlagStatus.Disabled,
                            CreatedBy = "migration",
                            UpdatedBy = "migration",
                            Variations = new Dictionary<string, object>
                            {
                                ["on"] = true,
                                ["off"] = false
                            },
                            DefaultVariation = "off",
                            Tags = ldFlag.Tags?.ToDictionary(t => t, t => "migrated") ?? new(),
                            ExpirationDate = DateTime.UtcNow.AddMonths(6) // Set default expiration
                        };

                        await _repository.CreateAsync(flag, cancellationToken);
                        result.SuccessfulMigrations.Add(flag.Key);
                        
                        _logger.LogInformation("Migrated LaunchDarkly flag: {FlagKey}", flag.Key);
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Failed to migrate flag {ldFlag.Key}: {ex.Message}");
                        _logger.LogError(ex, "Failed to migrate LaunchDarkly flag: {FlagKey}", ldFlag.Key);
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to read LaunchDarkly export: {ex.Message}");
            }

            return result;
        }

        public async Task<MigrationResult> MigrateFromSplitIoAsync(string exportFilePath, CancellationToken cancellationToken = default)
        {
            // Similar implementation for Split.io
            var result = new MigrationResult();
            result.Errors.Add("Split.io migration not yet implemented");
            return result;
        }

        public async Task<MigrationResult> MigrateFromConfigurationAsync(IConfiguration configuration, CancellationToken cancellationToken = default)
        {
            var result = new MigrationResult();
            
            try
            {
                var featureFlagsSection = configuration.GetSection("FeatureFlags");
                
                foreach (var kvp in featureFlagsSection.AsEnumerable())
                {
                    if (string.IsNullOrEmpty(kvp.Key) || kvp.Key == "FeatureFlags") continue;
                    
                    var flagKey = kvp.Key.Replace("FeatureFlags:", "");
                    var isEnabled = kvp.Value?.ToLowerInvariant() == "true";
                    
                    try
                    {
                        var flag = new FeatureFlag
                        {
                            Key = flagKey,
                            Name = flagKey.Replace("-", " ").Replace("_", " "),
                            Description = $"Migrated from configuration",
                            Status = isEnabled ? FeatureFlagStatus.Enabled : FeatureFlagStatus.Disabled,
                            CreatedBy = "config-migration",
                            UpdatedBy = "config-migration",
                            ExpirationDate = DateTime.UtcNow.AddMonths(3),
                            Tags = new Dictionary<string, string> { ["source"] = "configuration" }
                        };

                        await _repository.CreateAsync(flag, cancellationToken);
                        result.SuccessfulMigrations.Add(flag.Key);
                        
                        _logger.LogInformation("Migrated configuration flag: {FlagKey}", flag.Key);
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Failed to migrate config flag {flagKey}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to read configuration: {ex.Message}");
            }

            return result;
        }
    }

    public class MigrationResult
    {
        public List<string> SuccessfulMigrations { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public bool IsSuccess => !Errors.Any();
    }

    public class LaunchDarklyExport
    {
        public List<LaunchDarklyFlag> Flags { get; set; } = new();
    }

    public class LaunchDarklyFlag
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool On { get; set; }
        public List<string>? Tags { get; set; }
    }
}

// ===== DOCKER AND DEPLOYMENT CONFIGURATIONS =====

/*
# Dockerfile for the Feature Flag Management API
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["FeatureFlags.Management/FeatureFlags.Management.csproj", "FeatureFlags.Management/"]
COPY ["FeatureFlags.Core/FeatureFlags.Core.csproj", "FeatureFlags.Core/"]
RUN dotnet restore "FeatureFlags.Management/FeatureFlags.Management.csproj"
COPY . .
WORKDIR "/src/FeatureFlags.Management"
RUN dotnet build "FeatureFlags.Management.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FeatureFlags.Management.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Install the CLI tool
RUN dotnet tool install --global FeatureFlags.CLI

ENTRYPOINT ["dotnet", "FeatureFlags.Management.dll"]
*/

/*
# docker-compose.yml for local development
version: '3.8'

services:
  feature-flags-api:
    build: .
    ports:
      - "5000:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=featureflags;Username=postgres;Password=password
      - FeatureFlags__StorageProvider=postgresql
      - FeatureFlags__RedisConnectionString=redis:6379
    depends_on:
      - postgres
      - redis

  postgres:
    image: postgres:15
    environment:
      - POSTGRES_DB=featureflags
      - POSTGRES_USER=postgres
      - POSTGRES_PASSWORD=password
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"

  feature-flags-ui:
    build:
      context: ./ui
      dockerfile: Dockerfile
    ports:
      - "3000:3000"
    environment:
      - REACT_APP_API_URL=http://localhost:5000
    depends_on:
      - feature-flags-api

volumes:
  postgres_data:
*/

/*
# Kubernetes deployment example
# k8s/deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: feature-flags-api
  namespace: feature-flags
spec:
  replicas: 3
  selector:
    matchLabels:
      app: feature-flags-api
  template:
    metadata:
      labels:
        app: feature-flags-api
    spec:
      containers:
      - name: api
        image: your-registry/feature-flags-api:latest
        ports:
        - containerPort: 80
        env:
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: feature-flags-secrets
              key: postgres-connection
        - name: FeatureFlags__RedisConnectionString
          valueFrom:
            secretKeyRef:
              name: feature-flags-secrets
              key: redis-connection
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 80
          initialDelaySeconds: 5
          periodSeconds: 5
*/