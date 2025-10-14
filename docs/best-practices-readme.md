# Design Philosophy & Best Practices

## Understanding Flag Types

**Application Flags** are owned by development teams. They control technical implementations like which algorithm to use, which API version to call, or whether to show a new UI component. These flags auto-create when first referenced, allowing developers to ship code immediately.

**Global Flags** are owned by operations teams. They control system-wide concerns like maintenance mode or emergency shutoffs. These must be explicitly created through admin tools because they affect the entire system.

This separation prevents the common problem where developers can't deploy because they're waiting for someone to create a flag in production.

## What Feature Flags Should Control

Feature flags work best for technical decisions: which code path to execute, which algorithm to use, which API to call. They're perfect for controlling the technical implementation while keeping the business logic the same.

For example, your checkout process might have three different technical implementations (v1, v2, v3), but the business logic remains the same: process the order and charge the customer. The flag controls which technical approach runs, not whether the user is allowed to purchase.

Feature flags should not control business rules like user permissions, subscription tiers, or access rights. Those belong in your business logic layer, not your deployment tooling.

## When to Use Each Evaluation Mode

**Simple toggles** work for kill switches and basic technical rollouts. If something breaks, you can turn it off immediately without redeploying.

**Scheduled flags** coordinate technical releases across teams. Marketing wants the new homepage to go live at exactly 9 AM Eastern? Schedule the technical implementation to activate at that moment.

**Time windows** handle features that need human support. That complex new admin interface should only be available when support staff are online to help users.

**User targeting** enables gradual technical rollouts. You can test new algorithms with 10% of users before exposing everyone to the change.

**Custom targeting** allows sophisticated technical A/B testing. Premium users might get the machine learning algorithm while regular users get the simpler collaborative filtering approach.

## The Default Disabled Strategy

New flags should start disabled for the same reason you wouldn't merge untested code to main: it's safer to explicitly enable something when you're ready than to accidentally release it during deployment.

Starting disabled means you can deploy your code, verify it works in production, then activate the feature when business conditions are right. This separation of deployment from activation is the core value proposition.

## Managing Flag Lifecycle

Flags are temporary by design. They solve a deployment timing problem, not a permanent architectural concern. Once the old code path is removed, the flag should be deleted too.

The library includes expiration dates and tagging to help identify stale flags. Type safety ensures all flag references are visible in your codebase, making cleanup easier. But ultimately, flag hygiene requires team discipline.

## Architecture Patterns

The middleware approach handles global concerns like maintenance mode and context extraction. This runs once per request and sets up the evaluation context for all subsequent flag checks.

Individual flag evaluations happen in your business logic using either direct method calls or the optional attribute-based approach. The direct approach is simpler and more explicit; attributes provide cleaner code at the cost of additional complexity.

Caching improves performance by avoiding repeated database calls, but the cache duration should balance freshness with speed. Five minutes is usually sufficient for most applications.

## Common Pitfalls

The biggest mistake is using feature flags for business logic instead of technical control. If your flag evaluation logic includes business rules like "if user.subscriptionTier == 'premium'", you're probably doing it wrong.

Another common issue is creating flags that never get removed. Every flag adds complexity to your codebase, so they should be temporary solutions to deployment timing problems, not permanent architecture.

Finally, avoid the temptation to use feature flags as a configuration system. They're deployment tools, not application settings. Use proper configuration management for things like API endpoints, timeout values, or database connections.

## Performance and Reliability

Flag evaluation should be fast and reliable since it happens in your critical path. The library prioritizes simple evaluation logic and efficient caching over complex targeting rules that might slow down requests.

If flag evaluation fails, the system defaults to safe behavior rather than throwing exceptions. This ensures a flag service outage doesn't bring down your entire application.

Monitoring flag evaluation metrics helps identify problems before they affect users. Track evaluation latency, cache hit rates, and error frequencies to maintain system health.

## The Organizational Benefit

The ultimate goal is organizational efficiency. Developers ship features faster because they don't wait for business decisions. Product owners activate features more confidently because they control the timing. Both teams work in parallel instead of sequentially, reducing cycle time and improving predictability.

This separation also improves incident response. If a feature causes problems, product owners can disable it immediately without waiting for a development team to deploy a fix. The technical implementation stays deployed, but users don't see it until the issue is resolved.