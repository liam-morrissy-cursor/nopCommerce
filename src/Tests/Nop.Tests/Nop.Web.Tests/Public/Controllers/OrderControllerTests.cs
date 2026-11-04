using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Http;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Shipping;
using Nop.Web.Controllers;
using Nop.Web.Factories;
using NUnit.Framework;

namespace Nop.Tests.Nop.Web.Tests.Public.Controllers;

[TestFixture]
public class OrderControllerTests
{
    private const int CustomerId = 11;
    private const int OrderId = 42;

    [Test]
    public async Task CancelOrder_PaidOrder_DoesNotCancel()
    {
        var harness = CreateHarness(new Order
        {
            Id = OrderId,
            CustomerId = CustomerId,
            PaymentStatus = PaymentStatus.Paid,
            OrderStatus = OrderStatus.Processing
        });

        var result = await harness.Controller.CancelOrder(OrderId);

        harness.OrderProcessingService.Verify(
            service => service.CancelOrderAsync(It.IsAny<Order>(), It.IsAny<bool>()),
            Times.Never);
        harness.OrderService.Verify(service => service.UpdateOrderAsync(It.IsAny<Order>()), Times.Never);
        result.Should().BeOfType<RedirectToRouteResult>()
            .Which.RouteName.Should().Be(NopRouteNames.Standard.ORDER_DETAILS);
    }

    [Test]
    public async Task CancelOrder_CompleteOrder_DoesNotCancel()
    {
        var harness = CreateHarness(new Order
        {
            Id = OrderId,
            CustomerId = CustomerId,
            PaymentStatus = PaymentStatus.Paid,
            OrderStatus = OrderStatus.Complete
        });

        var result = await harness.Controller.CancelOrder(OrderId);

        harness.OrderProcessingService.Verify(
            service => service.CancelOrderAsync(It.IsAny<Order>(), It.IsAny<bool>()),
            Times.Never);
        harness.OrderService.Verify(service => service.UpdateOrderAsync(It.IsAny<Order>()), Times.Never);
        result.Should().BeOfType<RedirectToRouteResult>()
            .Which.RouteName.Should().Be(NopRouteNames.Standard.ORDER_DETAILS);
    }

    [Test]
    public async Task CancelOrder_PendingPayment_CancelsOnce()
    {
        var harness = CreateHarness(new Order
        {
            Id = OrderId,
            CustomerId = CustomerId,
            PaymentStatus = PaymentStatus.Pending,
            OrderStatus = OrderStatus.Pending
        });

        var result = await harness.Controller.CancelOrder(OrderId);

        harness.OrderProcessingService.Verify(
            service => service.CancelOrderAsync(It.Is<Order>(order => order.Id == OrderId), false),
            Times.Once);
        result.Should().BeOfType<RedirectToRouteResult>()
            .Which.RouteName.Should().Be(NopRouteNames.Standard.ORDER_DETAILS);
    }

    [Test]
    public async Task CancelOrder_ForeignOwner_ChallengesAndDoesNotCancel()
    {
        var harness = CreateHarness(new Order
        {
            Id = OrderId,
            CustomerId = CustomerId + 1,
            PaymentStatus = PaymentStatus.Pending,
            OrderStatus = OrderStatus.Pending
        });

        var result = await harness.Controller.CancelOrder(OrderId);

        result.Should().BeOfType<ChallengeResult>();
        harness.OrderProcessingService.Verify(
            service => service.CancelOrderAsync(It.IsAny<Order>(), It.IsAny<bool>()),
            Times.Never);
    }

    private static Harness CreateHarness(Order order)
    {
        var customer = new Customer { Id = CustomerId };
        var orderService = new Mock<IOrderService>();
        var orderProcessingService = new Mock<IOrderProcessingService>();
        var workContext = new Mock<IWorkContext>();
        var localizationService = new Mock<ILocalizationService>();

        orderService.Setup(service => service.GetOrderByIdAsync(OrderId)).ReturnsAsync(order);
        orderService.Setup(service => service.UpdateOrderAsync(It.IsAny<Order>())).Returns(Task.CompletedTask);
        orderProcessingService.Setup(service => service.CanCancelOrder(It.IsAny<Order>())).Returns(true);
        orderProcessingService.Setup(service => service.CancelOrderAsync(It.IsAny<Order>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);
        workContext.Setup(context => context.GetCurrentCustomerAsync()).ReturnsAsync(customer);
        localizationService.Setup(service => service.GetResourceAsync(It.IsAny<string>()))
            .ReturnsAsync((string key) => key);

        var controller = new OrderController(
            Mock.Of<ICustomerService>(),
            localizationService.Object,
            Mock.Of<INotificationService>(),
            Mock.Of<IOrderModelFactory>(),
            orderProcessingService.Object,
            orderService.Object,
            Mock.Of<IPaymentService>(),
            Mock.Of<IPdfService>(),
            Mock.Of<IShipmentService>(),
            Mock.Of<IWebHelper>(),
            workContext.Object,
            new OrderSettings { AllowCustomersCancelOrders = true },
            new RewardPointsSettings());

        return new Harness(controller, orderService, orderProcessingService);
    }

    private sealed record Harness(
        OrderController Controller,
        Mock<IOrderService> OrderService,
        Mock<IOrderProcessingService> OrderProcessingService);
}
