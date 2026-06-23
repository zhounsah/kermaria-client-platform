namespace Kermaria.ApiInternal.Services.Bpce;

public interface IBpceApiClient
{
    Task<T?> GetJsonAsync<T>(
        string relativePath,
        CancellationToken cancellationToken);

    Task<byte[]?> GetBinaryAsync(
        string relativePath,
        CancellationToken cancellationToken);

    Task<TResponse?> PostJsonAsync<TResponse>(
        string relativePath,
        object payload,
        CancellationToken cancellationToken);
}
