# Trading Platform Glossary

## Order
A client instruction to buy or sell an instrument. Example: Buy 1,000,000 EURUSD.

## Trade
The executed result of an accepted order. In Phase 1, we assume accepted market orders immediately become trades.

## Position
The net quantity held by a client or trader for a symbol. Example: +1,000,000 EURUSD.

## Symbol
The traded instrument identifier, such as EURUSD, GBPUSD, AAPL, or VOD.L.

## Side
Buy or Sell.

## Quantity
The amount being traded.

## Price
The execution price used for the trade.

## Notional
Quantity multiplied by price. Used for risk and exposure checks.

## Risk Check
A decision process that approves or rejects an order based on limits, client status, symbol rules, or exposure.

## Pricing Service
A service responsible for returning bid, ask, mid, and execution price.

## Gateway
The external entry point into the system. It accepts client requests and coordinates immediate checks.

## NServiceBus
A messaging framework used to send commands and publish events between services reliably.

## gRPC
A high-performance synchronous service-to-service communication mechanism.

## Correlation Id
An identifier passed through logs and messages to trace one business flow across services.
