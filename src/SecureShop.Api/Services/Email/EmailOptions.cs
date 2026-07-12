namespace SecureShop.Api.Services.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string? SendGridApiKey { get; set; }

    public string? SenderEmail { get; set; }

    public string SenderName { get; set; } = "SecureShop";
}
