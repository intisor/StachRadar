using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
// using Microsoft.Playwright; // Commented out until package is properly installed

namespace StackRadar.Core.Scouting;

public sealed class PlaywrightLinkedInSource : IDomainSource
{
    private readonly ILogger<PlaywrightLinkedInSource> _logger;
    private readonly HttpClient _httpClient;

    public PlaywrightLinkedInSource(ILogger<PlaywrightLinkedInSource> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.5");
    }

    public string Name => "playwright-linkedin";

    public async IAsyncEnumerable<DomainCandidate> FetchAsync(DomainSourceRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var totalYielded = 0;
        var limit = request.Limit;

        // If no specific query, try to read from discovered.txt and enrich with LinkedIn data
        if (string.IsNullOrWhiteSpace(request.Query) || request.Query == "ASP.NET Nigeria")
        {
            await foreach (var candidate in EnrichFromDiscoveredDomains(limit, cancellationToken))
            {
                yield return candidate;
                totalYielded++;
                if (limit.HasValue && totalYielded >= limit.Value)
                    yield break;
            }
        }
        else
        {
            // Search functionality using HTTP requests
            await foreach (var candidate in SearchLinkedIn(request.Query, limit, cancellationToken))
            {
                yield return candidate;
                totalYielded++;
                if (limit.HasValue && totalYielded >= limit.Value)
                    yield break;
            }
        }
    }

    private async IAsyncEnumerable<DomainCandidate> EnrichFromDiscoveredDomains(int? limit, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Read domains from discovered.txt
        var discoveredFile = "discovered.txt";
        if (!File.Exists(discoveredFile))
        {
            _logger.LogWarning("No discovered.txt file found for LinkedIn enrichment");
            yield break;
        }

        var domains = await File.ReadAllLinesAsync(discoveredFile, cancellationToken);
        var processedCount = 0;

        foreach (var domainLine in domains)
        {
            if (string.IsNullOrWhiteSpace(domainLine))
                continue;

            var url = domainLine.Trim();
            if (!url.StartsWith("https://"))
                continue;

            var domain = url.Replace("https://", "").Trim('/');

            // Try to extract company name from domain (remove .ng, .com.ng, etc.)
            var companyName = ExtractCompanyNameFromDomain(domain);

            if (!string.IsNullOrWhiteSpace(companyName))
            {
                // Try to find LinkedIn profile using HTTP requests
                var linkedinProfile = await SearchLinkedInHttp(companyName, cancellationToken);

                if (!string.IsNullOrWhiteSpace(linkedinProfile))
                {
                    var metadata = new Dictionary<string, string>
                    {
                        ["originalDomain"] = domain,
                        ["companyName"] = companyName,
                        ["linkedinUrl"] = linkedinProfile,
                        ["enriched"] = "true",
                        ["method"] = "http-fallback"
                    };

                    yield return DomainCandidate.Create(domain, Name, 0.8, metadata);
                    processedCount++;
                }
                else
                {
                    // If HTTP search fails, create candidate with manual lookup suggestion
                    var metadata = new Dictionary<string, string>
                    {
                        ["originalDomain"] = domain,
                        ["companyName"] = companyName,
                        ["needsManualLookup"] = "true",
                        ["suggestedSearch"] = $"{companyName} Nigeria site:linkedin.com/company",
                        ["method"] = "manual-required"
                    };

                    yield return DomainCandidate.Create(domain, Name, 0.5, metadata);
                    processedCount++;
                }

                if (limit.HasValue && processedCount >= limit.Value)
                    yield break;
            }

            // Add delay to avoid rate limiting
            await Task.Delay(2000, cancellationToken);
        }

        _logger.LogInformation("Processed {Count} domains for LinkedIn enrichment", processedCount);
    }

    private async IAsyncEnumerable<DomainCandidate> SearchLinkedIn(string query, int? limit, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var linkedinProfile = await SearchLinkedInHttp(query, cancellationToken);

        if (!string.IsNullOrWhiteSpace(linkedinProfile))
        {
            var metadata = new Dictionary<string, string>
            {
                ["companyName"] = query,
                ["linkedinUrl"] = linkedinProfile,
                ["method"] = "http-search"
            };

            yield return DomainCandidate.Create($"search-{query.Replace(" ", "-")}", Name, 0.6, metadata);
        }
        else
        {
            // If search fails, suggest manual lookup
            var metadata = new Dictionary<string, string>
            {
                ["companyName"] = query,
                ["needsManualLookup"] = "true",
                ["suggestedSearch"] = $"{query} Nigeria site:linkedin.com/company",
                ["method"] = "manual-required"
            };

            yield return DomainCandidate.Create($"search-{query.Replace(" ", "-")}", Name, 0.3, metadata);
        }
    }

    private async Task<string?> SearchLinkedInHttp(string companyName, CancellationToken cancellationToken)
    {
        try
        {
            // Try direct company search on LinkedIn
            var searchUrl = $"https://www.linkedin.com/search/results/companies/?keywords={Uri.EscapeDataString(companyName + " Nigeria")}";
            _logger.LogInformation("Searching LinkedIn via HTTP: {Url}", searchUrl);

            var response = await _httpClient.GetAsync(searchUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            // Look for company profile links in the HTML
            var companyLinks = ExtractCompanyLinksFromHtml(html);

            if (companyLinks.Any())
            {
                var bestMatch = companyLinks.First();
                _logger.LogInformation("Found LinkedIn profile via HTTP: {Url}", bestMatch);
                return bestMatch;
            }

            _logger.LogWarning("No LinkedIn company profiles found via HTTP for: {Company}", companyName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching LinkedIn via HTTP for {Company}", companyName);
            return null;
        }
    }

    private static List<string> ExtractCompanyLinksFromHtml(string html)
    {
        var links = new List<string>();
        var regex = new System.Text.RegularExpressions.Regex(@"href=""(https://www\.linkedin\.com/company/[^""]+)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var matches = regex.Matches(html);
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var url = match.Groups[1].Value;
            // Ensure it's a clean company profile URL
            if (System.Text.RegularExpressions.Regex.IsMatch(url, @"linkedin\.com/company/[^/]+/?$"))
            {
                links.Add(url);
            }
        }

        return links.Distinct().ToList();
    }

    private static string ExtractCompanyNameFromDomain(string domain)
    {
        // Remove common TLDs
        var name = domain
            .Replace(".com.ng", "")
            .Replace(".edu.ng", "")
            .Replace(".gov.ng", "")
            .Replace(".org.ng", "")
            .Replace(".ng", "")
            .Replace(".com", "")
            .Replace("www.", "");

        // Convert kebab-case and snake_case to Title Case
        name = System.Text.RegularExpressions.Regex.Replace(name, @"[-_]", " ");
        name = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower());

        return name.Trim();
    }
}