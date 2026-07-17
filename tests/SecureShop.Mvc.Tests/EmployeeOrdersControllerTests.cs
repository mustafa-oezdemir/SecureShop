using Microsoft.AspNetCore.Mvc;
using SecureShop.Mvc.Controllers;
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Models.Responses;
using SecureShop.Mvc.Models.ViewModels;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Tests;

public sealed class EmployeeOrdersControllerTests
{
    [Fact]
    public void VerifyGet_WithoutToken_ShowsScannerModel()
    {
        var controller = new EmployeeOrdersController(
            new UnusedOrderApiService());

        var result = Assert.IsType<ViewResult>(
            controller.Verify(null));
        var model = Assert.IsType<QrVerificationViewModel>(
            result.Model);

        Assert.Empty(model.Token);
        Assert.Null(model.Order);
    }

    [Fact]
    public void VerifyGet_WithToken_PreservesTokenForConfirmation()
    {
        var controller = new EmployeeOrdersController(
            new UnusedOrderApiService());

        var result = Assert.IsType<ViewResult>(
            controller.Verify("protected-token"));
        var model = Assert.IsType<QrVerificationViewModel>(
            result.Model);

        Assert.Equal("protected-token", model.Token);
        Assert.Null(model.Order);
    }

    private sealed class UnusedOrderApiService : IOrderApiService
    {
        public Task<ApiResponse<OrderResponse>> CreateAsync(
            CreateOrderRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResponse<IReadOnlyList<OrderResponse>>>
            GetMineAsync(
                CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResponse<OrderResponse>> GetMineAsync(
            string orderNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResponse<IReadOnlyList<OrderResponse>>>
            GetStaffAsync(
                CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResponse<OrderResponse>> GetStaffAsync(
            string orderNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResponse<OrderResponse>> ApproveAsync(
            string orderNumber,
            ProcessOrderRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResponse<OrderResponse>> MarkReadyAsync(
            string orderNumber,
            ProcessOrderRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResponse<OrderResponse>> CancelAsync(
            string orderNumber,
            ProcessOrderRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResponse<OrderResponse>> VerifyQrAsync(
            VerifyOrderQrRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
