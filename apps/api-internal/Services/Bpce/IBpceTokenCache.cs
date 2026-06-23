namespace Kermaria.ApiInternal.Services.Bpce;

public interface IBpceTokenCache
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);

    void Invalidate();
}
