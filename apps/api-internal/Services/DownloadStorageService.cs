using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services;

public sealed record StoredDownloadFileMetadata(
    string StorageKey,
    string OriginalName,
    string ContentType,
    long SizeBytes,
    string? Extension);

public sealed record DownloadFileReadResult(
    Stream Stream,
    string ContentType,
    string FileName,
    long? SizeBytes);

public interface IDownloadStorageService
{
    Task<StoredDownloadFileMetadata> SaveAsync(
        string originalName,
        string? contentType,
        Stream source,
        CancellationToken cancellationToken);

    Task<DownloadFileReadResult> OpenReadAsync(
        StoredDownloadFileMetadata file,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        StoredDownloadFileMetadata? file,
        CancellationToken cancellationToken);
}

public sealed class DownloadStorageService : IDownloadStorageService
{
    private readonly string _rootPath;

    public DownloadStorageService(DownloadStorageRuntimeConfiguration configuration)
    {
        _rootPath = configuration.RootPath;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<StoredDownloadFileMetadata> SaveAsync(
        string originalName,
        string? contentType,
        Stream source,
        CancellationToken cancellationToken)
    {
        var safeOriginalName = SanitizeOriginalName(originalName);
        var extension = NormalizeExtension(Path.GetExtension(safeOriginalName));
        var storageKey = $"{Guid.NewGuid():N}{extension}";
        var fullPath = ResolvePath(storageKey);

        await using var target = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous);
        await source.CopyToAsync(target, cancellationToken);

        return new StoredDownloadFileMetadata(
            storageKey,
            safeOriginalName,
            NormalizeContentType(contentType),
            target.Length,
            string.IsNullOrWhiteSpace(extension) ? null : extension);
    }

    public Task<DownloadFileReadResult> OpenReadAsync(
        StoredDownloadFileMetadata file,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = ResolvePath(file.StorageKey);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "Stored download file is missing.",
                fullPath);
        }

        Stream stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return Task.FromResult(
            new DownloadFileReadResult(
                stream,
                NormalizeContentType(file.ContentType),
                file.OriginalName,
                file.SizeBytes));
    }

    public Task DeleteAsync(
        StoredDownloadFileMetadata? file,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (file is null || string.IsNullOrWhiteSpace(file.StorageKey))
        {
            return Task.CompletedTask;
        }

        var fullPath = ResolvePath(file.StorageKey);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string storageKey)
    {
        var normalizedKey = storageKey.Trim();
        if (normalizedKey.Length == 0
            || normalizedKey.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || normalizedKey.Contains(Path.DirectorySeparatorChar)
            || normalizedKey.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidOperationException(
                "Invalid stored download key.");
        }

        return Path.Combine(_rootPath, normalizedKey);
    }

    private static string NormalizeContentType(string? contentType)
        => string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType.Trim();

    private static string SanitizeOriginalName(string value)
    {
        var fileName = Path.GetFileName(value ?? string.Empty).Trim();
        if (fileName.Length == 0)
        {
            fileName = "telechargement.bin";
        }

        var sanitized = new string(
            fileName
                .Where(character =>
                    !char.IsControl(character)
                    && character != '"'
                    && character != '\r'
                    && character != '\n')
                .ToArray());

        return string.IsNullOrWhiteSpace(sanitized)
            ? "telechargement.bin"
            : sanitized.Length <= 180
                ? sanitized
                : sanitized[..180];
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var normalized = extension.Trim().ToLowerInvariant();
        if (normalized.Length > 20 || normalized[0] != '.')
        {
            return string.Empty;
        }

        return normalized.All(character =>
            character == '.'
            || (character >= 'a' && character <= 'z')
            || (character >= '0' && character <= '9'))
            ? normalized
            : string.Empty;
    }
}
