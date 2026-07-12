namespace SecureShop.Api.Services.Email;

public interface IEmailSender
{
    Task SendAsync(
        string recipientEmail,
        string subject,
        string plainTextContent,
        string htmlContent,
        CancellationToken cancellationToken = default);
}
