using ECommerceApp.Web.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceApp.Web.Tests.Controllers;

public class HomeControllerTests
{
    private static HomeController CreateController() => new()
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
    };

    [Fact]
    public void Index_returns_the_default_view()
    {
        var controller = CreateController();

        var result = controller.Index();

        result.Should().BeOfType<ViewResult>().Which.ViewName.Should().BeNull();
    }

    [Fact]
    public void AccessDenied_returns_403_and_its_view()
    {
        var controller = CreateController();

        var result = controller.AccessDenied();

        controller.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        result.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public void HandleStatusCode_404_returns_the_NotFound_view()
    {
        var controller = CreateController();

        var result = controller.HandleStatusCode(404);

        result.Should().BeOfType<ViewResult>().Which.ViewName.Should().Be("NotFound");
    }

    [Fact]
    public void HandleStatusCode_403_redirects_to_AccessDenied()
    {
        var controller = CreateController();

        var result = controller.HandleStatusCode(403);

        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be(nameof(HomeController.AccessDenied));
    }
}
