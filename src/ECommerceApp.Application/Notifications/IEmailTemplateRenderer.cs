namespace ECommerceApp.Application.Notifications;

/// <summary>
/// Renders a named email template with a strongly-typed model into an HTML
/// string. The Web layer owns the actual templates and rendering engine
/// (Razor views under Views/Emails) since Application/Infrastructure have no
/// view-rendering capability of their own.
/// </summary>
public interface IEmailTemplateRenderer
{
    Task<string> RenderAsync<TModel>(string templateName, TModel model, CancellationToken cancellationToken = default);
}
