using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using SecureShop.Mvc.Controllers;
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Responses;
using SecureShop.Mvc.Models.ViewModels;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Tests;

public sealed class AccountControllerTests
{
    private const string QrReturnUrl =
        "/employee/orders/verify?token=protected-token";

    [Fact]
    public void LoginGet_PreservesLocalQrReturnUrl()
    {
        var controller = CreateController();

        var result = Assert.IsType<ViewResult>(
            controller.Login(QrReturnUrl));
        var model = Assert.IsType<LoginViewModel>(
            result.Model);

        Assert.Equal(QrReturnUrl, model.ReturnUrl);
    }

    [Fact]
    public void LoginGet_RejectsExternalReturnUrl()
    {
        var controller = CreateController();

        var result = Assert.IsType<ViewResult>(
            controller.Login(
                "https://attacker.example/steal-token"));
        var model = Assert.IsType<LoginViewModel>(
            result.Model);

        Assert.Null(model.ReturnUrl);
    }

    [Fact]
    public async Task LoginPost_RedirectsBackToLocalQrPage()
    {
        var controller = CreateController();
        var model = new LoginViewModel
        {
            Email = "employee@secureshop.local",
            Password = "test-password",
            ReturnUrl = QrReturnUrl
        };

        var result = await controller.Login(
            model,
            CancellationToken.None);

        var redirect = Assert.IsType<LocalRedirectResult>(
            result);
        Assert.Equal(QrReturnUrl, redirect.Url);
        Assert.True(controller.Response.Headers.ContainsKey(
            "Set-Cookie"));
    }

    [Fact]
    public async Task LoginPost_DoesNotRedirectToExternalUrl()
    {
        var controller = CreateController();
        var model = new LoginViewModel
        {
            Email = "employee@secureshop.local",
            Password = "test-password",
            ReturnUrl = "https://attacker.example/steal-token"
        };

        var result = await controller.Login(
            model,
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(
            result);
        Assert.Equal("Session", redirect.ActionName);
    }

    private static AccountController CreateController()
    {
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ControllerActionDescriptor());
        var controller = new AccountController(
            new SuccessfulAuthApiService())
        {
            ControllerContext = new ControllerContext(
                actionContext),
            Url = new UrlHelper(actionContext)
        };

        return controller;
    }

    private sealed class SuccessfulAuthApiService
        : IAuthApiService
    {
        public Task<LoginApiResult> LoginAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LoginApiResult(
                true,
                HttpStatusCode.OK,
                "__Host-SecureShop.Auth=test; path=/; secure; httponly",
                null));

        public Task<ApiResponse<AuthSessionResponse>> GetSessionAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
