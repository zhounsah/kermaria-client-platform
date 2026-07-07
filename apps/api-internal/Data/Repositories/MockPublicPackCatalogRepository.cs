using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockPublicPackCatalogStore
{
    public PublicPackCatalogContent? Content { get; set; }
}

public sealed class MockPublicPackCatalogRepository
    : IPublicPackCatalogRepository
{
    private readonly MockPublicPackCatalogStore _store;

    public MockPublicPackCatalogRepository(MockPublicPackCatalogStore store)
    {
        _store = store;
    }

    public bool IsPersistent => false;

    public Task<PublicPackCatalogContent?> GetAsync(
        CancellationToken cancellationToken)
        => Task.FromResult(_store.Content);

    public Task<PublicPackCatalogMutationResponse> UpsertAsync(
        ValidatedPublicPackCatalogContent content,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var updatedAt = DateTime.UtcNow.ToString("O");
        var next = new PublicPackCatalogContent(
            content.PageEyebrow,
            content.PageTitle,
            content.PageDescription,
            content.ComparisonColumnLabel,
            content.FootnotePrimary,
            content.FootnoteSecondary,
            content.Packs,
            content.ComparisonRows,
            updatedAt);
        var changed = !Equals(_store.Content, next);
        _store.Content = next;

        return Task.FromResult(
            new PublicPackCatalogMutationResponse(
                changed,
                updatedAt,
                correlationId));
    }
}
