# ADR-002: Should OrderService Be a Worker?

## Status
Accepted for Phase 1

## Decision
`TradingGateway.Api` should be a Web API, not a Worker.

`TradeCapture.Service` and `Position.Service` should be .NET Workers.

## Reason
The order entry/gateway service needs to expose HTTP endpoints for clients, Swagger, later Angular/React, and possibly authentication.

Worker services are better for background message handling, such as trade capture and position updates.

## Rule
Use Web API when an external client calls the service directly.

Use Worker Service when the service mainly consumes messages and performs background processing.
