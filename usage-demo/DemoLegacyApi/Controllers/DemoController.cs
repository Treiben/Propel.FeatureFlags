using DemoLegacyApi.FeatureFlags;
using Propel.FeatureFlags.Domain;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;

namespace DemoLegacyApi.Controllers
{
	/// <summary>
	/// Demo controller for .NET Framework 4.8.1 showcasing Propel Feature Flags
	/// without dependency injection or database persistence.
	/// 
	/// This demonstrates in-memory flag evaluation using the proper client abstraction.
	/// </summary>
	public class DemoController : ApiController
	{
		// Singleton instances (simulating what DI would provide)
		private static readonly FeatureFlagService _flagService = new FeatureFlagService();


		//=================================================================================
		// DEMO 1: Simple On/Off Feature Flag (Product API Versioning)
		//=================================================================================

		/// <summary>
		/// GET api/featureflags/products
		/// Demonstrates simple on/off feature flag for API versioning
		/// </summary>
		[HttpGet]
		[Route("api/featureflags/products")]
		public async Task<IHttpActionResult> GetProducts(string userId = null)
		{
			if (await _flagService.IsEnabledAsync<NewProductApiFeatureFlag>(userId: userId))
			{
				return Ok(new
				{
					version = "v2",
					products = new[] { "Product A", "Product B", "Product C", "Product D" },
					message = "✅ Using NEW Product API (Enhanced)"
				});
			}

			return Ok(new
			{
				version = "v1",
				products = new[] { "Product A", "Product B" },
				message = "⚠️ Using LEGACY Product API"
			});
		}

		//=================================================================================
		// DEMO 2: User Percentage Rollout (Recommendation Algorithm)
		//=================================================================================

		/// <summary>
		/// GET api/featureflags/recommendations/{userId}
		/// Demonstrates percentage-based rollout for gradual feature deployment
		/// </summary>
		[HttpGet]
		[Route("api/featureflags/recommendations/{userId}")]
		public async Task<IHttpActionResult> GetRecommendations(string userId)
		{
			var flag = new RecommendationAlgorithmFeatureFlag();
			if (await _flagService.IsEnabledAsync(flag, userId: userId))
			{
				return Ok(new
				{
					algorithm = "machine-learning",
					recommendations = new[] { "ML Product 1", "ML Product 2", "ML Product 3" },
					message = $"✅ Using ML Algorithm for user {userId}",
					rolloutPercentage = "50%"
				});
			}

			return Ok(new
			{
				algorithm = "collaborative-filtering",
				recommendations = new[] { "Popular Product 1", "Popular Product 2" },
				message = $"⚠️ Using Legacy Algorithm for user {userId}",
				rolloutPercentage = "50%"
			});
		}

		//=================================================================================
		// DEMO 3: Admin Panel Access (On/Off Toggle)
		//=================================================================================

		/// <summary>
		/// GET api/featureflags/admin/sensitive-operation
		/// Demonstrates access control using feature flags
		/// </summary>
		[HttpGet]
		[Route("api/featureflags/admin/sensitive-operation")]
		public async Task<IHttpActionResult> AdminSensitiveOperation(string userId = null)
		{
			if (await _flagService.IsEnabledAsync<AdminPanelEnabledFeatureFlag>(userId: userId))
			{
				return Ok(new
				{
					status = "success",
					message = "✅ Sensitive operation completed",
					operation = "Admin panel access granted"
				});
			}

			return NotFound(); // Feature not enabled
		}

		//=================================================================================
		// DEMO 4: Payment Processor Switch (Service Layer Integration)
		//=================================================================================

		/// <summary>
		/// POST api/featureflags/payments/process
		/// Demonstrates service layer feature flag integration with fallback
		/// </summary>
		[HttpPost]
		[Route("api/featureflags/payments/process")]
		public async Task<IHttpActionResult> ProcessPayment([FromBody] PaymentRequest request)
		{
			if (request == null)
				return BadRequest("Payment request is required");

			var attributes = new Dictionary<string, object>
			{
				["amount"] = request.Amount,
				["currency"] = request.Currency
			};

			if (await _flagService.IsEnabledAsync<NewPaymentProcessorFeatureFlag>(userId: request.CustomerId, attributes: attributes))
			{
				// New payment processor
				return Ok(new
				{
					success = true,
					transactionId = $"new-{Guid.NewGuid():N}",
					processor = "v2",
					message = "✅ Payment processed using NEW processor"
				});
			}

			// Legacy payment processor (fallback)
			return Ok(new
			{
				success = true,
				transactionId = $"legacy-{Guid.NewGuid():N}",
				processor = "v1",
				message = "⚠️ Payment processed using LEGACY processor"
			});
		}

		//=================================================================================
		// DEMO 5: Email Service with Feature Toggle
		//=================================================================================

		/// <summary>
		/// POST api/featureflags/notifications/send-email
		/// Demonstrates notification service switching based on feature flag
		/// </summary>
		[HttpPost]
		[Route("api/featureflags/notifications/send-email")]
		public async Task<IHttpActionResult> SendEmail([FromBody] EmailRequest request)
		{
			if (request == null)
				return BadRequest("Email request is required");

			if (await _flagService.IsEnabledAsync(flagKey: "new-payment-processor"))
			{
				// New email service
				await Task.Delay(100); // Simulate async operation
				return Ok(new
				{
					success = true,
					messageId = Guid.NewGuid().ToString(),
					service = "SendGrid",
					message = "✅ Email sent using NEW email service"
				});
			}

			// Legacy email service (fallback)
			await Task.Delay(200); // Simulate async operation
			return Ok(new
			{
				success = true,
				messageId = Guid.NewGuid().ToString(),
				service = "SMTP",
				message = "⚠️ Email sent using LEGACY email service"
			});
		}

		//=================================================================================
		// DEMO 6: Get Flag Status (Diagnostic Endpoint)
		//=================================================================================

		/// <summary>
		/// GET api/featureflags/status
		/// Returns the status of all configured feature flags
		/// </summary>
		[HttpGet]
		[Route("api/featureflags/status")]
		public async Task<IHttpActionResult> GetFlagsStatus()
		{
			// Evaluate without user or attributes - in real scenarios, you would likely evaluation options
			// such as user ID or tenant ID or attributes for targeting and variation evaluation

			var flags = _flagService.GetAllFlags();
			var statusList = new List<object>();

			foreach (var flag in flags)
			{
				var result = await _flagService.EvaluateAsync(flag);
				
				statusList.Add(new
				{
					key = flag.Key,
					name = flag.Name,
					description = flag.Description,
					isEnabled = result?.IsEnabled ?? false,
					reason = result?.Reason ?? "N/A"
				});
			}

			return Ok(new
			{
				totalFlags = statusList.Count,
				flags = statusList
			});
		}

		private IFeatureFlag CreateFlagInstance(string key)
		{
			return key switch
			{
				"new-product-api" => new NewProductApiFeatureFlag(),
				"recommendation-algorithm" => new RecommendationAlgorithmFeatureFlag(),
				"admin-panel-enabled" => new AdminPanelEnabledFeatureFlag(),
				"new-payment-processor" => new NewPaymentProcessorFeatureFlag(),
				"new-email-service" => new NewEmailServiceFeatureFlag(),
				_ => new GenericFeatureFlag(key)
			};
		}
	}

	//=================================================================================
	// Request DTOs
	//=================================================================================

	public class PaymentRequest
	{
		public string CustomerId { get; set; }
		public decimal Amount { get; set; }
		public string Currency { get; set; } = "USD";
	}

	public class EmailRequest
	{
		public string UserId { get; set; }
		public string Subject { get; set; }
		public string Body { get; set; }
	}
}