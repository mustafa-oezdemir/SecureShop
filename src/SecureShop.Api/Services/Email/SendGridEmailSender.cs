using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace SecureShop.Api.Services.Email;

public sealed class SendGridEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SendGridEmailSender> _logger;

    public SendGridEmailSender(
        IOptions<EmailOptions> options,
        ILogger<SendGridEmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        string recipientEmail,
        string subject,
        string plainTextContent,
        string htmlContent,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(plainTextContent);
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlContent);

        var apiKey = RequireConfigurationValue(
            _options.SendGridApiKey,
            $"{EmailOptions.SectionName}:SendGridApiKey");

        var senderEmail = RequireConfigurationValue(
            _options.SenderEmail,
            $"{EmailOptions.SectionName}:SenderEmail");

        var senderName = RequireConfigurationValue(
            _options.SenderName,
            $"{EmailOptions.SectionName}:SenderName");

        var client = new SendGridClient(apiKey);

        var message = MailHelper.CreateSingleEmail(
            new EmailAddress(senderEmail, senderName),
            new EmailAddress(recipientEmail.Trim()),
            subject.Trim(),
            plainTextContent,
            htmlContent);

        message.SetClickTracking(
            enable: false,
            enableText: false);

        var response = await client.SendEmailAsync(
            message,
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation(
                "E-posta saglayicisi mesaji basariyla kuyruga aldi.");

            return;
        }

        _logger.LogError(
            "E-posta saglayicisi mesaji reddetti. HTTP durum kodu: {StatusCode}",
            (int)response.StatusCode);

        throw new InvalidOperationException(
            "E-posta gonderim islemi tamamlanamadi.");
    }

    private static string RequireConfigurationValue(
        string? value,
        string configurationKey)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        throw new InvalidOperationException(
            $"'{configurationKey}' yapilandirmasi bulunamadi.");
    }
}
