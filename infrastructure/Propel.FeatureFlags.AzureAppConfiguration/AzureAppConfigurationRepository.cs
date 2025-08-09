using Azure;
using Azure.Data.AppConfiguration;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Core;
using Propel.FeatureFlags.Persistence;
using System.Text.Json;

namespace Propel.FeatureFlags.AzureAppConfiguration;

public class AzureAppConfigurationRepository : IFeatureFlagRepository
{
	private readonly ConfigurationClient _client;
	private readonly ILogger<AzureAppConfigurationRepository> _logger;
	private const string FEATURE_FLAG_PREFIX = "FeatureFlags:";

	public AzureAppConfigurationRepository(string connectionString, ILogger<AzureAppConfigurationRepository> logger)
	{
		_client = new ConfigurationClient(connectionString);
		_logger = logger;
	}

	public async Task<FeatureFlag?> GetAsync(string key, CancellationToken cancellationToken = default)
	{
		try
		{
			var response = await _client.GetConfigurationSettingAsync($"{FEATURE_FLAG_PREFIX}{key}", cancellationToken: cancellationToken);
			if (response?.Value?.Value == null)
				return null;

			return JsonSerializer.Deserialize<FeatureFlag>(response.Value.Value);
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
			return null;
		}
	}

	public async Task<List<FeatureFlag>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		var flags = new List<FeatureFlag>();

		await foreach (var setting in _client.GetConfigurationSettingsAsync(
			new SettingSelector { KeyFilter = $"{FEATURE_FLAG_PREFIX}*" },
			cancellationToken))
		{
			if (setting.Value != null)
			{
				var flag = JsonSerializer.Deserialize<FeatureFlag>(setting.Value);
				if (flag != null)
					flags.Add(flag);
			}
		}

		return flags.OrderBy(f => f.Name).ToList();
	}

	public async Task<FeatureFlag> CreateAsync(FeatureFlag flag, CancellationToken cancellationToken = default)
	{
		var setting = new ConfigurationSetting($"{FEATURE_FLAG_PREFIX}{flag.Key}", JsonSerializer.Serialize(flag))
		{
			ContentType = "application/json"
		};

		await _client.SetConfigurationSettingAsync(setting: setting, cancellationToken: cancellationToken);
		return flag;
	}

	public async Task<FeatureFlag> UpdateAsync(FeatureFlag flag, CancellationToken cancellationToken = default)
	{
		flag.UpdatedAt = DateTime.UtcNow;
		return await CreateAsync(flag, cancellationToken); // Azure App Config handles upserts
	}

	public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
	{
		try
		{
			await _client.DeleteConfigurationSettingAsync($"{FEATURE_FLAG_PREFIX}{key}", cancellationToken: cancellationToken);
			return true;
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
			return false;
		}
	}

	public async Task<List<FeatureFlag>> GetExpiringAsync(DateTime before, CancellationToken cancellationToken = default)
	{
		var allFlags = await GetAllAsync(cancellationToken);
		return allFlags.Where(f => f.ExpirationDate.HasValue && f.ExpirationDate <= before && !f.IsPermanent).ToList();
	}

	public async Task<List<FeatureFlag>> GetByTagsAsync(Dictionary<string, string> tags, CancellationToken cancellationToken = default)
	{
		var allFlags = await GetAllAsync(cancellationToken);
		return allFlags.Where(f => tags.All(tag => f.Tags.ContainsKey(tag.Key) && f.Tags[tag.Key] == tag.Value)).ToList();
	}
}
