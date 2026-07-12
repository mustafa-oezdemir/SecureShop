using Microsoft.Extensions.Configuration;

namespace SecureShop.Api.Configuration;

public static class DotEnvConfiguration
{
    private const string DotEnvFileName = ".env";

    public static void AddMissingFromDotEnv(
        ConfigurationManager configuration,
        string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        var dotEnvPath = FindDotEnvPath(contentRootPath);

        if (dotEnvPath is null)
        {
            return;
        }

        var values = new Dictionary<string, string?>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var line in File.ReadLines(dotEnvPath))
        {
            if (!TryParseLine(line, out var key, out var value))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(configuration[key]))
            {
                continue;
            }

            values.TryAdd(key, value);
        }

        if (values.Count > 0)
        {
            configuration.AddInMemoryCollection(values);
        }
    }

    private static string? FindDotEnvPath(
        string contentRootPath)
    {
        var startDirectories = new[]
        {
            Directory.GetCurrentDirectory(),
            contentRootPath,
            AppContext.BaseDirectory
        };

        foreach (var startDirectory in startDirectories.Distinct())
        {
            var directory = new DirectoryInfo(startDirectory);

            while (directory is not null)
            {
                var candidate = Path.Combine(
                    directory.FullName,
                    DotEnvFileName);

                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }

    private static bool TryParseLine(
        string line,
        out string key,
        out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var trimmedLine = line.Trim();

        if (string.IsNullOrEmpty(trimmedLine)
            || trimmedLine.StartsWith('#'))
        {
            return false;
        }

        if (trimmedLine.StartsWith(
            "export ",
            StringComparison.OrdinalIgnoreCase))
        {
            trimmedLine = trimmedLine["export ".Length..].TrimStart();
        }

        var separatorIndex = trimmedLine.IndexOf('=');

        if (separatorIndex <= 0)
        {
            return false;
        }

        key = trimmedLine[..separatorIndex]
            .Trim()
            .Replace("__", ":", StringComparison.Ordinal);

        value = NormalizeValue(
            trimmedLine[(separatorIndex + 1)..].Trim());

        return !string.IsNullOrWhiteSpace(key);
    }

    private static string NormalizeValue(
        string value)
    {
        if (value.Length < 2)
        {
            return value;
        }

        var quote = value[0];

        if ((quote != '"' && quote != '\'')
            || value[^1] != quote)
        {
            return value;
        }

        var unquotedValue = value[1..^1];

        return quote == '"'
            ? unquotedValue
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\n", "\n", StringComparison.Ordinal)
                .Replace("\\r", "\r", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal)
            : unquotedValue;
    }
}
