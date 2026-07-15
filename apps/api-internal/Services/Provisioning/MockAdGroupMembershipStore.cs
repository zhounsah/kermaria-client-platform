namespace Kermaria.ApiInternal.Services.Provisioning;

public sealed class MockAdGroupMembershipStore
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, HashSet<string>> _membersByGroup =
        new(StringComparer.OrdinalIgnoreCase);

    public bool AddMembership(
        string groupSamAccountName,
        string userSamAccountName)
    {
        lock (_syncRoot)
        {
            if (!_membersByGroup.TryGetValue(
                    groupSamAccountName,
                    out var members))
            {
                members = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _membersByGroup[groupSamAccountName] = members;
            }

            return members.Add(userSamAccountName);
        }
    }

    public bool RemoveMembership(
        string groupSamAccountName,
        string userSamAccountName)
    {
        lock (_syncRoot)
        {
            return _membersByGroup.TryGetValue(
                       groupSamAccountName,
                       out var members)
                   && members.Remove(userSamAccountName);
        }
    }

    public IReadOnlyList<string> GetGroupsForUser(string userSamAccountName)
    {
        lock (_syncRoot)
        {
            return _membersByGroup
                .Where(kvp => kvp.Value.Contains(userSamAccountName))
                .Select(kvp => kvp.Key)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
