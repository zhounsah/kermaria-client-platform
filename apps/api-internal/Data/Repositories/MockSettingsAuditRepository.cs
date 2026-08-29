using Kermaria.ApiInternal.Data.Configuration;
using Kermaria.ApiInternal.Services;

namespace Kermaria.ApiInternal.Data.Repositories;

/// <summary>
/// Journal d'audit en memoire, alimente par la persistance mock.
///
/// Un journal vide en developpement serait trompeur : on ne saurait pas
/// distinguer « aucune mutation » de « la page ne lit pas le bon journal ».
/// Les evenements sont donc conserves le temps du processus, et rien au-dela :
/// hors developpement, c'est MariaDB qui repond.
/// </summary>
public sealed class MockSettingsAuditRepository : ISettingsAuditRepository
{
    private const int Capacity = 500;
    private static readonly List<SettingsAuditEntry> Entries = [];
    private static readonly object Gate = new();

    public bool IsPersistent => false;

    public static void Append(AuditEvent auditEvent)
    {
        if (SettingsAuditRegistry.Find(auditEvent.Action) is null)
        {
            return;
        }

        var entry = new SettingsAuditEntry(
            DateTime.UtcNow.ToString("O"),
            auditEvent.ActorUserId ?? "API-INTERNAL",
            auditEvent.Action,
            auditEvent.Outcome,
            auditEvent.ReasonCode,
            auditEvent.TargetType,
            auditEvent.TargetReference,
            auditEvent.CorrelationId,
            MariaDbAddressMask.Apply(auditEvent.SourceAddress));

        lock (Gate)
        {
            Entries.Add(entry);
            if (Entries.Count > Capacity)
            {
                Entries.RemoveRange(0, Entries.Count - Capacity);
            }
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            Entries.Clear();
        }
    }

    public Task<IReadOnlyList<SettingsAuditEntry>> SearchAsync(
        SettingsAuditQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Actions.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<SettingsAuditEntry>>([]);
        }

        var actions = new HashSet<string>(query.Actions, StringComparer.Ordinal);
        List<SettingsAuditEntry> snapshot;
        lock (Gate)
        {
            snapshot = [.. Entries];
        }

        var matches = snapshot
            .Where(entry => actions.Contains(entry.Action))
            .Where(entry => Matches(entry, query))
            .OrderByDescending(entry => entry.OccurredAt, StringComparer.Ordinal)
            .Take(Math.Clamp(query.Limit, 1, Capacity))
            .ToArray();

        return Task.FromResult<IReadOnlyList<SettingsAuditEntry>>(matches);
    }

    private static bool Matches(SettingsAuditEntry entry, SettingsAuditQuery query)
    {
        if (!DateTime.TryParse(
                entry.OccurredAt,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var occurredAt))
        {
            return false;
        }

        if (query.FromUtc is not null && occurredAt < query.FromUtc.Value)
        {
            return false;
        }

        if (query.ToUtc is not null && occurredAt > query.ToUtc.Value)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.Outcome)
            && !string.Equals(entry.Outcome, query.Outcome, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.CorrelationId)
            && !string.Equals(
                entry.CorrelationId,
                query.CorrelationId,
                StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.TargetReference)
            && (entry.TargetReference is null
                || !entry.TargetReference.Contains(
                    query.TargetReference,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.Actor)
            && !entry.Actor.Contains(query.Actor, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
