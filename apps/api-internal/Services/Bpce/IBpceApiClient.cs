namespace Kermaria.ApiInternal.Services.Bpce;

public interface IBpceApiClient
{
    Task<T?> GetJsonAsync<T>(
        string relativePath,
        CancellationToken cancellationToken);
}
