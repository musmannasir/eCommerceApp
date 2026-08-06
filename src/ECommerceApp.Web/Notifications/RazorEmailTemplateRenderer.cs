using ECommerceApp.Application.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace ECommerceApp.Web.Notifications;

/// <summary>
/// Renders an email template (a normal Razor view under Views/Emails) to an
/// HTML string using ASP.NET Core's own view engine - the standard
/// "render a view to string" technique, so no third-party templating
/// library is needed for Milestone 15.1.
/// </summary>
public sealed class RazorEmailTemplateRenderer : IEmailTemplateRenderer
{
    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;
    private readonly IServiceProvider _serviceProvider;

    public RazorEmailTemplateRenderer(IRazorViewEngine viewEngine, ITempDataProvider tempDataProvider, IServiceProvider serviceProvider)
    {
        _viewEngine = viewEngine;
        _tempDataProvider = tempDataProvider;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> RenderAsync<TModel>(string templateName, TModel model, CancellationToken cancellationToken = default)
    {
        var httpContext = new DefaultHttpContext { RequestServices = _serviceProvider };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        var viewResult = _viewEngine.GetView(executingFilePath: null, viewPath: $"~/Views/{templateName}.cshtml", isMainPage: true);
        if (!viewResult.Success)
        {
            throw new InvalidOperationException(
                $"Email template '{templateName}' could not be found. Searched: {string.Join(", ", viewResult.SearchedLocations)}");
        }

        await using var writer = new StringWriter();

        var viewDataDictionary = new ViewDataDictionary<TModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model,
        };

        var viewContext = new ViewContext(
            actionContext,
            viewResult.View,
            viewDataDictionary,
            new TempDataDictionary(actionContext.HttpContext, _tempDataProvider),
            writer,
            new HtmlHelperOptions());

        await viewResult.View.RenderAsync(viewContext);
        return writer.ToString();
    }
}
