using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using StackRadar.Core.Models;
using StackRadar.Core.Detection;

namespace StackRadar.Core.Scraping;

/// <summary>
/// Full-featured web scraper that:
/// 1. Fetches website content (HTML)
/// 2. Extracts structured data (company info, contact, tech stack)
/// 3. Enriches with AI analysis (Gemma)
/// 4. Detects ASP.NET technology stack
/// </summary>
public sealed class FullWebScraper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FullWebScraper> _logger;
    private readonly WebContentExtractor _contentExtractor;
    private readonly GemmaAiEnricher? _aiEnricher;

    public FullWebScraper(
        HttpClient httpClient,
        ILogger<FullWebScraper> logger,
        WebContentExtractor contentExtractor,
        GemmaAiEnricher? aiEnricher = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _contentExtractor = contentExtractor ?? throw new ArgumentNullException(nameof(contentExtractor));
        _aiEnricher = aiEnricher;
    }

    /// <summary>
    /// Scrape a domain and return comprehensive enriched information
    /// </summary>
    public async Task<ScrapedWebsiteData?> ScrapeAsync(string domain, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = domain.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? domain
                : $"https://{domain}";

            _logger.LogInformation("Scraping {Url}", url);

            // Fetch HTML
            var html = await FetchHtmlAsync(url, cancellationToken);
            if (string.IsNullOrEmpty(html))
            {
                _logger.LogWarning("Failed to fetch HTML from {Url}", url);
                return null;
            }

            // Extract structured content
            var content = _contentExtractor.Extract(html, domain);

            // Enrich with AI if available
            AiEnrichedContent? aiContent = null;
            if (_aiEnricher != null)
            {
                aiContent = await _aiEnricher.AnalyzeContentAsync(content, cancellationToken);
            }

            var result = new ScrapedWebsiteData
            {
                Domain = domain,
                Url = url,
                FetchedAt = DateTime.UtcNow,
                ExtractedContent = content,
                AiAnalysis = aiContent,
                IsSuccessful = true
            };

            _logger.LogInformation("Successfully scraped {Domain}", domain);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping {Domain}", domain);
            return new ScrapedWebsiteData
            {
                Domain = domain,
                IsSuccessful = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Scrape multiple domains with concurrency control.
    /// Uses Parallel.ForEachAsync for cleaner, modern concurrency handling.
    /// Default maxConcurrency = 3 is optimized for Latitude E6440 (2 cores / 4 threads).
    /// </summary>
    public async IAsyncEnumerable<ScrapedWebsiteData> ScrapeMultipleAsync(
        IEnumerable<string> domains,
        int maxConcurrency = 3, // Optimized for Dual Core E6440
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Use ConcurrentBag for thread-safe collection
        var results = new System.Collections.Concurrent.ConcurrentBag<ScrapedWebsiteData>();

        // Use the modern Parallel.ForEachAsync (much cleaner)
        await Parallel.ForEachAsync(domains, new ParallelOptions
        {
            MaxDegreeOfParallelism = maxConcurrency,
            CancellationToken = cancellationToken
        }, async (domain, ct) =>
        {
            var data = await ScrapeAsync(domain, ct);
            if (data != null)
                results.Add(data);
        });

        foreach (var r in results)
        {
            yield return r;
        }
    }

    private async Task<string?> FetchHtmlAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            var response = await _httpClient.GetAsync(url, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("HTTP {StatusCode} from {Url}", response.StatusCode, url);
                return null;
            }

            var html = await response.Content.ReadAsStringAsync(cts.Token);
            return string.IsNullOrWhiteSpace(html) ? null : html;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Request timeout for {Url}", url);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Request error for {Url}", url);
            return null;
        }
    }
}

/// <summary>
/// Complete result of scraping and analyzing a website
/// </summary>
public sealed class ScrapedWebsiteData
{
    public string Domain { get; set; } = string.Empty;
    public string? Url { get; set; }
    public DateTime FetchedAt { get; set; }
    public bool IsSuccessful { get; set; }
    public string? Error { get; set; }
    public ExtractedWebContent? ExtractedContent { get; set; }
    public AiEnrichedContent? AiAnalysis { get; set; }

    /// <summary>
    /// Convert scraped data to enrichment evidence for detection engine
    /// </summary>
    public IEnumerable<DetectionEvidence> ToDetectionEvidence()
    {
        if (!IsSuccessful || ExtractedContent == null)
            yield break;

        // Add tech stack evidence
        foreach (var tech in ExtractedContent.TechStack)
        {
            if (tech.Framework.Contains("ASP.NET", StringComparison.OrdinalIgnoreCase) ||
                tech.Framework.Contains("Razor", StringComparison.OrdinalIgnoreCase))
            {
                yield return new DetectionEvidence(
                    DetectionSignal.HtmlRazorDirective,
                    $"ASP.NET technology detected: {tech.Framework}",
                    tech.Framework,
                    8 // Scale: 0-10
                );
            }
        }

        // Add company quality indicators
        if (ExtractedContent.EmailAddresses.Count > 0)
        {
            yield return new DetectionEvidence(
                DetectionSignal.HtmlViewState,
                $"Professional contact info found ({ExtractedContent.EmailAddresses.Count} emails)",
                string.Join(",", ExtractedContent.EmailAddresses),
                3 // Scale: 0-10
            );
        }

        // Add social proof
        if (ExtractedContent.SocialLinks.Count > 2)
        {
            yield return new DetectionEvidence(
                DetectionSignal.HtmlCshtmlReference,
                $"Professional presence ({ExtractedContent.SocialLinks.Count} social links)",
                string.Join(",", ExtractedContent.SocialLinks.Keys),
                2 // Scale: 0-10
            );
        }
    }
}
