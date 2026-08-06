using ECommerceApp.Application.Notifications.Models;
using ECommerceApp.Infrastructure.Notifications;
using ECommerceApp.Infrastructure.Tests.TestSupport;
using FluentAssertions;

namespace ECommerceApp.Infrastructure.Tests.Notifications;

public class EmailNotificationServiceTests
{
    private readonly FakeEmailTemplateRenderer _templateRenderer = new();
    private readonly FakeEmailSender _emailSender = new();
    private readonly EmailNotificationService _service;

    public EmailNotificationServiceTests()
    {
        _service = new EmailNotificationService(_templateRenderer, _emailSender);
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_renders_the_password_reset_template_with_the_reset_link()
    {
        await _service.SendPasswordResetEmailAsync("customer@example.com", "https://example.com/reset?token=abc");

        _templateRenderer.Rendered.Should().ContainSingle();
        var (templateName, model) = _templateRenderer.Rendered[0];
        templateName.Should().Be("Emails/PasswordReset");
        model.Should().BeOfType<PasswordResetEmailModel>()
            .Which.ResetLink.Should().Be("https://example.com/reset?token=abc");
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_sends_the_rendered_html_to_the_given_address()
    {
        await _service.SendPasswordResetEmailAsync("customer@example.com", "https://example.com/reset?token=abc");

        _emailSender.SentEmails.Should().ContainSingle();
        var (toEmail, subject, htmlBody) = _emailSender.SentEmails[0];
        toEmail.Should().Be("customer@example.com");
        subject.Should().Be("Reset your password");
        htmlBody.Should().Be("<html>Emails/PasswordReset</html>");
    }

    [Fact]
    public async Task SendOrderConfirmationEmailAsync_renders_the_order_confirmation_template_with_the_order_model()
    {
        var model = new OrderConfirmationEmailModel(
            "ORD-1001", "Jane Doe", DateTime.UtcNow, [], 100m, 0m, 8m, 5m, 113m);

        await _service.SendOrderConfirmationEmailAsync("customer@example.com", model);

        _templateRenderer.Rendered.Should().ContainSingle();
        var (templateName, renderedModel) = _templateRenderer.Rendered[0];
        templateName.Should().Be("Emails/OrderConfirmation");
        renderedModel.Should().BeSameAs(model);
    }

    [Fact]
    public async Task SendOrderConfirmationEmailAsync_sends_the_rendered_html_with_a_subject_naming_the_order()
    {
        var model = new OrderConfirmationEmailModel(
            "ORD-1001", "Jane Doe", DateTime.UtcNow, [], 100m, 0m, 8m, 5m, 113m);

        await _service.SendOrderConfirmationEmailAsync("customer@example.com", model);

        _emailSender.SentEmails.Should().ContainSingle();
        var (toEmail, subject, htmlBody) = _emailSender.SentEmails[0];
        toEmail.Should().Be("customer@example.com");
        subject.Should().Be("Order confirmation - ORD-1001");
        htmlBody.Should().Be("<html>Emails/OrderConfirmation</html>");
    }
}
