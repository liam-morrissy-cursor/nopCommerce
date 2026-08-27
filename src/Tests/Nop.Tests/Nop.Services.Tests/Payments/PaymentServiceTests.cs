using AwesomeAssertions;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Services.Payments;
using NUnit.Framework;

namespace Nop.Tests.Nop.Services.Tests.Payments;

[TestFixture]
public class PaymentServiceTests : ServiceTest
{
    private const string TestPaymentMethodSystemName = "Payments.TestMethod";
    private const string UnknownPaymentMethodSystemName = "Payments.DoesNotExist";
    private const int TestOrderId = 42;

    private IPaymentPluginManager _paymentPluginManager;
    private IPaymentService _paymentService;

    [OneTimeSetUp]
    public void SetUp()
    {
        _paymentPluginManager = GetService<IPaymentPluginManager>();
        _paymentService = GetService<IPaymentService>();
    }

    [TearDown]
    public void TearDown()
    {
        ResetTestPaymentMethodFlags();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        ResetTestPaymentMethodFlags();
    }

    [Test]
    public async Task CanLoadPaymentMethods()
    {
        var paymentMethods = await _paymentPluginManager.LoadAllPluginsAsync();
        paymentMethods.Should().NotBeNull();
    }

    [Test]
    public async Task CanLoadPaymentMethodBySystemKeyword()
    {
        var paymentMethod = await _paymentPluginManager.LoadPluginBySystemNameAsync(TestPaymentMethodSystemName);
        paymentMethod.Should().NotBeNull();
    }

    [Test]
    public async Task CanLoadActivePaymentMethods()
    {
        var paymentMethods = await _paymentPluginManager.LoadActivePluginsAsync([TestPaymentMethodSystemName]);
        paymentMethods.Should().NotBeNull();
        paymentMethods.Any().Should().BeTrue();
    }

    [Test]
    public async Task CanProcessPaymentWithTestMethod()
    {
        var request = new ProcessPaymentRequest
        {
            PaymentMethodSystemName = TestPaymentMethodSystemName,
            OrderTotal = 10m
        };
        var orderGuid = request.OrderGuid;

        var result = await _paymentService.ProcessPaymentAsync(request);

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.NewPaymentStatus.Should().Be(PaymentStatus.Paid);
        request.OrderGuid.Should().Be(orderGuid);
        orderGuid.Should().NotBe(Guid.Empty);
    }

    [Test]
    public async Task GetAdditionalHandlingFeeForEmptyCartIsNonNegativeDecimal()
    {
        TestPaymentMethod.AdditionalHandlingFee = 2.50m;

        var fee = await _paymentService.GetAdditionalHandlingFeeAsync([], TestPaymentMethodSystemName);

        fee.Should().BeOfType(typeof(decimal));
        fee.Should().BeGreaterThanOrEqualTo(0m);
        fee.Should().Be(2.50m);
    }

    [Test]
    public async Task CanCapturePaymentWhenSupportedAndNotWhenDisabled()
    {
        var order = CreateTestOrder(PaymentStatus.Authorized);

        TestPaymentMethod.TestSupportCapture = true;

        (await _paymentService.SupportCaptureAsync(TestPaymentMethodSystemName)).Should().BeTrue();

        var request = new CapturePaymentRequest { Order = order };
        var result = await _paymentService.CaptureAsync(request);

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.NewPaymentStatus.Should().Be(PaymentStatus.Paid);
        request.Order.Id.Should().Be(TestOrderId);

        TestPaymentMethod.TestSupportCapture = false;

        (await _paymentService.SupportCaptureAsync(TestPaymentMethodSystemName)).Should().BeFalse();
    }

    [Test]
    public async Task CanRefundPaymentWhenSupportedAndNotWhenDisabled()
    {
        var order = CreateTestOrder(PaymentStatus.Paid);

        TestPaymentMethod.TestSupportRefund = true;

        (await _paymentService.SupportRefundAsync(TestPaymentMethodSystemName)).Should().BeTrue();

        var request = new RefundPaymentRequest
        {
            Order = order,
            AmountToRefund = order.OrderTotal
        };
        var result = await _paymentService.RefundAsync(request);

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.NewPaymentStatus.Should().Be(PaymentStatus.Refunded);
        request.Order.Id.Should().Be(TestOrderId);

        TestPaymentMethod.TestSupportRefund = false;

        (await _paymentService.SupportRefundAsync(TestPaymentMethodSystemName)).Should().BeFalse();
    }

    [Test]
    public async Task CanVoidPaymentWhenSupportedAndNotWhenDisabled()
    {
        var order = CreateTestOrder(PaymentStatus.Authorized);

        TestPaymentMethod.TestSupportVoid = true;

        (await _paymentService.SupportVoidAsync(TestPaymentMethodSystemName)).Should().BeTrue();

        var request = new VoidPaymentRequest { Order = order };
        var result = await _paymentService.VoidAsync(request);

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.NewPaymentStatus.Should().Be(PaymentStatus.Voided);
        request.Order.Id.Should().Be(TestOrderId);

        TestPaymentMethod.TestSupportVoid = false;

        (await _paymentService.SupportVoidAsync(TestPaymentMethodSystemName)).Should().BeFalse();
    }

    [Test]
    public async Task SupportPartiallyRefundFollowsTestFlag()
    {
        TestPaymentMethod.TestSupportPartiallyRefund = true;
        (await _paymentService.SupportPartiallyRefundAsync(TestPaymentMethodSystemName)).Should().BeTrue();

        TestPaymentMethod.TestSupportPartiallyRefund = false;
        (await _paymentService.SupportPartiallyRefundAsync(TestPaymentMethodSystemName)).Should().BeFalse();
    }

    [Test]
    public async Task UnknownPaymentMethodDoesNotThrowUncaught()
    {
        (await _paymentService.SupportCaptureAsync(UnknownPaymentMethodSystemName)).Should().BeFalse();
        (await _paymentService.SupportRefundAsync(UnknownPaymentMethodSystemName)).Should().BeFalse();
        (await _paymentService.SupportPartiallyRefundAsync(UnknownPaymentMethodSystemName)).Should().BeFalse();
        (await _paymentService.SupportVoidAsync(UnknownPaymentMethodSystemName)).Should().BeFalse();

        var fee = await _paymentService.GetAdditionalHandlingFeeAsync([], UnknownPaymentMethodSystemName);
        fee.Should().Be(decimal.Zero);

        var order = CreateTestOrder(PaymentStatus.Authorized, UnknownPaymentMethodSystemName);

        var process = async () => await _paymentService.ProcessPaymentAsync(new ProcessPaymentRequest
        {
            PaymentMethodSystemName = UnknownPaymentMethodSystemName,
            OrderTotal = 10m
        });
        await process.Should().ThrowAsync<NopException>()
            .WithMessage("Payment method couldn't be loaded");

        var capture = async () => await _paymentService.CaptureAsync(new CapturePaymentRequest { Order = order });
        await capture.Should().ThrowAsync<NopException>()
            .WithMessage("Payment method couldn't be loaded");

        var refund = async () => await _paymentService.RefundAsync(new RefundPaymentRequest { Order = order });
        await refund.Should().ThrowAsync<NopException>()
            .WithMessage("Payment method couldn't be loaded");

        var voidPayment = async () => await _paymentService.VoidAsync(new VoidPaymentRequest { Order = order });
        await voidPayment.Should().ThrowAsync<NopException>()
            .WithMessage("Payment method couldn't be loaded");
    }

    private static void ResetTestPaymentMethodFlags()
    {
        TestPaymentMethod.TestSupportRefund = false;
        TestPaymentMethod.TestSupportCapture = false;
        TestPaymentMethod.TestSupportPartiallyRefund = false;
        TestPaymentMethod.TestSupportVoid = false;
        TestPaymentMethod.AdditionalHandlingFee = decimal.Zero;
    }

    private static Order CreateTestOrder(PaymentStatus paymentStatus, string paymentMethodSystemName = TestPaymentMethodSystemName)
    {
        return new Order
        {
            Id = TestOrderId,
            OrderTotal = 10m,
            PaymentStatus = paymentStatus,
            PaymentMethodSystemName = paymentMethodSystemName
        };
    }
}
