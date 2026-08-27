---
name: extend-via-plugin
description: >-
  Adds or extends a nopCommerce plugin instead of forking core libraries.
  Use when building a payment method, tax/shipping provider, widget, or
  org-specific integration.
---

# Extend via plugin

Default: new org behavior lives in `src/Plugins/Nop.Plugin.{Group}.{Name}/`. Edit `src/Libraries/` only when every store needs the change.

## Scaffold

1. Copy a close sibling (payments: `Nop.Plugin.Payments.CheckMoneyOrder`).
2. Rename the project, namespace, `plugin.json` `SystemName` + `FileName`, and the csproj. Add the project to `src/NopCommerce.sln`.
3. Payment methods: `BasePlugin` + `IPaymentMethod`. Other groups: the matching `ITaxProvider` / `IShippingRateComputationMethod` / `IWidgetPlugin` / etc.
4. Credentials and endpoints go in a `*Settings` class loaded through `ISettingService`, not source.
5. `InstallAsync` / `UninstallAsync`: locale resources and settings. Uninstall must leave no leftovers.

## Constraints

- Do not collide `SystemName` with an existing plugin.
- Gateway I/O stays in the plugin. Totals, capture, refund, and void still go through `IOrderProcessingService`.
- No PAN/CVV in logs or settings dumps. Tokenize at the gateway.
- Add tests under `src/Tests` and register a test double in `ServiceTest.InitPlugins` if load-by-system-name is part of the contract.

See `plugin-conventions` and `finserv-sensitive-data` rules.
