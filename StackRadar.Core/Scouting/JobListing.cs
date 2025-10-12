using System.Collections.ObjectModel;

namespace StackRadar.Core.Scouting;

public sealed record JobListing(
    string Title,
    string Company,
    string Location,
    string Description,
    string Url,
    string Source,
    DateTimeOffset RetrievedAt)
{
    public static JobListing Create(
        string title,
        string company,
        string location,
        string description,
        string url,
        string source,
        DateTimeOffset? retrievedAt = null)
    {
        var safeTitle = title?.Trim() ?? throw new ArgumentNullException(nameof(title));
        var safeCompany = company?.Trim() ?? throw new ArgumentNullException(nameof(company));
        var safeLocation = location?.Trim() ?? string.Empty;
        var safeDescription = description?.Trim() ?? string.Empty;
        var safeUrl = url?.Trim() ?? throw new ArgumentNullException(nameof(url));

        if (string.IsNullOrWhiteSpace(safeTitle))
        {
            throw new ArgumentException("Title cannot be empty", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(safeCompany))
        {
            throw new ArgumentException("Company cannot be empty", nameof(company));
        }

        return new JobListing(
            safeTitle,
            safeCompany,
            safeLocation,
            safeDescription,
            safeUrl,
            source,
            retrievedAt ?? DateTimeOffset.UtcNow);
    }

    public string GenerateCompanyLinkedInUrl()
    {
        var encodedCompany = Uri.EscapeDataString(Company);
        return $"https://www.linkedin.com/search/results/companies/?keywords={encodedCompany}";
    }

    public string GeneratePeopleLinkedInUrl()
    {
        var encodedCompany = Uri.EscapeDataString(Company);
        return $"https://www.linkedin.com/search/results/people/?keywords={encodedCompany}";
    }
}
