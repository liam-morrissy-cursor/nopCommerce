using AwesomeAssertions;
using Nop.Core.Domain.Directory;
using Nop.Services.Catalog;
using NUnit.Framework;

namespace Nop.Tests.Nop.Services.Tests.Catalog;

[TestFixture]
public class PriceCalculationServiceRoundParityTests : ServiceTest
{
    private static readonly (decimal Value, decimal Expected, RoundingType RoundingType)[] RoundCases =
    {
        (12.366M, 12.37M, RoundingType.Rounding001),
        (12.363M, 12.36M, RoundingType.Rounding001),
        (1.225M, 1.22M, RoundingType.Rounding001),
        (2.005M, 2.00M, RoundingType.Rounding001),
        (2.015M, 2.02M, RoundingType.Rounding001),
        (12.365M, 12.36M, RoundingType.Rounding001),
        (12.34M, 12.35M, RoundingType.Rounding005Up),
        (12.36M, 12.40M, RoundingType.Rounding005Up),
        (12.34M, 12.30M, RoundingType.Rounding005Down),
        (12.35M, 12.35M, RoundingType.Rounding005Down),
        (12.35M, 12.40M, RoundingType.Rounding01Up),
        (12.05M, 12.10M, RoundingType.Rounding01Up),
        (12.35M, 12.30M, RoundingType.Rounding01Down),
        (12.05M, 12.00M, RoundingType.Rounding01Down),
        (12.24M, 12.00M, RoundingType.Rounding05),
        (12.49M, 12.50M, RoundingType.Rounding05),
        (12.74M, 12.50M, RoundingType.Rounding05),
        (12.99M, 13.00M, RoundingType.Rounding05),
        (12.49M, 12.00M, RoundingType.Rounding1),
        (12.50M, 13.00M, RoundingType.Rounding1),
        (12.01M, 13.00M, RoundingType.Rounding1Up)
    };

    [Test]
    public void RoundMatchesBetweenCSharpBaselineAndRustSidecar()
    {
        var service = GetService<IPriceCalculationService>();
        var previousFlag = Environment.GetEnvironmentVariable(RoundSidecarSettings.UseRustRoundEnvironmentVariable);

        try
        {
            foreach (var (value, expected, roundingType) in RoundCases)
            {
                Environment.SetEnvironmentVariable(RoundSidecarSettings.UseRustRoundEnvironmentVariable, null);
                var csharp = service.Round(value, roundingType);

                Environment.SetEnvironmentVariable(RoundSidecarSettings.UseRustRoundEnvironmentVariable, "1");
                var rust = service.Round(value, roundingType);

                csharp.Should().Be(expected, $"C# baseline for {value} ({roundingType})");
                rust.Should().Be(expected, $"Rust sidecar for {value} ({roundingType})");
                rust.Should().Be(csharp, $"Rust/C# parity for {value} ({roundingType})");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(RoundSidecarSettings.UseRustRoundEnvironmentVariable, previousFlag);
        }
    }
}
