using ApiFlagUsageDemo.FeatureFlags;
using Propel.FeatureFlags.Clients;

namespace ApiFlagUsageDemo.Services;

/// <summary>
///=== SERVICE LAYER FEATURE FLAG INTEGRATION DEMO ===
///
/// ✅ CORRECT USAGE - Service Layer Feature Flags:
/// • Progressive rollout of new service implementations
/// • A/B testing different technical approaches (algorithms, APIs, processors)
/// • Gradual migration from legacy to new systems
/// • Risk mitigation with automatic fallback mechanisms
/// • Performance testing of new integrations
/// • Canary releases of critical service components
///
/// ❌ INCORRECT USAGE:
/// • User payment plan restrictions (premium vs basic features)
/// • Geographic payment method limitations due to business rules
/// • Currency restrictions based on user subscription
/// • Payment limits based on user account type
/// • Fee structures or pricing logic
///
/// This service demonstrates the feature flag usage:
/// 1. Technical implementation switching (v1 vs v2 processors)
/// 2. Automatic fallback for resilience
/// 3. Rich context for intelligent targeting
/// 4. Same business outcome regardless of flag state
/// 5. Progressive rollout with risk mitigation
///
/// THE BUSINESS LOGIC REMAINS UNCHANGED:
/// All customers can process payments successfully regardless of which
/// technical implementation handles their request. The feature flag
/// controls HOW we process payments, not WHO can process payments.
/// </summary>

// ===== 1. SERVICE LAYER INTEGRATION =====
public class PaymentService(
	IApplicationFlagClient featureFlags,
	IPaymentProcessorV1 legacyProcessor,
	IPaymentProcessorV2 newProcessor,
	IFeatureFlagFactory flagFactory,
	ILogger<PaymentService> logger)
{
	// Service demonstrating feature flag call using IFeatureFlagClient instead of HttpContext
	public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request)
	{
		// IMPORTANT: This context is for TECHNICAL routing decisions,
		// not for restricting user access or applying business rules.
		var context = new Dictionary<string, object>
		{
			["amount"] = request.Amount,
			["currency"] = request.Currency,
			["country"] = request.BillingAddress.Country,
			["paymentMethod"] = request.PaymentMethod,
			["riskScore"] = await CalculateRiskScore(request)
		};

		// The flag might be configured to:
		// • Enable new processor for low-risk transactions first
		// • Route high-value payments through proven legacy system
		// • Use new processor in specific countries for testing
		// • Enable gradually based on customer risk profiles
		//

		var paymentProcessorFlag = flagFactory.GetFlagByType<NewPaymentProcessorFeatureFlag>();
		if (await featureFlags.IsEnabledAsync(
				paymentProcessorFlag,
				userId: request.CustomerId,
				attributes: context))
		{
			try
			{
				logger.LogInformation("Using new payment processor for customer {CustomerId}", request.CustomerId);
				return await newProcessor.ProcessAsync(request);
			}
			catch (Exception ex)
			{
				// This is a critical pattern for service reliability:
				// • If new implementation fails, automatically use proven system
				// • Ensures payment processing never completely fails
				// • Allows safe experimentation with new technologies
				// • Provides seamless user experience even during technical issues
				//
				// This fallback is TECHNICAL resilience, not business logic.
				// All users should be able to complete payments successfully.
				//
				logger.LogError(ex, "New payment processor failed, falling back to legacy for customer {CustomerId}", request.CustomerId);

				return await legacyProcessor.ProcessAsync(request);
			}
		}

		// Default to proven, stable implementation
		return await legacyProcessor.ProcessAsync(request);
	}

	private async Task<double> CalculateRiskScore(PaymentRequest request)
	{
		// Risk calculation logic for technical routing decisions
		// NOT for denying payments based on business rules
		return 0.5; // Placeholder
	}
}

//=== IMPLEMENTATION INTERFACES ===
//
// Both processors accomplish the same business goal (process payment)
// with different technical approaches. The business logic and user
// experience remain consistent regardless of implementation.
//

public interface IPaymentProcessorV1
{
	Task<PaymentResult> ProcessAsync(PaymentRequest request);
}

public class PaymentProcessorV1 : IPaymentProcessorV1
{
	public async Task<PaymentResult> ProcessAsync(PaymentRequest request)
	{
		// Legacy payment processing logic
		await Task.Delay(100); // Simulate async operation
		return new PaymentResult { Success = true, TransactionId = "legacy-12345" };
	}
}

public interface IPaymentProcessorV2
{
	Task<PaymentResult> ProcessAsync(PaymentRequest request);
}

public class PaymentProcessorV2 : IPaymentProcessorV2
{
	public async Task<PaymentResult> ProcessAsync(PaymentRequest request)
	{
		// New payment processing logic
		await Task.Delay(100); // Simulate async operation
		return new PaymentResult { Success = true, TransactionId = "new-12345" };
	}
}

public class PaymentRequest
{
	public string CustomerId { get; set; } = string.Empty;
	public decimal Amount { get; set; }
	public string Currency { get; set; } = "USD";
	public string PaymentMethod { get; set; } = "card";
	public Address BillingAddress { get; set; } = new();
}

public class Address
{
	public string Country { get; set; } = string.Empty;
}

public class PaymentResult
{
	public bool Success { get; set; }
	public string TransactionId { get; set; } = string.Empty;
}
