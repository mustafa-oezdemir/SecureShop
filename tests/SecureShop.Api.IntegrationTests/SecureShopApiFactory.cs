using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SecureShop.Api.Data;
using SecureShop.Api.Domain.Entities;

namespace SecureShop.Api.IntegrationTests;

public sealed class SecureShopApiFactory
    : WebApplicationFactory<Program>
{
    private readonly string _keyRingPath = Path.Combine(
        Path.GetTempPath(),
        $"secureshop-api-tests-{Guid.NewGuid():N}");
    private readonly string _databaseName =
        $"SecureShop-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "Authentication:SharedCookie:KeyRingPath",
            _keyRingPath);
        builder.UseSetting(
            "QrCodes:Orders:VerificationBaseUrl",
            "https://localhost/employee/orders/verify");
        builder.UseSetting(
            "QrCodes:Orders:LifetimeMinutes",
            "30");

        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Server=(localdb)\\MSSQLLocalDB;Database=UnusedTestDb;Trusted_Connection=True",
                    ["Authentication:Google:ClientId"] =
                        "integration-test-client",
                    ["Authentication:Google:ClientSecret"] =
                        "integration-test-secret",
                    ["Authentication:SharedCookie:KeyRingPath"] =
                        _keyRingPath,
                    ["QrCodes:Orders:VerificationBaseUrl"] =
                        "https://localhost/employee/orders/verify",
                    ["QrCodes:Orders:LifetimeMinutes"] = "30"
                });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<
                IDbContextOptionsConfiguration<AppDbContext>>();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(
                    _databaseName);
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var dbContext =
            scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.Database.EnsureCreated();

        var category = new Category("Elektronik");
        var product = new Product(
            category.Id,
            "Integration Product",
            "INTEGRATION-SKU",
            29.90m,
            12,
            "Integration test product");

        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        dbContext.SaveChanges();

        return host;
    }

    public HttpClient CreateHttpsClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(_keyRingPath))
        {
            Directory.Delete(_keyRingPath, recursive: true);
        }
    }
}
