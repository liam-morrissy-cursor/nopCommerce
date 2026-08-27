# Agent instructions

This is a financial-services customization of nopCommerce (ASP.NET Core shopping cart). Customer, payment, and order data are regulated. Prefer a plugin over a core edit.

## Defaults

- Sensitive data: never put PAN, CVV, full account numbers, secrets, or raw PII in logs, fixtures, screenshots, or PR text.
- Money: `decimal` only. Match existing rounding and `PaymentStatus` transitions.
- Tests: NUnit + AwesomeAssertions. Inherit `ServiceTest` or `WebTest`. Mirror production folders under `src/Tests/Nop.Tests/`.
- PRs: follow `.cursor/rules/open-a-pr.mdc`. Fill `.github/PULL_REQUEST_TEMPLATE.md`, then run `humanizer`. Test-only PRs use the Coverage section and stop there.

## Delegate

- Payment, customer, GDPR, auth, or money diffs → `finserv-reviewer`
- What to test (plan only) → `test-planner`
- New or expanded tests → `test-engineer`

Rules live in `.cursor/rules/`. Skills live in `.cursor/skills/`.
