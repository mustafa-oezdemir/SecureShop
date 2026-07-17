using Microsoft.Extensions.Configuration;
using SecureShop.Api.Configuration;

namespace SecureShop.Api.UnitTests;

public sealed class DotEnvConfigurationTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"secureshop-dotenv-tests-{Guid.NewGuid():N}");

    [Fact]
    public void DotEnv_OverridesAppSettingsValue()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            Path.Combine(_directory, ".env"),
            "QrCodes__Orders__VerificationBaseUrl=https://phone.example/verify");
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["QrCodes:Orders:VerificationBaseUrl"] =
                    "https://localhost/verify"
            });

        DotEnvConfiguration.AddMissingFromDotEnv(
            configuration,
            _directory);

        Assert.Equal(
            "https://phone.example/verify",
            configuration[
                "QrCodes:Orders:VerificationBaseUrl"]);
    }

    [Fact]
    public void ProcessEnvironment_OverridesDotEnvValue()
    {
        const string key =
            "SecureShopTest__PhoneQr__PublicUrl";
        const string configurationKey =
            "SecureShopTest:PhoneQr:PublicUrl";
        var originalValue =
            Environment.GetEnvironmentVariable(key);

        try
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(
                Path.Combine(_directory, ".env"),
                $"{key}=https://dotenv.example");
            Environment.SetEnvironmentVariable(
                key,
                "https://environment.example");
            var configuration = new ConfigurationManager();
            configuration.AddEnvironmentVariables();

            DotEnvConfiguration.AddMissingFromDotEnv(
                configuration,
                _directory);

            Assert.Equal(
                "https://environment.example",
                configuration[configurationKey]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                key,
                originalValue);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
