using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Security;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Web.Controllers;
using Nop.Web.Factories;
using Nop.Web.Models.Order;
using NUnit.Framework;

namespace Nop.Tests.Nop.Web.Tests.Public.Controllers;

[TestFixture]
public class ReturnRequestControllerTests : WebTest
{
    private IAddressService _addressService;
    private ICustomerService _customerService;
    private IGenericAttributeService _genericAttributeService;
    private IOrderService _orderService;
    private IProductService _productService;
    private IRepository<Order> _orderRepository;
    private IRepository<OrderItem> _orderItemRepository;
    private IRepository<ReturnRequest> _returnRequestRepository;
    private IReturnRequestService _returnRequestService;
    private ISettingService _settingService;
    private IWorkContext _workContext;
    private ReturnRequestSettings _returnRequestSettings;

    [OneTimeSetUp]
    public async Task SetUp()
    {
        _addressService = GetService<IAddressService>();
        _customerService = GetService<ICustomerService>();
        _genericAttributeService = GetService<IGenericAttributeService>();
        _orderService = GetService<IOrderService>();
        _productService = GetService<IProductService>();
        _orderRepository = GetService<IRepository<Order>>();
        _orderItemRepository = GetService<IRepository<OrderItem>>();
        _returnRequestRepository = GetService<IRepository<ReturnRequest>>();
        _returnRequestService = GetService<IReturnRequestService>();
        _settingService = GetService<ISettingService>();
        _workContext = GetService<IWorkContext>();
        _returnRequestSettings = GetService<ReturnRequestSettings>();

        _returnRequestSettings.ReturnRequestsEnabled = true;
        _returnRequestSettings.ReturnRequestsForCompletedOrdersOnly = true;
        _returnRequestSettings.NumberOfDaysReturnRequestAvailable = 365;
        _returnRequestSettings.WithdrawalLinkDaysValid = 14;
        _returnRequestSettings.DownloadableProductsReturnRequestsAllowed = false;
        await _settingService.SaveSettingAsync(_returnRequestSettings);
    }

    private ReturnRequestController CreateController()
    {
        return new ReturnRequestController(
            GetService<CaptchaSettings>(),
            _addressService,
            _customerService,
            GetService<ICustomNumberFormatter>(),
            GetService<IDownloadService>(),
            _genericAttributeService,
            GetService<ILocalizationService>(),
            GetService<INopFileProvider>(),
            GetService<IOrderProcessingService>(),
            _orderService,
            GetService<IReturnRequestModelFactory>(),
            _returnRequestService,
            GetService<IStoreContext>(),
            _workContext,
            GetService<IWorkflowMessageService>(),
            GetService<LocalizationSettings>(),
            GetService<ReturnRequestSettings>());
    }

    [Test]
    public async Task ReturnRequestSubmitShouldRejectQuantityExceedingAvailableQuantityForReturn()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var order = new Order
        {
            OrderGuid = Guid.NewGuid(),
            CustomOrderNumber = string.Empty,
            CustomerId = customer.Id,
            BillingAddressId = 1,
            OrderStatus = OrderStatus.Complete,
            CreatedOnUtc = DateTime.UtcNow
        };
        await _orderService.InsertOrderAsync(order);

        var orderItem = new OrderItem
        {
            OrderItemGuid = Guid.NewGuid(),
            OrderId = order.Id,
            ProductId = 1,
            Quantity = 2,
            UnitPriceInclTax = 10,
            UnitPriceExclTax = 10,
            PriceInclTax = 20,
            PriceExclTax = 20
        };
        await _orderService.InsertOrderItemAsync(orderItem);

        try
        {
            var controller = CreateController();

            // Submitting quantity 3 when only 2 were purchased should be rejected
            var excessiveForm = new FormCollection(new Dictionary<string, StringValues>
            {
                [$"quantity{orderItem.Id}"] = "3"
            });
            var excessiveResult = await controller.ReturnRequestSubmit(order.Id, new SubmitReturnRequestModel(), excessiveForm) as ViewResult;
            excessiveResult.Should().NotBeNull();
            controller.ModelState.IsValid.Should().BeFalse();

            var requestsAfterExcessive = await _returnRequestService.SearchReturnRequestsAsync(orderItemId: orderItem.Id);
            requestsAfterExcessive.Should().BeEmpty();

            // Submitting valid partial quantity 1 should succeed
            var controllerForValid = CreateController();
            var validForm = new FormCollection(new Dictionary<string, StringValues>
            {
                [$"quantity{orderItem.Id}"] = "1"
            });
            var validResult = await controllerForValid.ReturnRequestSubmit(order.Id, new SubmitReturnRequestModel(), validForm) as ViewResult;
            validResult.Should().NotBeNull();
            controllerForValid.ModelState.IsValid.Should().BeTrue();

            var requestsAfterValid = await _returnRequestService.SearchReturnRequestsAsync(orderItemId: orderItem.Id);
            requestsAfterValid.Count.Should().Be(1);
            requestsAfterValid[0].Quantity.Should().Be(1);

            // Submitting quantity 2 when only 1 remains available should be rejected
            var controllerForRemaining = CreateController();
            var overRemainingForm = new FormCollection(new Dictionary<string, StringValues>
            {
                [$"quantity{orderItem.Id}"] = "2"
            });
            var overRemainingResult = await controllerForRemaining.ReturnRequestSubmit(order.Id, new SubmitReturnRequestModel(), overRemainingForm) as ViewResult;
            overRemainingResult.Should().NotBeNull();
            controllerForRemaining.ModelState.IsValid.Should().BeFalse();

            var requestsAfterOverRemaining = await _returnRequestService.SearchReturnRequestsAsync(orderItemId: orderItem.Id);
            requestsAfterOverRemaining.Count.Should().Be(1);
        }
        finally
        {
            var createdRequests = await _returnRequestService.SearchReturnRequestsAsync(orderItemId: orderItem.Id);
            foreach (var rr in createdRequests)
                await _returnRequestRepository.DeleteAsync(rr);

            await _orderItemRepository.DeleteAsync(orderItem);
            await _orderRepository.DeleteAsync(order);
        }
    }

    [Test]
    public async Task ReturnRequestSubmitShouldRejectDownloadableProductWhenNotAllowed()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var downloadableProduct = await _productService.GetProductByIdAsync(3);
        var originalIsDownload = downloadableProduct.IsDownload;
        downloadableProduct.IsDownload = true;
        await _productService.UpdateProductAsync(downloadableProduct);

        var order = new Order
        {
            OrderGuid = Guid.NewGuid(),
            CustomOrderNumber = string.Empty,
            CustomerId = customer.Id,
            BillingAddressId = 1,
            OrderStatus = OrderStatus.Complete,
            CreatedOnUtc = DateTime.UtcNow
        };
        await _orderService.InsertOrderAsync(order);

        var physicalItem = new OrderItem
        {
            OrderItemGuid = Guid.NewGuid(),
            OrderId = order.Id,
            ProductId = 1,
            Quantity = 1
        };
        await _orderService.InsertOrderItemAsync(physicalItem);

        var downloadableItem = new OrderItem
        {
            OrderItemGuid = Guid.NewGuid(),
            OrderId = order.Id,
            ProductId = downloadableProduct.Id,
            Quantity = 1
        };
        await _orderService.InsertOrderItemAsync(downloadableItem);

        try
        {
            var controller = CreateController();
            var form = new FormCollection(new Dictionary<string, StringValues>
            {
                [$"quantity{downloadableItem.Id}"] = "1"
            });

            var result = await controller.ReturnRequestSubmit(order.Id, new SubmitReturnRequestModel(), form) as ViewResult;
            result.Should().NotBeNull();
            controller.ModelState.IsValid.Should().BeFalse();

            var downloadableRequests = await _returnRequestService.SearchReturnRequestsAsync(orderItemId: downloadableItem.Id);
            downloadableRequests.Should().BeEmpty();
        }
        finally
        {
            downloadableProduct.IsDownload = originalIsDownload;
            await _productService.UpdateProductAsync(downloadableProduct);

            await _orderItemRepository.DeleteAsync(downloadableItem);
            await _orderItemRepository.DeleteAsync(physicalItem);
            await _orderRepository.DeleteAsync(order);
        }
    }

    [Test]
    public async Task ReturnRequestSubmitWithWithdrawalTokenShouldAssignOrderCustomer()
    {
        var guestCustomer = await _customerService.InsertGuestCustomerAsync();
        var order = new Order
        {
            OrderGuid = Guid.NewGuid(),
            CustomOrderNumber = string.Empty,
            CustomerId = guestCustomer.Id,
            BillingAddressId = 1,
            OrderStatus = OrderStatus.Complete,
            CreatedOnUtc = DateTime.UtcNow
        };
        await _orderService.InsertOrderAsync(order);

        var orderItem = new OrderItem
        {
            OrderItemGuid = Guid.NewGuid(),
            OrderId = order.Id,
            ProductId = 1,
            Quantity = 1
        };
        await _orderService.InsertOrderItemAsync(orderItem);

        var token = Guid.NewGuid().ToString();
        await _genericAttributeService.SaveAttributeAsync(order, NopOrderDefaults.WithdrawalTokenAttribute, token);
        await _genericAttributeService.SaveAttributeAsync(order, NopOrderDefaults.WithdrawalTokenDateGeneratedAttribute, DateTime.UtcNow);

        try
        {
            var controller = CreateController();
            var form = new FormCollection(new Dictionary<string, StringValues>
            {
                [$"quantity{orderItem.Id}"] = "1"
            });

            var model = new SubmitReturnRequestModel { WithdrawalToken = token };
            var result = await controller.ReturnRequestSubmit(order.Id, model, form) as ViewResult;
            result.Should().NotBeNull();
            controller.ModelState.IsValid.Should().BeTrue();

            var createdRequests = await _returnRequestService.SearchReturnRequestsAsync(orderItemId: orderItem.Id);
            createdRequests.Count.Should().Be(1);
            createdRequests[0].CustomerId.Should().Be(guestCustomer.Id);
        }
        finally
        {
            var createdRequests = await _returnRequestService.SearchReturnRequestsAsync(orderItemId: orderItem.Id);
            foreach (var rr in createdRequests)
                await _returnRequestRepository.DeleteAsync(rr);

            await _genericAttributeService.SaveAttributeAsync<string>(order, NopOrderDefaults.WithdrawalTokenAttribute, null);
            await _orderItemRepository.DeleteAsync(orderItem);
            await _orderRepository.DeleteAsync(order);
            await GetService<IRepository<Customer>>().DeleteAsync(guestCustomer);
        }
    }
}
