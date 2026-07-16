using System.Text;
using ECommerceApp.Application.Common.Interfaces;

namespace ECommerceApp.Infrastructure.Email;

/// <summary>
/// Development-only email "sender": writes the rendered email to a local
/// preview file instead of delivering it, and deliberately does not go
/// through <c>ILogger</c>/Serilog, so reset/confirmation links and any other
/// sensitive content in the email body never reach the structured application
/// log. Replaced by a real SMTP sender in Milestone 15.
/// </summary>
public sealed class DevEmailSender : IEmailSender
{
    private readonly string _previewDirectory;

    public DevEmailSender()
    {
        _previewDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "DevEmails");
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_previewDirectory);

        var safeRecipient = string.Concat(toEmail.Split(Path.GetInvalidFileNameChars()));
        var fileName = $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}_{safeRecipient}.html";
        var filePath = Path.Combine(_previewDirectory, fileName);

        var content = new StringBuilder()
            .AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"></head><body>")
            .AppendLine($"<p><strong>To:</strong> {toEmail}</p>")
            .AppendLine($"<p><strong>Subject:</strong> {subject}</p>")
            .AppendLine("<hr/>")
            .AppendLine(htmlBody)
            .AppendLine("</body></html>")
            .ToString();

        await File.WriteAllTextAsync(filePath, content, cancellationToken);
    }
}
