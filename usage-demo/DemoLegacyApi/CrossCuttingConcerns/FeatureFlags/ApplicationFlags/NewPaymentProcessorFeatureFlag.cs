using Propel.FeatureFlags.Domain;

namespace DemoLegacyApi.FeatureFlags.ApplicationFlags
{
	public class NewPaymentProcessorFeatureFlag : FeatureFlagBase
	{
		public NewPaymentProcessorFeatureFlag()
			: base(
				key: "new-payment-processor",
				name: "New Payment Processor",
				description: "Controls payment processor selection",
				onOfMode: EvaluationMode.Off)
		{
		}
	}
}