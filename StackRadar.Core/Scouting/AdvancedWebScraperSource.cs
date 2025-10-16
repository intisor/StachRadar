using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using StackRadar.Core.Models;
using StackRadar.Core.Scraping;

namespace StackRadar.Core.Scouting;

/// <summary>
/// Advanced domain source that performs full web scraping with AI enrichment.
/// Fetches HTML, extracts structured data, and uses Gemma for intelligent analysis.
/// </summary>
public sealed class AdvancedWebScraperSource : IDomainSource
{
    private readonly FullWebScraper _scraper;
    private readonly ILogger<AdvancedWebScraperSource> _logger;

    public AdvancedWebScraperSource(FullWebScraper scraper, ILogger<AdvancedWebScraperSource> logger)
    {
        _scraper = scraper ?? throw new ArgumentNullException(nameof(scraper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "fullscrape";

    public async IAsyncEnumerable<DomainCandidate> FetchAsync(
        DomainSourceRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // If no query, try to read from discovered.txt and scrape each domain
        if (string.IsNullOrWhiteSpace(request.Query) || request.Query == "ASP.NET Nigeria")
        {
            await foreach (var candidate in ScrapeDiscoveredDomainsAsync(request.Limit, cancellationToken))
            {
                yield return candidate;
            }
        }
        else
        {
            // Direct domain scraping
            var scrapedData = await _scraper.ScrapeAsync(request.Query, cancellationToken);
            if (scrapedData?.IsSuccessful == true)
            {
                yield return ConvertToCandidate(scrapedData);
            }
        }
    }

    private async IAsyncEnumerable<DomainCandidate> ScrapeDiscoveredDomainsAsync(
        int? limit,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var discoveredFile = "discovered.txt";
        if (!File.Exists(discoveredFile))
        {
            _logger.LogWarning("discovered.txt not found for full scraping");
            yield break;
        }

        var domains = await File.ReadAllLinesAsync(discoveredFile, cancellationToken);
        var validDomains = domains
            .Where(line => !string.IsNullOrWhiteSpace(line) && line.StartsWith("https://"))
            .Select(line => line.Trim().Replace("https://", "").Trim('/'))
            .Take(limit ?? 100)
            .ToList();

        _logger.LogInformation("Starting full web scraping for {Count} domains", validDomains.Count);

        var processedCount = 0;
        await foreach (var scrapedData in _scraper.ScrapeMultipleAsync(validDomains, maxConcurrency: 3, cancellationToken))
        {
            yield return ConvertToCandidate(scrapedData);
            processedCount++;

            if (limit.HasValue && processedCount >= limit.Value)
                yield break;
        }

        _logger.LogInformation("Completed scraping {Count} domains", processedCount);
    }

    private DomainCandidate ConvertToCandidate(ScrapedWebsiteData scrapedData)
    {
        var confidence = 0.75; // High confidence for full scraping
        var metadata = new Dictionary<string, string>();

        // Add extracted content
        if (scrapedData.ExtractedContent != null)
        {
            var content = scrapedData.ExtractedContent;

            if (!string.IsNullOrEmpty(content.CompanyName))
                metadata["companyName"] = content.CompanyName;

            if (!string.IsNullOrEmpty(content.Title))
                metadata["title"] = content.Title;

            if (!string.IsNullOrEmpty(content.CompanyDescription))
                metadata["description"] = content.CompanyDescription;

            if (content.EmailAddresses.Any())
                metadata["emails"] = string.Join(", ", content.EmailAddresses);

            if (content.PhoneNumbers.Any())
                metadata["phones"] = string.Join(", ", content.PhoneNumbers);

            if (content.SocialLinks.Any())
                metadata["socialLinks"] = string.Join(", ", content.SocialLinks.Values);

            if (content.TechStack.Any())
                metadata["techStack"] = string.Join(", ", content.TechStack.Select(t => t.Framework));

            // Boost confidence if ASP.NET detected
            var hasAspNet = content.TechStack.Any(t =>
                t.Framework.Contains("ASP.NET", StringComparison.OrdinalIgnoreCase) ||
                t.Framework.Contains("Razor", StringComparison.OrdinalIgnoreCase));

            if (hasAspNet)
            {
                metadata["aspnetDetected"] = "true";
                confidence = 0.95;
            }
        }

        // Add AI analysis
        if (scrapedData.AiAnalysis != null)
        {
            var ai = scrapedData.AiAnalysis;

            if (!string.IsNullOrEmpty(ai.BusinessType))
                metadata["businessType"] = ai.BusinessType;

            if (!string.IsNullOrEmpty(ai.Industry))
                metadata["industry"] = ai.Industry;

            if (ai.Services.Any())
                metadata["services"] = string.Join(", ", ai.Services);

            if (!string.IsNullOrEmpty(ai.CompetencyLevel))
                metadata["competencyLevel"] = ai.CompetencyLevel;

            if (!string.IsNullOrEmpty(ai.DigitalMaturity))
                metadata["digitalMaturity"] = ai.DigitalMaturity;

            if (ai.KeyFindings.Any())
                metadata["keyFindings"] = string.Join(" | ", ai.KeyFindings);

            if (ai.RecommendedActions.Any())
                metadata["recommendations"] = string.Join(" | ", ai.RecommendedActions);
        }

        metadata["method"] = "fullscrape";
        metadata["scrapedAt"] = scrapedData.FetchedAt.ToString("O");

        return DomainCandidate.Create(
            scrapedData.Domain,
            Name,
            confidence,
            metadata
        );
    }
}
