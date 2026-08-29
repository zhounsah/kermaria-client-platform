using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

public sealed class MockCommercialStore
{
    private static readonly IFiscalPolicy FiscalPolicy = new FiscalPolicy();

    public object SyncRoot { get; } = new();

    public List<MockCommercialDocument> Documents { get; } =
    [
        new(
            "commercial-doc-mock-001",
            MockPortalData.Profile.CustomerReference,
            MockPortalData.Profile.CompanyName,
            "service-request-mock-001",
            "SRV-MOCK-ADMIN-001",
            CommercialStatuses.QuoteDraft,
            CommercialStatuses.SharedWithCustomer,
            "Proposition d'accompagnement VPN",
            "COM-20260612-0001",
            "EUR",
            CommercialStatuses.DefaultDisclaimer,
            "Administration interne de démonstration",
            "2026-06-12T10:00:00Z",
            "2026-06-12T10:30:00Z",
            "2026-06-12T10:30:00Z",
            null),
        new(
            "commercial-doc-mock-002",
            MockPortalData.Profile.CustomerReference,
            MockPortalData.Profile.CompanyName,
            null,
            null,
            CommercialStatuses.BillingDraft,
            CommercialStatuses.Draft,
            "Préparation de document de suivi",
            "COM-20260613-0002",
            "EUR",
            CommercialStatuses.DefaultDisclaimer,
            "Administration interne de démonstration",
            "2026-06-13T08:45:00Z",
            "2026-06-13T08:45:00Z",
            null,
            null)
    ];

    public Dictionary<string, List<MockCommercialDocumentLine>> Lines { get; } =
        new();

    public List<MockCommercialDocumentSubscriptionLink> SubscriptionLinks { get; } =
        [];

    public MockCommercialStore()
    {
        Lines["commercial-doc-mock-001"] =
        [
            new(
                "commercial-line-mock-001",
                "commercial-doc-mock-001",
                "Intervention ponctuelle",
                "Qualification informative de l'accès VPN envisagé.",
                2m,
                "heure",
                8500,
                null,
                17000,
                10,
                "2026-06-12T10:00:00Z",
                "2026-06-12T10:00:00Z"),
            new(
                "commercial-line-mock-002",
                "commercial-doc-mock-001",
                "Sauvegarde additionnelle",
                "Option informative associée à la proposition.",
                1m,
                "mois",
                2400,
                null,
                2400,
                20,
                "2026-06-12T10:05:00Z",
                "2026-06-12T10:05:00Z")
        ];

        Lines["commercial-doc-mock-002"] =
        [
            new(
                "commercial-line-mock-003",
                "commercial-doc-mock-002",
                "Accompagnement initial",
                "Brouillon de ligne informative interne.",
                1m,
                "forfait",
                4500,
                null,
                4500,
                10,
                "2026-06-13T08:45:00Z",
                "2026-06-13T08:45:00Z")
        ];

        foreach (var document in Documents)
        {
            RecalculateTotals(document);
        }
    }

    private void RecalculateTotals(MockCommercialDocument document)
    {
        var lines = Lines.TryGetValue(document.Id, out var storedLines)
            ? storedLines
            : [];
        document.SubtotalAmountCents = lines.Sum(line => line.LineTotalCents);
        document.TaxAmountCents = lines.Sum(line => CalculateTaxAmount(line));
        document.TotalAmountCents =
            document.SubtotalAmountCents + document.TaxAmountCents;
    }

    private static int CalculateTaxAmount(MockCommercialDocumentLine line)
        => FiscalPolicy.CalculateTaxAmount(
            line.LineTotalCents,
            line.TaxRateBasisPoints);

}

public sealed class MockCommercialDocumentRepository : ICommercialDocumentRepository
{
    private static readonly IFiscalPolicy FiscalPolicy = new FiscalPolicy();

    private readonly MockCommercialStore _store;

    public MockCommercialDocumentRepository(MockCommercialStore store)
    {
        _store = store;
    }

    public bool IsPersistent => false;

    public Task<IReadOnlyList<CommercialDocumentSummary>> GetClientDocumentsAsync(
        PortalSessionContext session,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<CommercialDocumentSummary>>(
                _store.Documents
                    .Where(document =>
                        document.CustomerReference == session.CustomerReference
                        && document.SharedAt is not null)
                    .OrderByDescending(document => document.UpdatedAt)
                    .Select(ToDocumentSummary)
                    .ToArray());
        }
    }

    public Task<CommercialDocumentDetail?> GetClientDocumentAsync(
        PortalSessionContext session,
        string documentId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var document = _store.Documents.FirstOrDefault(candidate =>
                candidate.Id == documentId
                && candidate.CustomerReference == session.CustomerReference
                && candidate.SharedAt is not null);
            return Task.FromResult(
                document is null ? null : ToClientDetail(document));
        }
    }

    public Task<IReadOnlyList<AdminCommercialDocumentSummary>>
        GetAdminDocumentsAsync(
            CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<AdminCommercialDocumentSummary>>(
                _store.Documents
                    .OrderByDescending(document => document.UpdatedAt)
                    .Select(ToAdminSummary)
                    .ToArray());
        }
    }

    public Task<AdminCommercialDocumentDetail?> GetAdminDocumentAsync(
        string documentId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var document = _store.Documents.FirstOrDefault(
                candidate => candidate.Id == documentId);
            return Task.FromResult(
                document is null ? null : ToAdminDetail(document));
        }
    }

    public Task<CommercialDocumentMutationResponse> CreateDocumentAsync(
        PortalSessionContext actor,
        ValidatedCommercialDocument document,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            EnsureKnownCustomer(document.CustomerReference);
            EnsureKnownServiceRequest(document.ServiceRequestId);
            var customerReference = document.CustomerReference
                ?? throw new PortalValidationException();

            var now = DateTime.UtcNow.ToString("O");
            var item = new MockCommercialDocument(
                Guid.NewGuid().ToString("D"),
                customerReference,
                MockPortalData.Profile.CompanyName,
                document.ServiceRequestId,
                ResolveServiceRequestReference(document.ServiceRequestId),
                document.DocumentType,
                document.Status,
                document.Title,
                CreateReference(),
                document.Currency,
                document.Disclaimer,
                actor.DisplayName,
                now,
                now,
                null,
                null);
            item.CustomerId = "mock-customer";
            _store.Documents.Add(item);
            _store.Lines[item.Id] = [];

            return Task.FromResult(new CommercialDocumentMutationResponse(
                item.Id,
                item.InternalReference,
                item.Status,
                true,
                correlationId));
        }
    }

    public Task<CommercialDocumentMutationResponse> UpdateDocumentAsync(
        PortalSessionContext actor,
        string documentId,
        ValidatedCommercialDocument document,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var current = FindDocument(documentId);
            if (current.Status != CommercialStatuses.Draft)
            {
                throw new PortalValidationException();
            }

            EnsureKnownCustomer(document.CustomerReference);
            EnsureKnownServiceRequest(document.ServiceRequestId);

            var changed =
                current.Title != document.Title
                || current.DocumentType != document.DocumentType
                || current.Disclaimer != document.Disclaimer
                || current.ServiceRequestId != document.ServiceRequestId
                || current.Status != document.Status;
            current.Title = document.Title;
            current.DocumentType = document.DocumentType;
            current.Disclaimer = document.Disclaimer;
            current.ServiceRequestId = document.ServiceRequestId;
            current.ServiceRequestReference =
                ResolveServiceRequestReference(document.ServiceRequestId);
            current.Status = document.Status;
            current.UpdatedAt = DateTime.UtcNow.ToString("O");

            return Task.FromResult(new CommercialDocumentMutationResponse(
                current.Id,
                current.InternalReference,
                current.Status,
                changed,
                correlationId));
        }
    }

    public Task<CommercialDocumentLineMutationResponse> AddLineAsync(
        PortalSessionContext actor,
        string documentId,
        ValidatedCommercialDocumentLine line,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var document = FindDraftDocument(documentId);
            var now = DateTime.UtcNow.ToString("O");
            var created = CreateLine(documentId, line, now);
            LinesFor(documentId).Add(created);
            document.UpdatedAt = now;
            RecalculateTotals(document);

            return Task.FromResult(new CommercialDocumentLineMutationResponse(
                created.Id,
                documentId,
                true,
                correlationId));
        }
    }

    public Task<CommercialDocumentLineMutationResponse> UpdateLineAsync(
        PortalSessionContext actor,
        string documentId,
        string lineId,
        ValidatedCommercialDocumentLine line,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var document = FindDraftDocument(documentId);
            var current = LinesFor(documentId).FirstOrDefault(
                item => item.Id == lineId);
            if (current is null)
            {
                throw new PortalDataNotFoundException();
            }

            var resolved = CreateLine(documentId, line, DateTime.UtcNow.ToString("O"), lineId);
            var changed =
                current.Label != resolved.Label
                || current.Description != resolved.Description
                || current.Quantity != resolved.Quantity
                || current.UnitLabel != resolved.UnitLabel
                || current.UnitPriceCents != resolved.UnitPriceCents
                || current.TaxRateBasisPoints != resolved.TaxRateBasisPoints
                || current.SortOrder != resolved.SortOrder
                || current.LineTotalCents != resolved.LineTotalCents;
            current.Label = resolved.Label;
            current.Description = resolved.Description;
            current.Quantity = resolved.Quantity;
            current.UnitLabel = resolved.UnitLabel;
            current.UnitPriceCents = resolved.UnitPriceCents;
            current.TaxRateBasisPoints = resolved.TaxRateBasisPoints;
            current.LineTotalCents = resolved.LineTotalCents;
            current.SortOrder = resolved.SortOrder;
            current.UpdatedAt = resolved.UpdatedAt;
            document.UpdatedAt = current.UpdatedAt;
            RecalculateTotals(document);

            return Task.FromResult(new CommercialDocumentLineMutationResponse(
                current.Id,
                documentId,
                changed,
                correlationId));
        }
    }

    public Task<CommercialDocumentMutationResponse> ShareDocumentAsync(
        PortalSessionContext actor,
        string documentId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var current = FindDocument(documentId);
            if (current.Status == CommercialStatuses.Cancelled)
            {
                throw new PortalValidationException();
            }

            if (current.Status == CommercialStatuses.SharedWithCustomer)
            {
                return Task.FromResult(new CommercialDocumentMutationResponse(
                    current.Id,
                    current.InternalReference,
                    current.Status,
                    false,
                    correlationId));
            }

            var now = DateTime.UtcNow.ToString("O");
            current.Status = CommercialStatuses.SharedWithCustomer;
            current.SharedAt ??= now;
            current.UpdatedAt = now;

            return Task.FromResult(new CommercialDocumentMutationResponse(
                current.Id,
                current.InternalReference,
                current.Status,
                true,
                correlationId));
        }
    }

    public Task<CommercialDocumentMutationResponse> CancelDocumentAsync(
        PortalSessionContext actor,
        string documentId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var current = FindDocument(documentId);
            if (current.Status == CommercialStatuses.Cancelled)
            {
                return Task.FromResult(new CommercialDocumentMutationResponse(
                    current.Id,
                    current.InternalReference,
                    current.Status,
                    false,
                    correlationId));
            }

            var now = DateTime.UtcNow.ToString("O");
            current.Status = CommercialStatuses.Cancelled;
            current.CancelledAt = now;
            current.UpdatedAt = now;

            return Task.FromResult(new CommercialDocumentMutationResponse(
                current.Id,
                current.InternalReference,
                current.Status,
                true,
                correlationId));
        }
    }

    public Task<DocumentForIssuing?> GetDocumentForIssuingAsync(
        string documentId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var doc = _store.Documents.FirstOrDefault(
                d => d.Id == documentId);
            if (doc is null)
            {
                return Task.FromResult<DocumentForIssuing?>(null);
            }

            var lines = LinesFor(documentId)
                .OrderBy(l => l.SortOrder)
                .Select(ToLine)
                .ToArray();

            return Task.FromResult<DocumentForIssuing?>(new DocumentForIssuing(
                doc.Id,
                doc.CustomerId,
                doc.CustomerReference,
                doc.CustomerName,
                null,
                null,
                null,
                "FR",
                doc.Title,
                doc.InternalReference,
                doc.Currency,
                doc.TotalAmountCents,
                doc.Status,
                lines));
        }
    }

    public Task MarkDocumentIssuedAsync(
        string documentId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var doc = _store.Documents.FirstOrDefault(d => d.Id == documentId);
            if (doc is not null)
            {
                doc.Status = "issued";
                doc.UpdatedAt = DateTime.UtcNow.ToString("O");
            }
        }

        return Task.CompletedTask;
    }

    public Task MarkDocumentPaidAsync(
        string documentId,
        string correlationId,
        string paymentMethod,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var doc = _store.Documents.FirstOrDefault(d => d.Id == documentId);
            if (doc is not null)
            {
                doc.Status = "paid";
                doc.PaymentMethod = paymentMethod;
                doc.UpdatedAt = DateTime.UtcNow.ToString("O");
            }
        }

        return Task.CompletedTask;
    }

    public Task SetDocumentPaymentMethodAsync(
        string documentId,
        string? paymentMethod,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var doc = _store.Documents.FirstOrDefault(d => d.Id == documentId);
            if (doc is not null)
            {
                doc.PaymentMethod = paymentMethod;
                doc.UpdatedAt = DateTime.UtcNow.ToString("O");
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CommercialDocumentSummary>>
        GetDocumentsForSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            return Task.FromResult<IReadOnlyList<CommercialDocumentSummary>>(
                _store.Documents
                    .Where(document =>
                        _store.SubscriptionLinks.Any(link =>
                            link.DocumentId == document.Id
                            && link.SubscriptionId == subscriptionId))
                    .OrderByDescending(document => document.CreatedAt)
                    .Select(ToDocumentSummary)
                    .ToArray());
        }
    }

    public Task<IReadOnlyList<string>> GetLinkedSubscriptionIdsForDocumentAsync(
        string documentId,
        CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var ids = _store.SubscriptionLinks
                .Where(link => link.DocumentId == documentId)
                .Select(link => link.SubscriptionId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult<IReadOnlyList<string>>(ids);
        }
    }

    private MockCommercialDocument FindDocument(string documentId)
        => _store.Documents.FirstOrDefault(document => document.Id == documentId)
            ?? throw new PortalDataNotFoundException();

    private MockCommercialDocument FindDraftDocument(string documentId)
    {
        var document = FindDocument(documentId);
        if (document.Status != CommercialStatuses.Draft)
        {
            throw new PortalValidationException();
        }

        return document;
    }

    private List<MockCommercialDocumentLine> LinesFor(string documentId)
    {
        if (!_store.Lines.TryGetValue(documentId, out var lines))
        {
            lines = [];
            _store.Lines[documentId] = lines;
        }

        return lines;
    }

    private MockCommercialDocumentLine CreateLine(
        string documentId,
        ValidatedCommercialDocumentLine line,
        string now,
        string? existingId = null)
    {
        var label = line.Label
            ?? throw new PortalValidationException();
        var unitLabel = line.UnitLabel
            ?? throw new PortalValidationException();
        var unitPriceCents = line.UnitPriceCents
            ?? throw new PortalValidationException();
        if (unitPriceCents < 0)
        {
            throw new PortalValidationException();
        }

        var description = line.Description ?? string.Empty;
        var lineTotalCents = (int)decimal.Round(
            line.Quantity * unitPriceCents,
            0,
            MidpointRounding.AwayFromZero);

        return new MockCommercialDocumentLine(
            existingId ?? Guid.NewGuid().ToString("D"),
            documentId,
            label,
            description,
            line.Quantity,
            unitLabel,
            unitPriceCents,
            line.TaxRateBasisPoints,
            lineTotalCents,
            line.SortOrder,
            now,
            now);
    }

    private void RecalculateTotals(MockCommercialDocument document)
    {
        var lines = LinesFor(document.Id);
        document.SubtotalAmountCents = lines.Sum(line => line.LineTotalCents);
        document.TaxAmountCents = lines.Sum(CalculateTaxAmount);
        document.TotalAmountCents =
            document.SubtotalAmountCents + document.TaxAmountCents;
    }

    private static int CalculateTaxAmount(MockCommercialDocumentLine line)
        => FiscalPolicy.CalculateTaxAmount(
            line.LineTotalCents,
            line.TaxRateBasisPoints);

    private static void EnsureKnownCustomer(string? customerReference)
    {
        if (customerReference is null)
        {
            return;
        }

        if (!string.Equals(
                customerReference,
                MockPortalData.Profile.CustomerReference,
                StringComparison.Ordinal))
        {
            throw new PortalValidationException();
        }
    }

    private static void EnsureKnownServiceRequest(string? serviceRequestId)
    {
        if (serviceRequestId is not null
            && !string.Equals(
                serviceRequestId,
                "service-request-mock-001",
                StringComparison.Ordinal))
        {
            throw new PortalValidationException();
        }
    }

    private static string? ResolveServiceRequestReference(string? serviceRequestId)
        => serviceRequestId is null ? null : "SRV-MOCK-ADMIN-001";

    private CommercialDocumentDetail ToClientDetail(MockCommercialDocument document)
        => new(
            document.Id,
            document.DocumentType,
            document.Status,
            document.Title,
            document.InternalReference,
            document.Currency,
            document.SubtotalAmountCents,
            document.TaxAmountCents,
            document.TotalAmountCents,
            document.Disclaimer,
            document.CreatedAt,
            document.UpdatedAt,
            document.SharedAt,
            document.ServiceRequestId,
            document.ServiceRequestReference,
            document.PaymentMethod,
            LinesFor(document.Id)
                .OrderBy(line => line.SortOrder)
                .ThenBy(line => line.CreatedAt)
                .Select(ToLine)
                .ToArray());

    private AdminCommercialDocumentDetail ToAdminDetail(MockCommercialDocument document)
        => new(
            document.Id,
            document.DocumentType,
            document.Status,
            document.Title,
            document.InternalReference,
            document.Currency,
            document.SubtotalAmountCents,
            document.TaxAmountCents,
            document.TotalAmountCents,
            document.Disclaimer,
            document.CreatedAt,
            document.UpdatedAt,
            document.SharedAt,
            document.ServiceRequestId,
            document.ServiceRequestReference,
            document.PaymentMethod,
            document.CustomerReference,
            document.CustomerName,
            document.CreatedByDisplayName,
            LinesFor(document.Id)
                .OrderBy(line => line.SortOrder)
                .ThenBy(line => line.CreatedAt)
                .Select(ToLine)
                .ToArray());

    private static CommercialDocumentSummary ToDocumentSummary(
        MockCommercialDocument document)
        => new(
            document.Id,
            document.DocumentType,
            document.Status,
            document.Title,
            document.InternalReference,
            document.Currency,
            document.SubtotalAmountCents,
            document.TaxAmountCents,
            document.TotalAmountCents,
            document.Disclaimer,
            document.CreatedAt,
            document.UpdatedAt,
            document.SharedAt,
            document.ServiceRequestId,
            document.ServiceRequestReference,
            document.PaymentMethod);

    private static AdminCommercialDocumentSummary ToAdminSummary(
        MockCommercialDocument document)
        => new(
            document.Id,
            document.DocumentType,
            document.Status,
            document.Title,
            document.InternalReference,
            document.Currency,
            document.SubtotalAmountCents,
            document.TaxAmountCents,
            document.TotalAmountCents,
            document.Disclaimer,
            document.CreatedAt,
            document.UpdatedAt,
            document.SharedAt,
            document.ServiceRequestId,
            document.ServiceRequestReference,
            document.PaymentMethod,
            document.CustomerReference,
            document.CustomerName);

    private static CommercialDocumentLine ToLine(MockCommercialDocumentLine line)
    {
        var fiscal = FiscalPolicy.Resolve(
            line.TaxRateBasisPoints,
            DateTime.TryParse(
                line.CreatedAt,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal
                    | System.Globalization.DateTimeStyles.AssumeUniversal,
                out var createdAt)
                ? DateTime.SpecifyKind(createdAt, DateTimeKind.Utc)
                : DateTime.UtcNow);
        return new CommercialDocumentLine(
            line.Id,
            line.Label,
            line.Description,
            line.Quantity,
            line.UnitLabel,
            line.UnitPriceCents,
            fiscal.TaxRateBasisPoints,
            fiscal.FiscalRegime,
            fiscal.FiscalMention,
            line.LineTotalCents,
            line.SortOrder,
            line.CreatedAt,
            line.UpdatedAt);
    }

    private static string CreateReference()
        => $"COM-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..17]
            .ToUpperInvariant();
}

public sealed record MockCommercialDocument(
    string Id,
    string CustomerReference,
    string CustomerName,
    string? InitialServiceRequestId,
    string? InitialServiceRequestReference,
    string InitialDocumentType,
    string InitialStatus,
    string InitialTitle,
    string InternalReference,
    string Currency,
    string InitialDisclaimer,
    string CreatedByDisplayName,
    string CreatedAt,
    string InitialUpdatedAt,
    string? InitialSharedAt,
    string? InitialCancelledAt)
{
    public string CustomerId { get; set; } = "mock-customer";
    public string? Origin { get; set; }
    public string? ServiceRequestId { get; set; } = InitialServiceRequestId;
    public string? ServiceRequestReference { get; set; } =
        InitialServiceRequestReference;
    public string DocumentType { get; set; } = InitialDocumentType;
    public string Status { get; set; } = InitialStatus;
    public string Title { get; set; } = InitialTitle;
    public string Disclaimer { get; set; } = InitialDisclaimer;
    public int SubtotalAmountCents { get; set; }
    public int TaxAmountCents { get; set; }
    public int TotalAmountCents { get; set; }
    public string UpdatedAt { get; set; } = InitialUpdatedAt;
    public string? SharedAt { get; set; } = InitialSharedAt;
    public string? CancelledAt { get; set; } = InitialCancelledAt;
    public string? PaymentMethod { get; set; }
}

public sealed record MockCommercialDocumentSubscriptionLink(
    string DocumentId,
    string SubscriptionId);

public sealed record MockCommercialDocumentLine(
    string Id,
    string DocumentId,
    string InitialLabel,
    string InitialDescription,
    decimal InitialQuantity,
    string InitialUnitLabel,
    int InitialUnitPriceCents,
    int? InitialTaxRateBasisPoints,
    int InitialLineTotalCents,
    int InitialSortOrder,
    string CreatedAt,
    string InitialUpdatedAt)
{
    public string Label { get; set; } = InitialLabel;
    public string Description { get; set; } = InitialDescription;
    public decimal Quantity { get; set; } = InitialQuantity;
    public string UnitLabel { get; set; } = InitialUnitLabel;
    public int UnitPriceCents { get; set; } = InitialUnitPriceCents;
    public int? TaxRateBasisPoints { get; set; } = InitialTaxRateBasisPoints;
    public int LineTotalCents { get; set; } = InitialLineTotalCents;
    public int SortOrder { get; set; } = InitialSortOrder;
    public string UpdatedAt { get; set; } = InitialUpdatedAt;
}
