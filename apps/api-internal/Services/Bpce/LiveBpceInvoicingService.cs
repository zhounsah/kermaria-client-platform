using System.Text.Json.Serialization;
using Kermaria.ApiInternal.Contracts;
using Kermaria.ApiInternal.Data.Configuration;

namespace Kermaria.ApiInternal.Services.Bpce;

public sealed class LiveBpceInvoicingService : IBpceInvoicingService
{
    private const string SendersPath = "/inv/api/v5/senders/";
    private const string CustomersPath = "/inv/api/v5/customers/";
    private const string InvoicesPath = "/inv/api/v5/invoices/";
    private const string InvoiceItemsPath = "/inv/api/v5/invoicesimpleitems/";

    private readonly BpceRuntimeConfiguration _configuration;
    private readonly IBpceTokenCache _tokenCache;
    private readonly IBpceApiClient _apiClient;
    private readonly ILogger<LiveBpceInvoicingService> _logger;

    public LiveBpceInvoicingService(
        BpceRuntimeConfiguration configuration,
        IBpceTokenCache tokenCache,
        IBpceApiClient apiClient,
        ILogger<LiveBpceInvoicingService> logger)
    {
        _configuration = configuration;
        _tokenCache = tokenCache;
        _apiClient = apiClient;
        _logger = logger;
    }

    public string ModeName => _configuration.ModeName;

    public async Task<BpceStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        if (!_configuration.ConfigurationValid)
        {
            return new BpceStatusResponse(
                _configuration.ModeName,
                "unconfigured",
                false,
                false,
                _configuration.BaseUrl,
                _configuration.RequestTimeoutMs);
        }

        try
        {
            _ = await _tokenCache.GetAccessTokenAsync(cancellationToken);
            return new BpceStatusResponse(
                _configuration.ModeName,
                "connected",
                true,
                _configuration.SenderId is not null,
                _configuration.BaseUrl,
                _configuration.RequestTimeoutMs);
        }
        catch (Exception exception)
            when (exception is BpceAuthenticationException
                or HttpRequestException
                or TaskCanceledException)
        {
            _logger.LogWarning(
                exception,
                "BPCE live status probe could not establish a session");
            return new BpceStatusResponse(
                _configuration.ModeName,
                "unreachable",
                _configuration.ConfigurationValid,
                _configuration.SenderId is not null,
                _configuration.BaseUrl,
                _configuration.RequestTimeoutMs);
        }
    }

    public async Task<BpceServiceResult<IReadOnlyList<BpceSenderInfo>>> ListSendersAsync(
        CancellationToken cancellationToken)
    {
        if (!_configuration.ConfigurationValid)
        {
            return UnconfiguredList<BpceSenderInfo>();
        }

        try
        {
            var payload = await _apiClient.GetJsonAsync<BpceSenderListPayload>(
                SendersPath, cancellationToken);
            var senders = (payload?.Results ?? Array.Empty<BpceSenderApiDto>())
                .Select(MapToSenderInfo)
                .ToArray();
            return new BpceServiceResult<IReadOnlyList<BpceSenderInfo>>(
                StatusCodes.Status200OK,
                "BPCE_SENDERS_FOUND",
                "BPCE senders returned.",
                senders);
        }
        catch (Exception ex)
            when (ex is BpceAuthenticationException
                or HttpRequestException
                or TaskCanceledException)
        {
            _logger.LogWarning(ex, "BPCE sender list could not be retrieved");
            return new BpceServiceResult<IReadOnlyList<BpceSenderInfo>>(
                StatusCodes.Status503ServiceUnavailable,
                "BPCE_UNREACHABLE",
                "BPCE invoicing API could not be reached.",
                Array.Empty<BpceSenderInfo>());
        }
    }

    public async Task<BpceServiceResult<BpceSenderInfo>> GetSenderAsync(
        string senderId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(senderId))
        {
            return new BpceServiceResult<BpceSenderInfo>(
                StatusCodes.Status400BadRequest,
                "INVALID_REQUEST",
                "Sender identifier is required.");
        }

        if (!_configuration.ConfigurationValid)
        {
            return new BpceServiceResult<BpceSenderInfo>(
                StatusCodes.Status503ServiceUnavailable,
                "BPCE_UNCONFIGURED",
                "BPCE invoicing API is not configured.");
        }

        try
        {
            var payload = await _apiClient.GetJsonAsync<BpceSenderApiDto>(
                $"{SendersPath}{senderId.Trim()}/", cancellationToken);
            if (payload is null)
            {
                return new BpceServiceResult<BpceSenderInfo>(
                    StatusCodes.Status404NotFound,
                    "BPCE_SENDER_NOT_FOUND",
                    "The requested BPCE sender was not found.");
            }

            return new BpceServiceResult<BpceSenderInfo>(
                StatusCodes.Status200OK,
                "BPCE_SENDER_FOUND",
                "BPCE sender returned.",
                MapToSenderInfo(payload));
        }
        catch (Exception ex)
            when (ex is BpceAuthenticationException
                or HttpRequestException
                or TaskCanceledException)
        {
            _logger.LogWarning(ex, "BPCE sender {SenderId} could not be retrieved", senderId);
            return new BpceServiceResult<BpceSenderInfo>(
                StatusCodes.Status503ServiceUnavailable,
                "BPCE_UNREACHABLE",
                "BPCE invoicing API could not be reached.");
        }
    }

    public async Task<BpceServiceResult<string>> UpsertCustomerAsync(
        string externalReference,
        string displayName,
        string? email,
        string? address,
        string? city,
        string? country,
        CancellationToken cancellationToken)
    {
        if (!_configuration.ConfigurationValid)
        {
            return new BpceServiceResult<string>(
                StatusCodes.Status503ServiceUnavailable,
                "BPCE_UNCONFIGURED",
                "BPCE invoicing API is not configured.");
        }

        try
        {
            var existingId = await GetCustomerByExternalIdAsync(
                externalReference, cancellationToken);
            if (existingId is not null)
            {
                return new BpceServiceResult<string>(
                    StatusCodes.Status200OK,
                    "BPCE_CUSTOMER_FOUND",
                    "BPCE customer already exists.",
                    existingId);
            }

            var payload = BuildCustomerPayload(
                externalReference, displayName, email, address, city, country);

            var created = await _apiClient.PostJsonAsync<BpceCustomerApiDto>(
                CustomersPath, payload, cancellationToken);

            if (created is null)
            {
                return new BpceServiceResult<string>(
                    StatusCodes.Status503ServiceUnavailable,
                    "BPCE_CUSTOMER_CREATE_FAILED",
                    "Failed to create BPCE customer.");
            }

            return new BpceServiceResult<string>(
                StatusCodes.Status201Created,
                "BPCE_CUSTOMER_CREATED",
                "BPCE customer created.",
                created.Id.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
            when (ex is BpceAuthenticationException
                or HttpRequestException
                or TaskCanceledException)
        {
            _logger.LogWarning(ex, "BPCE customer upsert failed");
            return new BpceServiceResult<string>(
                StatusCodes.Status503ServiceUnavailable,
                "BPCE_UNREACHABLE",
                "BPCE invoicing API could not be reached.");
        }
    }

    public async Task<BpceServiceResult<string>> CreateDraftInvoiceAsync(
        string bpceCustomerId,
        string externalReference,
        string title,
        string issueDate,
        IReadOnlyList<BpceInvoiceLineInput> lines,
        CancellationToken cancellationToken)
    {
        if (!_configuration.ConfigurationValid
            || _configuration.SenderId is null)
        {
            return new BpceServiceResult<string>(
                StatusCodes.Status503ServiceUnavailable,
                "BPCE_UNCONFIGURED",
                "BPCE sender ID is not configured.");
        }

        if (!int.TryParse(_configuration.SenderId, out var senderId)
            || !int.TryParse(bpceCustomerId, out var customerId))
        {
            return new BpceServiceResult<string>(
                StatusCodes.Status400BadRequest,
                "INVALID_BPCE_ID",
                "Sender or customer ID is not a valid BPCE integer.");
        }

        try
        {
            var invoicePayload = new
            {
                type = "invoice",
                sender = senderId,
                customer = customerId,
                external_id = externalReference,
                issue_date = issueDate,
                title = title.Length > 30 ? title[..30] : title,
                duedate_method = "DUEDATE_30_DAYS"
            };

            var created = await _apiClient.PostJsonAsync<BpceInvoiceApiDto>(
                InvoicesPath, invoicePayload, cancellationToken);

            if (created is null)
            {
                return new BpceServiceResult<string>(
                    StatusCodes.Status503ServiceUnavailable,
                    "BPCE_INVOICE_CREATE_FAILED",
                    "Failed to create BPCE draft invoice.");
            }

            var invoiceId = created.Id.ToString(
                System.Globalization.CultureInfo.InvariantCulture);

            foreach (var line in lines.OrderBy(l => l.SortOrder))
            {
                var linePayload = new
                {
                    invoice = created.Id,
                    label = line.Label,
                    description = line.Description,
                    quantity = line.Quantity,
                    unit = line.UnitLabel,
                    unit_price = line.UnitPriceEuros,
                    vat_rate = line.TaxRatePercent ?? 0m
                };

                await _apiClient.PostJsonAsync<object>(
                    InvoiceItemsPath, linePayload, cancellationToken);
            }

            return new BpceServiceResult<string>(
                StatusCodes.Status201Created,
                "BPCE_INVOICE_DRAFT_CREATED",
                "BPCE draft invoice created.",
                invoiceId);
        }
        catch (Exception ex)
            when (ex is BpceAuthenticationException
                or HttpRequestException
                or TaskCanceledException)
        {
            _logger.LogWarning(ex, "BPCE draft invoice creation failed");
            return new BpceServiceResult<string>(
                StatusCodes.Status503ServiceUnavailable,
                "BPCE_UNREACHABLE",
                "BPCE invoicing API could not be reached.");
        }
    }

    public async Task<BpceServiceResult<(string? FiscalNumber, string Status)>> ValidateInvoiceAsync(
        string bpceInvoiceId,
        bool sendEmail,
        CancellationToken cancellationToken)
    {
        if (!_configuration.ConfigurationValid)
        {
            return new BpceServiceResult<(string?, string)>(
                StatusCodes.Status503ServiceUnavailable,
                "BPCE_UNCONFIGURED",
                "BPCE invoicing API is not configured.");
        }

        try
        {
            var payload = new
            {
                send_email = sendEmail,
                no_auto_email_reminder = !sendEmail
            };

            var result = await _apiClient.PostJsonAsync<BpceInvoiceApiDto>(
                $"{InvoicesPath}{bpceInvoiceId}/validate/",
                payload,
                cancellationToken);

            if (result is null)
            {
                return new BpceServiceResult<(string?, string)>(
                    StatusCodes.Status503ServiceUnavailable,
                    "BPCE_VALIDATE_FAILED",
                    "BPCE invoice validation failed.");
            }

            return new BpceServiceResult<(string?, string)>(
                StatusCodes.Status200OK,
                "BPCE_INVOICE_VALIDATED",
                "BPCE invoice validated.",
                (result.InvoiceNumber, result.Status ?? "validated"));
        }
        catch (Exception ex)
            when (ex is BpceAuthenticationException
                or HttpRequestException
                or TaskCanceledException)
        {
            _logger.LogWarning(
                ex,
                "BPCE invoice {InvoiceId} validation failed",
                bpceInvoiceId);
            return new BpceServiceResult<(string?, string)>(
                StatusCodes.Status503ServiceUnavailable,
                "BPCE_UNREACHABLE",
                "BPCE invoicing API could not be reached.");
        }
    }

    public async Task<BpceServiceResult<byte[]>> GetInvoicePdfAsync(
        string bpceInvoiceId,
        CancellationToken cancellationToken)
    {
        if (!_configuration.ConfigurationValid)
        {
            return new BpceServiceResult<byte[]>(
                StatusCodes.Status503ServiceUnavailable,
                "BPCE_UNCONFIGURED",
                "BPCE invoicing API is not configured.");
        }

        try
        {
            var pdf = await _apiClient.GetBinaryAsync(
                $"{InvoicesPath}{bpceInvoiceId}/pdf/",
                cancellationToken);

            if (pdf is null || pdf.Length == 0)
            {
                return new BpceServiceResult<byte[]>(
                    StatusCodes.Status404NotFound,
                    "BPCE_PDF_NOT_FOUND",
                    "BPCE PDF not available yet.");
            }

            return new BpceServiceResult<byte[]>(
                StatusCodes.Status200OK,
                "BPCE_PDF_RETRIEVED",
                "BPCE PDF retrieved.",
                pdf);
        }
        catch (Exception ex)
            when (ex is BpceAuthenticationException
                or HttpRequestException
                or TaskCanceledException)
        {
            _logger.LogWarning(
                ex,
                "BPCE PDF for invoice {InvoiceId} failed",
                bpceInvoiceId);
            return new BpceServiceResult<byte[]>(
                StatusCodes.Status503ServiceUnavailable,
                "BPCE_UNREACHABLE",
                "BPCE invoicing API could not be reached.");
        }
    }

    private static Dictionary<string, object?> BuildCustomerPayload(
        string externalReference,
        string displayName,
        string? email,
        string? address,
        string? city,
        string? country)
    {
        var payload = new Dictionary<string, object?>
        {
            ["external_id"] = externalReference,
            ["name"] = displayName,
            ["is_legal_entity"] = true,
            ["country"] = "FR"
        };

        if (!string.IsNullOrWhiteSpace(email))
        {
            payload["email"] = email;
        }

        if (!string.IsNullOrWhiteSpace(address))
        {
            payload["address"] = address;
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            payload["city"] = city;
        }

        return payload;
    }

    private async Task<string?> GetCustomerByExternalIdAsync(
        string externalReference,
        CancellationToken cancellationToken)
    {
        var payload = await _apiClient.GetJsonAsync<BpceCustomerListPayload>(
            $"{CustomersPath}search_list/?external_id={Uri.EscapeDataString(externalReference)}",
            cancellationToken);
        var results = payload?.Results;
        if (results is null || results.Count == 0)
        {
            return null;
        }

        return results[0].Id.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static BpceServiceResult<IReadOnlyList<T>> UnconfiguredList<T>()
        => new(
            StatusCodes.Status503ServiceUnavailable,
            "BPCE_UNCONFIGURED",
            "BPCE invoicing API is not configured.",
            Array.Empty<T>());

    private static BpceSenderInfo MapToSenderInfo(BpceSenderApiDto dto)
        => new(
            Id: dto.Id.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            Name: dto.Name,
            ProfileName: dto.ProfileName,
            Siren: dto.Siren,
            Siret: dto.Siret,
            Email: dto.Email,
            Country: dto.Country,
            Locale: dto.Locale,
            IsDefault: dto.IsDefault,
            IsArchived: dto.IsArchived);

    private sealed record BpceSenderListPayload(
        [property: JsonPropertyName("count")] int? Count,
        [property: JsonPropertyName("results")]
            IReadOnlyList<BpceSenderApiDto>? Results);

    private sealed record BpceSenderApiDto(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("profile_name")] string? ProfileName,
        [property: JsonPropertyName("siren")] string? Siren,
        [property: JsonPropertyName("siret")] string? Siret,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("country")] string? Country,
        [property: JsonPropertyName("locale")] string? Locale,
        [property: JsonPropertyName("is_default")] bool IsDefault,
        [property: JsonPropertyName("is_archived")] bool IsArchived);

    private sealed record BpceCustomerListPayload(
        [property: JsonPropertyName("count")] int? Count,
        [property: JsonPropertyName("results")]
            IReadOnlyList<BpceCustomerApiDto>? Results);

    private sealed record BpceCustomerApiDto(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("external_id")] string? ExternalId);

    private sealed record BpceInvoiceApiDto(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("invoice_number")] string? InvoiceNumber,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("draft")] bool? Draft);
}
