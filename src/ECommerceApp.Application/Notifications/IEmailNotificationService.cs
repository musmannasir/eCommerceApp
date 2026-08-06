using ECommerceApp.Application.Notifications.Models;

namespace ECommerceApp.Application.Notifications;

/// <summary>
/// The business-facing email API - "send a password reset email", "send an
/// order confirmation email" - composing <see cref="IEmailTemplateRenderer"/>
/// and <see cref="Common.Interfaces.IEmailSender"/> so callers never build
/// HTML themselves.
/// </summary>
public interface IEmailNotificationService
{
    Task SendPasswordResetEmailAsync(string toEmail, string resetLink, CancellationToken cancellationToken = default);

    Task SendOrderConfirmationEmailAsync(string toEmail, OrderConfirmationEmailModel model, CancellationToken cancellationToken = default);
}
