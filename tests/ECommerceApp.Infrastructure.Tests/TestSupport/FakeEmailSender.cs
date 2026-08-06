using ECommerceApp.Application.Common.Interfaces;

namespace ECommerceApp.Infrastructure.Tests.TestSupport;

/// <summary>In-memory stand-in for <see cref="IEmailSender"/>, so tests don't touch disk like the real DevEmailSender does.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    public List<(string ToEmail, string Subject, string HtmlBody)> SentEmails { get; } = [];

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        SentEmails.Add((toEmail, subject, htmlBody));
        return Task.CompletedTask;
    }
}
