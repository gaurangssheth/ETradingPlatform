# ADR-001: Phase 1 Service Boundaries

## Status
Accepted for Phase 1

## Context
The existing solution contains an NServiceBus learning project with `OrderService`, `BillingService`, `ShippingService`, `Contracts`, and `Shared.Validation`.

The new target is a realistic trading platform. However, rewriting everything at once would create unnecessary breakage.

## Decision
Phase 1 will use these service boundaries:

- `TradingGateway.Api` handles HTTP order submission.
- `PricingService.Grpc` owns pricing logic.
- `RiskService.Grpc` owns risk checks.
- `TradeCapture.Service` is a .NET Worker handling accepted orders asynchronously.
- `Position.Service` is a .NET Worker handling captured trades asynchronously.
- `TradingApp.Contracts` owns NServiceBus commands/events.
- `TradingApp.GrpcContracts` owns `.proto` files.

## Consequences
This separates synchronous request-response work from asynchronous post-acceptance workflow.

The gateway can fail fast if pricing/risk fails. After order acceptance, NServiceBus protects the workflow with retries and error queues.

## Interview Explanation
The API is not responsible for every step. It orchestrates only the immediate order acceptance decision. Longer-running workflow steps are moved to workers and connected through durable messages.
