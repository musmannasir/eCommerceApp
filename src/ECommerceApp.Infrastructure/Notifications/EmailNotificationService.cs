using ECommerceApp.Application.Common.Interfaces;
using ECommerceApp.Application.Notifications;
using ECommerceApp.Application.Notifications.Models;

namespace ECommerceApp.Infrastructure.Notifications;

public sealed class EmailNotificationService : IEmailNotificationService
{
    private readonly IEmailTemplateRenderer _templateRenderer;
    private readonly IEmailSender _emailSender;

    public EmailNotificationService(IEmailTemplateRenderer templateRenderer, IEmailSender emailSender)
    {
        _templateRenderer = templateRenderer;
        _emailSender = emailSender;
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetLink, CancellationToken cancellationToken = default)
    {
        var html = await _templateRenderer.RenderAsync("Emails/PasswordReset", new PasswordResetEmailModel(resetLink), cancellationToken);
        await _emailSender.SendAsync(toEmail, "Reset your password", html, cancellationToken);
    }

    public async Task SendOrderConfirmationEmailAsync(string toEmail, OrderConfirmationEmailModel model, CancellationToken cancellationToken = default)
    {
        var html = await _templateRenderer.RenderAsync("Emails/OrderConfirmation", model, cancellationToken);
        await _emailSender.SendAsync(toEmail, $"Order confirmation - {model.OrderNumber}", html, cancellationToken);
    }
}
