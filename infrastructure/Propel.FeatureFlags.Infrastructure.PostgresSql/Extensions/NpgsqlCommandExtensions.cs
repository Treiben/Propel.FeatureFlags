using Npgsql;
using NpgsqlTypes;

namespace Propel.FeatureFlags.Infrastructure.PostgresSql.Extensions;

public static class NpgsqlCommandExtensions
{
	public static void AddFilterParameters(this NpgsqlCommand command, Dictionary<string, object> parameters)
	{
		foreach (var (key, value) in parameters)
		{
			if (key.StartsWith("tag") && !key.StartsWith("tagKey"))
			{
				// JSONB parameter for tag values
				var parameter = command.Parameters.Add(key, NpgsqlDbType.Jsonb);
				parameter.Value = value;
			}
			else if (key.StartsWith("mode"))
			{
				// JSONB parameter for evaluation modes
				var parameter = command.Parameters.Add(key, NpgsqlDbType.Jsonb);
				parameter.Value = value;
			}
			else
			{
				command.Parameters.AddWithValue(key, value);
			}
		}
	}
}
