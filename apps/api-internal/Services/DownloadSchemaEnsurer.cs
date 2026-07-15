using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Data.Migration;

namespace Kermaria.ApiInternal.Services;

public interface IDownloadSchemaEnsurer
{
    Task EnsureAsync(CancellationToken cancellationToken);
}

public sealed class DownloadSchemaEnsurer : IDownloadSchemaEnsurer
{
    private readonly bool _isPersistent;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly MariaDbMigrationRunner _migrationRunner;
    private readonly ILogger<DownloadSchemaEnsurer> _logger;
    private volatile bool _ensured;

    public DownloadSchemaEnsurer(
        SqlRuntimeConfiguration configuration,
        MariaDbMigrationRunner migrationRunner,
        ILogger<DownloadSchemaEnsurer> logger)
    {
        _isPersistent = configuration.IsPersistent;
        _migrationRunner = migrationRunner;
        _logger = logger;
    }

    public async Task EnsureAsync(CancellationToken cancellationToken)
    {
        if (_ensured || !_isPersistent)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_ensured)
            {
                return;
            }

            await _migrationRunner.ApplyAsync(
                seedDevelopmentData: false,
                cancellationToken);

            _ensured = true;
            _logger.LogInformation(
                "Download schema ensured before download service usage.");
        }
        finally
        {
            _gate.Release();
        }
    }
}
