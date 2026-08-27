---
name: write-nopcommerce-tests
description: >-
  Adds NUnit tests that match this repo's ServiceTest/WebTest layout,
  AwesomeAssertions usage, and plugin test doubles. Use when writing tests,
  expanding coverage, or adding an API endpoint.
---

# Write nopCommerce tests

## Layout

| Production | Test |
|---|---|
| `src/Libraries/Nop.Services/{Area}/` | `src/Tests/Nop.Tests/Nop.Services.Tests/{Area}/` |
| `src/Presentation/Nop.Web/` validators, factories | `src/Tests/Nop.Tests/Nop.Web.Tests/Public/` or `Admin/` |
| Plugin under `src/Plugins/` | Prefer tests next to the behavior they lock, in `Nop.Tests` if the plugin is loaded in `ServiceTest.InitPlugins` |

Namespace: `Nop.Tests.Nop.Services.Tests.{Area}` (or `Nop.Web.Tests…`). Class: `{Type}Tests`.

## Bases

- Services, payments, orders: `ServiceTest` (`GetService<T>()`, test plugins already registered).
- Web factories/validators: `WebTest` or `BaseNopTest`.
- CRUD-shaped services: `ServiceTest<TEntity>` and `CrudData`.

## Style

- NUnit `[TestFixture]` / `[Test]` / `[OneTimeSetUp]`.
- Assertions: AwesomeAssertions (`Should().BeTrue()`), FluentValidation.TestHelper for validators.
- Name like neighbors: `CanLoadPaymentMethods`, `EnsureOrderCanOnlyBeCancelled…`, `ShouldHaveErrorWhenEmailIsNullOrEmpty`.
- Payment: `TestPaymentMethod` (`Payments.TestMethod`). Toggle `TestSupportRefund` / `TestSupportCapture` / `TestSupportVoid` in setup/teardown. Never use real card data.
- New endpoints: extend the existing fixture for that controller/service. Do not add an untested action.

## Run

```bash
dotnet test src/Tests/Nop.Tests/Nop.Tests.csproj --filter FullyQualifiedName~OrderProcessingServiceTests
```

CI is `dotnet test` on `src` after a Release build (see `.github/workflows/dotnet.yml`).
