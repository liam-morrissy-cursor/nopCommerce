using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using Moq;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Directory;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using NUnit.Framework;

namespace Nop.Tests.Nop.Services.Tests.Catalog;

[TestFixture]
public class PriceRoundingFallbackTests
{
    [Test]
    public void FlagOff_MatchesOracle()
    {
        new CatalogSettings().UseRustPriceRounding.Should().BeFalse();
        AssertOracle(CreateService(new CatalogSettings()));
    }

    [Test]
    public void FlagOn_LibraryMissing_MatchesOracle()
    {
        var library = Path.Combine(AppContext.BaseDirectory, NativeLibraryFileName());
        File.Exists(library).Should().BeFalse(
            "this case is the flag-on path with no cdylib next to the test host");

        var settings = new CatalogSettings { UseRustPriceRounding = true };
        AssertOracle(CreateService(settings));
    }

    [Test]
    public void NullCatalogSettings_MatchesOracle()
    {
        AssertOracle(CreateService(catalogSettings: null));
    }

    [Test]
    public async Task RoundPriceAsync_LoadsCurrency_WhenFlagIsOn()
    {
        const int currencyId = 7;
        var currency = new Currency
        {
            Id = currencyId,
            RoundingType = RoundingType.Rounding001
        };
        var currencyService = new Mock<ICurrencyService>();
        currencyService
            .Setup(service => service.GetCurrencyByIdAsync(currencyId))
            .ReturnsAsync(currency);

        var service = CreateService(
            new CatalogSettings { UseRustPriceRounding = true },
            new CurrencySettings { PrimaryStoreCurrencyId = currencyId },
            currencyService.Object);

        var actual = await service.RoundPriceAsync(12.366m);

        actual.Should().Be(12.37m);
        currencyService.Verify(service => service.GetCurrencyByIdAsync(currencyId), Times.Once);
    }

    private static void AssertOracle(PriceCalculationService service)
    {
        var rows = LoadOracleRows();
        rows.Should().NotBeEmpty();

        foreach (var row in rows)
        {
            var roundingType = Enum.Parse<RoundingType>(row.RoundingType);
            var value = decimal.Parse(row.Value, CultureInfo.InvariantCulture);
            var expected = decimal.Parse(row.Expected, CultureInfo.InvariantCulture);

            service.Round(value, roundingType).Should().Be(expected, $"{row.Value} / {row.RoundingType}");
        }
    }

    private static PriceCalculationService CreateService(
        CatalogSettings catalogSettings,
        CurrencySettings currencySettings = null,
        ICurrencyService currencyService = null)
    {
        return new PriceCalculationService(
            catalogSettings,
            currencySettings,
            categoryService: null,
            currencyService: currencyService,
            customerService: null,
            discountService: null,
            manufacturerService: null,
            priceListService: null,
            productAttributeParser: null,
            productService: null,
            staticCacheManager: null);
    }

    private static string NativeLibraryFileName()
    {
        if (OperatingSystem.IsWindows())
            return "price_rounding.dll";
        if (OperatingSystem.IsMacOS())
            return "libprice_rounding.dylib";
        return "libprice_rounding.so";
    }

    private static List<OracleRow> LoadOracleRows([CallerFilePath] string sourcePath = "")
    {
        var directory = Path.GetDirectoryName(sourcePath);
        if (string.IsNullOrEmpty(directory))
            throw new InvalidOperationException("Cannot locate price-rounding-oracle.json.");

        var path = Path.Combine(directory, "price-rounding-oracle.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Rounding golden file was not found at {path}.", path);

        var rows = JsonSerializer.Deserialize<List<OracleRow>>(File.ReadAllText(path));
        if (rows == null)
            throw new InvalidOperationException($"No rows in {path}.");

        return rows;
    }

    private sealed class OracleRow
    {
        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("roundingType")]
        public string RoundingType { get; set; }

        [JsonPropertyName("expected")]
        public string Expected { get; set; }
    }
}
