#!/usr/bin/env bash
# Run PriceCalculationServiceTests with the Rust round sidecar flag off and on.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FILTER="FullyQualifiedName~PriceCalculationServiceTests"
TEST_PROJECT="${REPO_ROOT}/src/Tests/Nop.Tests/Nop.Tests.csproj"
SIDECAR="${REPO_ROOT}/nop-round/target/release/nop-round-sidecar"

echo "==> Building nop-round sidecar"
cargo build --release --manifest-path "${REPO_ROOT}/nop-round/Cargo.toml"

export NOP_ROUND_SIDECAR_PATH="${SIDECAR}"

echo "==> Running PriceCalculationServiceTests with NOP_USE_RUST_ROUND unset (C# baseline)"
unset NOP_USE_RUST_ROUND
dotnet test "${TEST_PROJECT}" --filter "${FILTER}" --no-restore

echo "==> Running PriceCalculationServiceTests with NOP_USE_RUST_ROUND=1 (Rust sidecar)"
export NOP_USE_RUST_ROUND=1
dotnet test "${TEST_PROJECT}" --filter "${FILTER}" --no-restore

echo "==> Running round parity test"
dotnet test "${TEST_PROJECT}" --filter "FullyQualifiedName~PriceCalculationServiceRoundParityTests" --no-restore

echo "==> PriceCalculationService canary suite passed in both modes"
