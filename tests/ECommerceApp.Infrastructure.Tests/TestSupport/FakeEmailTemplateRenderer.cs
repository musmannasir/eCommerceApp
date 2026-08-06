using ECommerceApp.Application.Notifications;

namespace ECommerceApp.Infrastructure.Tests.TestSupport;

/// <summary>In-memory stand-in for <see cref="IEmailTemplateRenderer"/>, so EmailNotificationService tests don't need a real Razor view engine.</summary>
public sealed class FakeEmailTemplateRenderer : IEmailTemplateRenderer
{
    public List<(string TemplateName, object? Model)> Rendered { get; } = [];

    public Task<string> RenderAsync<TModel>(string templateName, TModel model, CancellationToken cancellationToken = default)
    {
        Rendered.Add((templateName, model));
        return Task.FromResult($"<html>{templateName}</html>");
    }
}
