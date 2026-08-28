using System.Text.Json;

namespace Kermaria.ApiInternal.Contracts;

public sealed record ApplicationSettingItem(
    string Key,
    string Category,
    string Label,
    string Description,
    string ValueType,
    JsonElement Value,
    string Classification,
    string Risk,
    bool Editable,
    bool RestartRequired,
    bool Sensitive,
    string Source,
    int Version,
    string? UpdatedAt);

public sealed record ApplicationSettingsSnapshot(
    IReadOnlyList<ApplicationSettingItem> Settings,
    bool Persistent);

public sealed record ApplicationSettingUpdateRequest(
    JsonElement Value,
    int ExpectedVersion);

public sealed record ApplicationSettingMutationResponse(
    string Code,
    string Message,
    ApplicationSettingItem? Setting,
    string CorrelationId);

public sealed record PortalBillingConfiguration(string? Iban, string? Bic, string? PaypalUrl, string TransferLabel);
