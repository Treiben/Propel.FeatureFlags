using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Propel.FeatureFlags.Core;

public static class JsonDefaults
{
	public static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true
	};
}
