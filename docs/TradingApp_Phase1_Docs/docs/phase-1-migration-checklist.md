# Phase 1 Migration Checklist

## Safety First
- [ ] Copy the whole current solution to a new folder.
- [ ] Open the copied solution only.
- [ ] Build the solution before changing names.
- [ ] Commit the copied baseline to Git before refactoring.

## Step 1: Standardise Names
- [ ] Rename `Contracts` project to `TradingApp.Contracts`.
- [ ] Change namespace from `Contracts` to `TradingApp.Contracts`.
- [ ] Do not rename all services yet.

## Step 2: Clean Contracts
- [ ] Move old e-commerce messages to an `Archive` folder or delete after backup.
- [ ] Keep only trading messages for Phase 1:
  - [ ] `OrderAccepted`
  - [ ] `TradeCaptured`
  - [ ] `PositionUpdated`
  - [ ] `CaptureTrade` if using command style
  - [ ] `UpdatePosition` if using command style

## Step 3: Keep Existing API Working
- [ ] Keep `OrderService` compiling before renaming it.
- [ ] Later rename `OrderService` to `TradingGateway.Api`.
- [ ] Change request model from customer/amount to trader/symbol/side/quantity.

## Step 4: Add gRPC Contracts
- [ ] Create `TradingApp.GrpcContracts`.
- [ ] Add `pricing.proto`.
- [ ] Add `risk.proto`.

## Step 5: Add Services Gradually
- [ ] Add `PricingService.Grpc`.
- [ ] Add `RiskService.Grpc`.
- [ ] Add `TradeCapture.Service`.
- [ ] Add `Position.Service`.

## Step 6: GitHub
- [ ] Create `.gitignore`.
- [ ] Commit working baseline.
- [ ] Commit each refactor step separately.
