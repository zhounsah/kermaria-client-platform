namespace Kermaria.ApiInternal.Data.Configuration;

public sealed record DownloadStorageRuntimeConfiguration(
    string RootPath,
    bool IsExplicitlyConfigured);

public static class DownloadStorageConfigurationResolver
{
    public static DownloadStorageRuntimeConfiguration Resolve(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var configuredRoot = configuration["DOWNLOAD_STORAGE_ROOT"]?.Trim();
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            return new DownloadStorageRuntimeConfiguration(
                Path.GetFullPath(configuredRoot),
                IsExplicitlyConfigured: true);
        }

        var developmentRoot = Path.Combine(
            AppContext.BaseDirectory,
            "AppData",
            "downloads");

        return new DownloadStorageRuntimeConfiguration(
            Path.GetFullPath(developmentRoot),
            IsExplicitlyConfigured: false);
    }
}
