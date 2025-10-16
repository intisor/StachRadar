using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace StackRadar.Core.Scouting;

public sealed class LinkedInSource : IDomainSource
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LinkedInSource> _logger;

    public LinkedInSource(IHttpClientFactory httpClientFactory, ILogger<LinkedInSource> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "linkedin";

    public async IAsyncEnumerable<DomainCandidate> FetchAsync(DomainSourceRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("scout");
        var totalYielded = 0;
        var limit = request.Limit;

        // If no specific query, try to read from discovered.txt and enrich with LinkedIn data
        if (string.IsNullOrWhiteSpace(request.Query) || request.Query == "ASP.NET Nigeria")
        {
            await foreach (var candidate in EnrichFromDiscoveredDomains(client, limit, cancellationToken))
            {
                yield return candidate;
                totalYielded++;
                if (limit.HasValue && totalYielded >= limit.Value)
                    yield break;
            }
        }
        else
        {
            // Original search functionality
            var searchUrl = $"https://www.linkedin.com/search/results/companies/?keywords={Uri.EscapeDataString(request.Query)}";

            string html;
            try
            {
                using var response = await client.GetAsync(searchUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("LinkedIn search responded with {StatusCode}", response.StatusCode);
                    yield break;
                }

                html = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching from LinkedIn");
                yield break;
            }

            var companies = ExtractCompaniesFromHtml(html);

            foreach (var company in companies)
            {
                if (string.IsNullOrWhiteSpace(company.Website))
                    continue;

                var domain = ExtractDomain(company.Website);
                if (string.IsNullOrWhiteSpace(domain))
                    continue;

                var metadata = new Dictionary<string, string>
                {
                    ["companyName"] = company.Name,
                    ["description"] = company.Description,
                    ["location"] = company.Location,
                    ["linkedinUrl"] = company.LinkedInUrl,
                    ["website"] = company.Website
                };

                yield return DomainCandidate.Create(domain, Name, 0.6, metadata);

                totalYielded++;
                if (limit.HasValue && totalYielded >= limit.Value)
                    yield break;
            }
        }
    }

    private async IAsyncEnumerable<DomainCandidate> EnrichFromDiscoveredDomains(HttpClient client, int? limit, [EnumeratorCancellation] CancellationToken cancellationToken)
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
                // Search LinkedIn for this company
                var linkedinUrl = $"https://www.linkedin.com/search/results/companies/?keywords={Uri.EscapeDataString(companyName + " Nigeria")}";
                
                string html;
                try
                {
                    using var response = await client.GetAsync(linkedinUrl, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        html = await response.Content.ReadAsStringAsync(cancellationToken);
                        
                        // Check if LinkedIn returned a login page instead of search results
                        if (html.Contains("LinkedIn Login") || html.Contains("Sign in"))
                        {
                            _logger.LogWarning("LinkedIn returned login page for {Company}. LinkedIn requires authentication for search.", companyName);
                            continue;
                        }
                    }
                    else
                    {
                        continue; // Skip this company if search fails
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error searching LinkedIn for {Company}", companyName);
                    continue; // Skip this company
                }

                var linkedinProfile = ExtractLinkedInProfileFromHtml(html, companyName);
                
                if (!string.IsNullOrWhiteSpace(linkedinProfile))
                {
                    var metadata = new Dictionary<string, string>
                    {
                        ["originalDomain"] = domain,
                        ["companyName"] = companyName,
                        ["linkedinUrl"] = linkedinProfile,
                        ["enriched"] = "true"
                    };

                    yield return DomainCandidate.Create(domain, Name, 0.7, metadata);
                    processedCount++;

                    if (limit.HasValue && processedCount >= limit.Value)
                        yield break;
                }
            }

            // Add delay to avoid rate limiting
            await Task.Delay(1000, cancellationToken);
        }

        _logger.LogInformation("Enriched {Count} domains with LinkedIn data", processedCount);
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

    private static string? ExtractLinkedInProfileFromHtml(string html, string companyName)
    {
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);

        // Look for LinkedIn company profile links
        var links = doc.DocumentNode.SelectNodes("//a[contains(@href, '/company/')]");
        if (links != null)
        {
            foreach (var link in links)
            {
                var href = link.GetAttributeValue("href", "");
                if (href.Contains("/company/") && !href.Contains("/search/"))
                {
                    // Check if this looks like a company profile URL
                    if (System.Text.RegularExpressions.Regex.IsMatch(href, @"/company/[^/]+/?$"))
                    {
                        return "https://www.linkedin.com" + href;
                    }
                }
            }
        }

        return null;
    }

    private static IReadOnlyList<LinkedInCompany> ExtractCompaniesFromHtml(string html)
    {
        var companies = new List<LinkedInCompany>();
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);

        // Look for company result cards
        var companyNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'entity-result__item')]");
        if (companyNodes != null)
        {
            foreach (var node in companyNodes)
            {
                var company = new LinkedInCompany();

                // Extract company name
                var nameNode = node.SelectSingleNode(".//span[contains(@class, 'entity-result__title-text')]");
                company.Name = nameNode?.InnerText.Trim() ?? string.Empty;

                // Extract LinkedIn URL
                var linkNode = node.SelectSingleNode(".//a[contains(@class, 'entity-result__title-text')]");
                company.LinkedInUrl = linkNode?.GetAttributeValue("href", "") ?? string.Empty;

                // Extract description
                var descNode = node.SelectSingleNode(".//p[contains(@class, 'entity-result__summary')]");
                company.Description = descNode?.InnerText.Trim() ?? string.Empty;

                // Extract location
                var locNode = node.SelectSingleNode(".//div[contains(@class, 'entity-result__secondary-subtitle')]");
                company.Location = locNode?.InnerText.Trim() ?? string.Empty;

                // Try to extract website from description or other fields
                if (!string.IsNullOrWhiteSpace(company.Description))
                {
                    var websiteMatch = System.Text.RegularExpressions.Regex.Match(
                        company.Description,
                        @"(?:https?://)?([a-zA-Z0-9-]+\.[a-zA-Z]{2,})",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    if (websiteMatch.Success)
                    {
                        company.Website = websiteMatch.Groups[1].Value.ToLowerInvariant();
                    }
                }

                if (!string.IsNullOrWhiteSpace(company.Name) && !string.IsNullOrWhiteSpace(company.LinkedInUrl))
                {
                    companies.Add(company);
                }
            }
        }

        return companies;
    }

    private static string? ExtractDomain(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.StartsWith("http") ? url : $"https://{url}", UriKind.Absolute, out var uri))
            return null;
        return uri.Host.ToLowerInvariant();
    }

    private sealed record LinkedInCompany
    {
        public string Name { get; set; } = string.Empty;
        public string LinkedInUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
    }
}