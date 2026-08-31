using AwesomeAssertions;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Stores;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Tests.Fixtures;
using NUnit.Framework;

namespace Nop.Tests.Nop.Services.Tests.Catalog;

[TestFixture]
public class PriceCalculationServiceTests : ServiceTest
{
    #region Fields

    private ICustomerService _customerService;
    private IProductService _productService;
    private IPriceCalculationService _priceCalcService;
    private GetFinalPriceDocument _finalPriceFixtures;

    #endregion

    #region SetUp

    [OneTimeSetUp]
    public void SetUp()
    {
        _customerService = GetService<ICustomerService>();
        _productService = GetService<IProductService>();
        _priceCalcService = GetService<IPriceCalculationService>();
        _finalPriceFixtures = GetFinalPriceFixtures.Load();
    }

    #endregion

    #region Tests

    [Test]
    public async Task CanGetFinalProductPrice()
    {
        var product = await _productService.GetProductBySkuAsync("BP_20_WSP");

        var customer = new Customer();
        var store = new Store();

        var (finalPriceWithoutDiscounts, finalPrice, _, _) = await _priceCalcService.GetFinalPriceAsync(product, customer, store, 0, false);
        AssertMatchesFixture(_finalPriceFixtures.Get("base"), finalPriceWithoutDiscounts, finalPrice);

        (finalPriceWithoutDiscounts, finalPrice, _, _) = await _priceCalcService.GetFinalPriceAsync(product, customer, store, 0, false, 2);
        AssertMatchesFixture(_finalPriceFixtures.Get("qty-2"), finalPriceWithoutDiscounts, finalPrice);
    }

    [Test]
    public async Task CanGetFinalProductPriceWithTierPrices()
    {
        var product = await _productService.GetProductBySkuAsync("BP_20_WSP");

        var customer = new Customer();
        var store = new Store();

        foreach (var id in new[] { "base", "qty-2", "qty-3", "qty-5", "qty-7" })
        {
            var expected = _finalPriceFixtures.Get(id);
            var (finalPriceWithoutDiscounts, finalPrice, _, _) = await _priceCalcService.GetFinalPriceAsync(
                product, customer, store, expected.AdditionalChargeValue, expected.IncludeDiscounts, expected.Quantity);
            AssertMatchesFixture(expected, finalPriceWithoutDiscounts, finalPrice);
        }
    }

    [Test]
    public async Task CanGetFinalProductPriceWithTierPricesByCustomerRole()
    {
        var product = await _productService.GetProductBySkuAsync("NK_ZSJ_MM");

        //customer
        var customer = await _customerService.GetCustomerByEmailAsync(NopTestsDefaults.AdminEmail);
        var store = new Store();

        var roles = await _customerService.GetAllCustomerRolesAsync();
        var customerRole = roles.FirstOrDefault();

        customerRole.Should().NotBeNull();

        var tierPrices = _finalPriceFixtures.RoleTierInputs
            .Select(tier => new TierPrice
            {
                CustomerRoleId = customerRole.Id,
                ProductId = product.Id,
                Quantity = tier.Quantity,
                Price = tier.PriceValue
            })
            .ToList();

        foreach (var tierPrice in tierPrices)
            await _productService.InsertTierPriceAsync(tierPrice);

        var roleCases = _finalPriceFixtures.Cases
            .Where(c => c.Setup == "customerRoleTiers")
            .ToList();

        var results = new List<(GetFinalPriceCase Expected, decimal PriceWithoutDiscounts, decimal FinalPrice)>();
        foreach (var expected in roleCases)
        {
            var (priceWithoutDiscounts, finalPrice, _, _) = await _priceCalcService.GetFinalPriceAsync(
                product, customer, store, expected.AdditionalChargeValue, expected.IncludeDiscounts, expected.Quantity);
            results.Add((expected, priceWithoutDiscounts, finalPrice));
        }

        foreach (var tierPrice in tierPrices)
            await _productService.DeleteTierPriceAsync(tierPrice);

        foreach (var (expected, priceWithoutDiscounts, finalPrice) in results)
            AssertMatchesFixture(expected, priceWithoutDiscounts, finalPrice);
    }

    [Test]
    public async Task CanGetFinalProductPriceWithAdditionalFee()
    {
        var product = await _productService.GetProductBySkuAsync("BP_20_WSP");

        //customer
        var customer = new Customer();
        var store = new Store();

        var expected = _finalPriceFixtures.Get("additional-fee");
        var (finalPriceWithoutDiscounts, finalPrice, _, _) = await _priceCalcService.GetFinalPriceAsync(
            product, customer, store, expected.AdditionalChargeValue, expected.IncludeDiscounts);

        AssertMatchesFixture(expected, finalPriceWithoutDiscounts, finalPrice);
    }

    [Test]
    public async Task CanGetFinalProductPriceWithDiscount()
    {
        var expected = _finalPriceFixtures.Get("discount");
        var product = await _productService.GetProductBySkuAsync(expected.Sku);
        var customer = await _customerService.GetCustomerByEmailAsync(NopTestsDefaults.AdminEmail);
        var store = new Store();

        var mapping = new DiscountProductMapping
        {
            DiscountId = expected.DiscountId ?? 1,
            EntityId = product.Id
        };

        await _productService.InsertDiscountProductMappingAsync(mapping);
        await _customerService.ApplyDiscountCouponCodeAsync(customer, expected.CouponCode);

        var (finalPriceWithoutDiscounts, finalPrice, _, _) = await _priceCalcService.GetFinalPriceAsync(product, customer, store);

        await _productService.DeleteDiscountProductMappingAsync(mapping);
        await _customerService.RemoveDiscountCouponCodeAsync(customer, expected.CouponCode);

        AssertMatchesFixture(expected, finalPriceWithoutDiscounts, finalPrice);
    }

    [Test]
    public async Task CanGetPercentageAttributeAdjustmentUsingFloatCast()
    {
        var expected = _finalPriceFixtures.GetAttributeAdjustment("percent-float-bp-20-wsp");
        var product = await _productService.GetProductBySkuAsync(expected.Sku);
        var customer = new Customer();
        var store = new Store();

        var value = new ProductAttributeValue
        {
            AttributeValueType = AttributeValueType.Simple,
            PriceAdjustmentUsePercentage = expected.UsePercentage,
            PriceAdjustment = expected.PriceAdjustmentValue
        };

        var adjustment = await _priceCalcService.GetProductAttributeValuePriceAdjustmentAsync(
            product, value, customer, store, expected.ProductPriceValue);

        adjustment.Should().Be(expected.AdjustmentValue);
    }

    [TestCase(12.366, 12.37, RoundingType.Rounding001)]
    [TestCase(12.363, 12.36, RoundingType.Rounding001)]
    [TestCase(12.000, 12.00, RoundingType.Rounding001)]
    [TestCase(12.001, 12.00, RoundingType.Rounding001)]
    [TestCase(12.34, 12.35, RoundingType.Rounding005Up)]
    [TestCase(12.36, 12.40, RoundingType.Rounding005Up)]
    [TestCase(12.35, 12.35, RoundingType.Rounding005Up)]
    [TestCase(12.00, 12.00, RoundingType.Rounding005Up)]
    [TestCase(12.05, 12.05, RoundingType.Rounding005Up)]
    [TestCase(12.20, 12.20, RoundingType.Rounding005Up)]
    [TestCase(12.001, 12.00, RoundingType.Rounding005Up)]
    [TestCase(12.34, 12.30, RoundingType.Rounding005Down)]
    [TestCase(12.36, 12.35, RoundingType.Rounding005Down)]
    [TestCase(12.00, 12.00, RoundingType.Rounding005Down)]
    [TestCase(12.05, 12.05, RoundingType.Rounding005Down)]
    [TestCase(12.20, 12.20, RoundingType.Rounding005Down)]
    [TestCase(12.35, 12.40, RoundingType.Rounding01Up)]
    [TestCase(12.36, 12.40, RoundingType.Rounding01Up)]
    [TestCase(12.00, 12.00, RoundingType.Rounding01Up)]
    [TestCase(12.10, 12.10, RoundingType.Rounding01Up)]
    [TestCase(12.35, 12.30, RoundingType.Rounding01Down)]
    [TestCase(12.36, 12.40, RoundingType.Rounding01Down)]
    [TestCase(12.00, 12.00, RoundingType.Rounding01Down)]
    [TestCase(12.10, 12.10, RoundingType.Rounding01Down)]
    [TestCase(12.24, 12.00, RoundingType.Rounding05)]
    [TestCase(12.49, 12.50, RoundingType.Rounding05)]
    [TestCase(12.74, 12.50, RoundingType.Rounding05)]
    [TestCase(12.99, 13.00, RoundingType.Rounding05)]
    [TestCase(12.00, 12.00, RoundingType.Rounding05)]
    [TestCase(12.50, 12.50, RoundingType.Rounding05)]
    [TestCase(12.49, 12.00, RoundingType.Rounding1)]
    [TestCase(12.50, 13.00, RoundingType.Rounding1)]
    [TestCase(12.00, 12.00, RoundingType.Rounding1)]
    [TestCase(12.01, 13.00, RoundingType.Rounding1Up)]
    [TestCase(12.99, 13.00, RoundingType.Rounding1Up)]
    [TestCase(12.00, 12.00, RoundingType.Rounding1Up)]
    public void CanRound(decimal valueToRounding, decimal roundedValue, RoundingType roundingType)
    {
        _priceCalcService.Round(valueToRounding, roundingType).Should().Be(roundedValue);
    }

    #endregion

    #region Utilities

    private static void AssertMatchesFixture(GetFinalPriceCase expected, decimal priceWithoutDiscounts, decimal finalPrice)
    {
        finalPrice.Should().Be(expected.FinalPriceValue);
        priceWithoutDiscounts.Should().Be(expected.PriceWithoutDiscountsValue);
    }

    #endregion
}
