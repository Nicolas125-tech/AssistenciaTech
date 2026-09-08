using System.Collections.Generic;
using System.Security.Claims;
using AssistenciaTech.Filters;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Filters;

public class DemoModeFilterTests
{
    private readonly DemoModeFilter _filter;

    public DemoModeFilterTests()
    {
        _filter = new DemoModeFilter();
    }

    [Fact]
    public void OnActionExecuting_NonDemoUser_AllowsExecution()
    {
        // Arrange
        var context = CreateActionExecutingContext(isAuthenticated: true, userName: "admin@assistenciatech.com", httpMethod: "POST");

        // Act
        _filter.OnActionExecuting(context);

        // Assert
        context.Result.Should().BeNull(); // Null result means the action is allowed to execute
    }

    [Fact]
    public void OnActionExecuting_DemoUserGetMethod_AllowsExecution()
    {
        // Arrange
        var context = CreateActionExecutingContext(isAuthenticated: true, userName: "demo@assistenciatech.com", httpMethod: "GET");

        // Act
        _filter.OnActionExecuting(context);

        // Assert
        context.Result.Should().BeNull();
    }

    [Fact]
    public void OnActionExecuting_UnauthenticatedUser_AllowsExecution()
    {
        // Arrange
        var context = CreateActionExecutingContext(isAuthenticated: false, userName: null, httpMethod: "POST");

        // Act
        _filter.OnActionExecuting(context);

        // Assert
        context.Result.Should().BeNull();
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public void OnActionExecuting_DemoUserModifyingMethodsWithReferer_RedirectsToReferer(string method)
    {
        // Arrange
        var refererUrl = "https://example.com/some-page";
        var context = CreateActionExecutingContext(
            isAuthenticated: true,
            userName: "demo@assistenciatech.com",
            httpMethod: method,
            referer: refererUrl,
            isController: true);

        // Act
        _filter.OnActionExecuting(context);

        // Assert
        context.Result.Should().BeOfType<RedirectResult>();
        var redirectResult = (RedirectResult)context.Result!;
        redirectResult.Url.Should().Be(refererUrl);

        var controller = (Controller)context.Controller;
        controller.TempData["Error"].Should().Be("Ação não permitida: Você está logado em uma conta de Demonstração. Nenhuma alteração foi salva no banco de dados.");
    }

    [Fact]
    public void OnActionExecuting_DemoUserModifyingMethodsWithoutReferer_RedirectsToAdminIndex()
    {
        // Arrange
        var context = CreateActionExecutingContext(
            isAuthenticated: true,
            userName: "demo@assistenciatech.com",
            httpMethod: "POST",
            referer: null,
            isController: true);

        // Act
        _filter.OnActionExecuting(context);

        // Assert
        context.Result.Should().BeOfType<RedirectToActionResult>();
        var redirectResult = (RedirectToActionResult)context.Result!;
        redirectResult.ActionName.Should().Be("Index");
        redirectResult.ControllerName.Should().Be("Admin");
    }

    [Fact]
    public void OnActionExecuting_DemoUserModifyingMethodsApiController_ReturnsJsonResult()
    {
        // Arrange
        var context = CreateActionExecutingContext(
            isAuthenticated: true,
            userName: "demo@assistenciatech.com",
            httpMethod: "POST",
            referer: null,
            isController: false); // Represents an API controller (ControllerBase)

        // Act
        _filter.OnActionExecuting(context);

        // Assert
        context.Result.Should().BeOfType<JsonResult>();
        var jsonResult = (JsonResult)context.Result!;
        jsonResult.StatusCode.Should().Be(403);
    }

    private ActionExecutingContext CreateActionExecutingContext(
        bool isAuthenticated,
        string? userName,
        string httpMethod,
        string? referer = null,
        bool isController = true)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = httpMethod;

        if (referer != null)
        {
            httpContext.Request.Headers["Referer"] = referer;
        }

        if (isAuthenticated)
        {
            var claims = new List<Claim>();
            if (userName != null)
            {
                claims.Add(new Claim(ClaimTypes.Name, userName));
            }
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            httpContext.User = new ClaimsPrincipal(identity);
        }
        else
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        }

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary()
        );

        object controller;
        if (isController)
        {
            var mockController = new Mock<Controller>();
            var tempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                httpContext,
                new Mock<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>().Object);
            mockController.Object.TempData = tempData;
            controller = mockController.Object;
        }
        else
        {
            controller = new Mock<ControllerBase>().Object;
        }

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller
        );
    }
}
