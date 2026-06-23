namespace Kermaria.ApiInternal.Contracts;

public sealed record BpceStatusResponse(
    string Mode,
    string Status,
    bool ConfigurationValid,
    bool SenderConfigured,
    string BaseUrl,
    int RequestTimeoutMs);
