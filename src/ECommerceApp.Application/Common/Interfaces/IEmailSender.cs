namespace ECommerceApp.Application.Common.Interfaces;

/// <summary>
/// Sends transactional emails. The development implementation writes the
/// rendered email to a local preview location instead of delivering it, and
/// never writes it through the structured application log (so reset/
/// confirmation links and tokens never end up in Serilog output).
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
