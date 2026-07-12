using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SecureShop.Api.Services.Email;

public static class EmailServiceCollectionExtensions
{
    public static IServiceCollection AddSecureShopEmail(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<EmailOptions>(
            configuration.GetSection(EmailOptions.SectionName));

        services.AddTransient<IEmailSender, SendGridEmailSender>();

        return services;
    }
}
