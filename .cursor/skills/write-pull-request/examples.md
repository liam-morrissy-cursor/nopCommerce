# Coverage section examples

## Tight (use this shape)

    ## What changed
    - Tests for `CanCancelOrder`, `CanRefund`, and `CanVoid` across `OrderStatus` × `PaymentStatus`.

    ## Why
    Those guards were untested. A bad status mix could cancel or refund an order the processing service is supposed to reject.

    ## How to verify
    `dotnet test src/Tests/Nop.Tests/Nop.Tests.csproj --filter FullyQualifiedName~OrderProcessingServiceTests`
    Expect the new `EnsureOrderCanOnlyBe*` cases to pass.

    ## Coverage
    Gap: `IOrderProcessingService` status guards had no matrix coverage, so a regression in `CanRefund` would not fail CI.

    Cases:
    - `CanCancelOrder` when already `Cancelled` → false; any other `OrderStatus` → true
    - `CanRefund` only when `PaymentStatus` is `Paid` and not `Cancelled`
    - `CanVoid` only when `Authorized` and not `Cancelled`

    Still open: partial refund vs capture combinations (existing tests already cover those).

    Run: filter above, or `dotnet test src` as in CI.

## Bloated (do not do this)

    ## Summary
    This PR represents a comprehensive effort to enhance our robust test coverage,
    ensuring reliability across the order-processing landscape.

    ## Key takeaways
    - **Cancel:** We added tests highlighting that cancel is blocked when cancelled.
    - **Refund:** We added tests showcasing refund eligibility.
    - **Void:** We also added void tests, underscoring the importance of status checks.

    Additionally, we refactored fixtures and aligned with best practices. Future
    work could potentially further enhance coverage.

That version is long, restates the diff, and trips humanizer (puffery, AI vocab, bold labels, triad padding).
