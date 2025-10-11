using System.Collections.ObjectModel;

namespace StackRadar.Core.Scouting;

public sealed record DomainCandidate(
    string Domain,
    string Source,
    double Confidence,
    IReadOnlyDictionary<string, string> Metadata,
    DateTimeOffset RetrievedAt)
{
    public static DomainCandidate Create(
        string domain,
        string source,
        double confidence,
        IDictionary<string, string>? metadata = null,
        DateTimeOffset? retrievedAt = null)
    {
        var safeDomain = domain?.Trim()?.ToLowerInvariant() ?? throw new ArgumentNullException(nameof(domain));
        if (string.IsNullOrWhiteSpace(safeDomain))
        {
            throw new ArgumentException("Domain cannot be empty", nameof(domain));
        }

        var info = metadata is null
            ? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>())
            : new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase));

        return new DomainCandidate(
            safeDomain,
            source,
            confidence,
            info,
            retrievedAt ?? DateTimeOffset.UtcNow);
    }
}
