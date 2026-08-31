using System.Globalization;
using Newtonsoft.Json;

namespace Nop.Tests.Fixtures;

public static class GetFinalPriceFixtures
{
    public const string FileName = "GetFinalPriceAsync.json";

    public static GetFinalPriceDocument Load()
    {
        var path = ResolvePath();
        var json = File.ReadAllText(path);
        var document = JsonConvert.DeserializeObject<GetFinalPriceDocument>(json)
            ?? throw new InvalidOperationException($"Failed to deserialize {path}");

        if (document.Cases is not { Count: > 0 })
            throw new InvalidOperationException($"{path} has no cases");

        if (document.AttributeAdjustments is not { Count: > 0 })
            throw new InvalidOperationException($"{path} has no attributeAdjustments");

        return document;
    }

    public static string ResolvePath()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            foreach (var candidate in CandidatePaths(dir.FullName))
            {
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        throw new FileNotFoundException(
            $"Could not find {FileName} walking up from {AppContext.BaseDirectory}");
    }

    private static IEnumerable<string> CandidatePaths(string root)
    {
        yield return Path.Combine(root, "Fixtures", FileName);
        yield return Path.Combine(root, "src", "Tests", "Nop.Tests", "Fixtures", FileName);
    }
}

public sealed class GetFinalPriceDocument
{
    public IList<GetFinalPriceCase> Cases { get; set; } = new List<GetFinalPriceCase>();

    public IList<RoleTierInput> RoleTierInputs { get; set; } = new List<RoleTierInput>();

    public IList<AttributeAdjustmentCase> AttributeAdjustments { get; set; } = new List<AttributeAdjustmentCase>();

    public GetFinalPriceCase Get(string id)
    {
        var match = Cases.SingleOrDefault(c => c.Id == id);
        if (match is null)
            throw new InvalidOperationException($"Missing GetFinalPriceAsync fixture case '{id}'");

        return match;
    }

    public AttributeAdjustmentCase GetAttributeAdjustment(string id)
    {
        var match = AttributeAdjustments.SingleOrDefault(c => c.Id == id);
        if (match is null)
            throw new InvalidOperationException($"Missing attribute-adjustment fixture '{id}'");

        return match;
    }
}

public sealed class GetFinalPriceCase
{
    public string Id { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string Setup { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string AdditionalCharge { get; set; } = "0";

    public bool IncludeDiscounts { get; set; }

    public string CouponCode { get; set; }

    public int? DiscountId { get; set; }

    public string FinalPrice { get; set; } = string.Empty;

    public string PriceWithoutDiscounts { get; set; } = string.Empty;

    public decimal FinalPriceValue => ParseMoney(FinalPrice);

    public decimal PriceWithoutDiscountsValue => ParseMoney(PriceWithoutDiscounts);

    public decimal AdditionalChargeValue => ParseMoney(AdditionalCharge);

    private static decimal ParseMoney(string value) =>
        decimal.Parse(value, CultureInfo.InvariantCulture);
}

public sealed class RoleTierInput
{
    public int Quantity { get; set; }

    public string Price { get; set; } = string.Empty;

    public decimal PriceValue => decimal.Parse(Price, CultureInfo.InvariantCulture);
}

public sealed class AttributeAdjustmentCase
{
    public string Id { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string ProductPrice { get; set; } = string.Empty;

    public string PriceAdjustment { get; set; } = string.Empty;

    public bool UsePercentage { get; set; }

    public string Adjustment { get; set; } = string.Empty;

    public decimal ProductPriceValue => ParseMoney(ProductPrice);

    public decimal PriceAdjustmentValue => ParseMoney(PriceAdjustment);

    public decimal AdjustmentValue => ParseMoney(Adjustment);

    private static decimal ParseMoney(string value) =>
        decimal.Parse(value, CultureInfo.InvariantCulture);
}
