using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace StackRadar.Core.Scouting;

/// <summary>
/// GoogleDorkSource using Playwright to scrape Google SERP directly.
/// This replaces the paid Google Custom Search API with a stealthy browser-based approach.
/// Note: Run `pwsh bin/Debug/net8.0/playwright.ps1 install` to set up Playwright browsers.
/// </summary>
public sealed class GoogleDorkSource : IDomainSource
{
    private readonly ILogger<GoogleDorkSource> _logger;

    public GoogleDorkSource(ILogger<GoogleDorkSource> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "googleserper"; // Renamed to reflect method

    public async IAsyncEnumerable<DomainCandidate> FetchAsync(DomainSourceRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Collect results first to avoid yield-in-try-catch limitation
        var results = await ScrapeGoogleAsync(request, cancellationToken);

        foreach (var candidate in results)
        {
            yield return candidate;
        }
    }

    private async Task<List<DomainCandidate>> ScrapeGoogleAsync(DomainSourceRequest request, CancellationToken cancellationToken)
    {
        var candidates = new List<DomainCandidate>();
        IPlaywright? playwright = null;
        IBrowser? browser = null;

        try
        {
            playwright = await Playwright.CreateAsync();
            // Launch standard Chromium, headless but with a real user agent
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });

            // Context with stealthier settings
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
            });

            var page = await context.NewPageAsync();
            var query = request.Query ?? "site:linkedin.com/company \"Nigeria\" \"ASP.NET\"";

            _logger.LogInformation("Searching Google for: {Query}", query);

            // Go to Google
            await page.GotoAsync($"https://www.google.com/search?q={Uri.EscapeDataString(query)}&num=20");

            // Wait for results (with timeout)
            try
            {
                await page.WaitForSelectorAsync("div.g", new PageWaitForSelectorOptions { Timeout = 10000 });
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Timeout waiting for Google results. May have been rate-limited or CAPTCHA'd.");
                return candidates;
            }

            // Extract using Playwright locators
            var elements = await page.Locator("div.g").AllAsync();
            var limit = request.Limit ?? 50;

            foreach (var el in elements)
            {
                if (candidates.Count >= limit)
                    break;

                try
                {
                    var titleEl = el.Locator("h3").First;
                    var linkEl = el.Locator("a").First;

                    var title = await titleEl.InnerTextAsync();
                    var link = await linkEl.GetAttributeAsync("href");

                    if (string.IsNullOrEmpty(link))
                        continue;

                    // Extract domain or company info based on result type
                    var metadata = new Dictionary<string, string>
                    {
                        ["title"] = title ?? string.Empty,
                        ["link"] = link,
                        ["source"] = "Google SERP"
                    };

                    string candidateDomain;

                    if (link.Contains("linkedin.com/company"))
                    {
                        // LinkedIn company result - extract company name from title
                        metadata["linkedinUrl"] = link;
                        var companyName = title?.Split(new[] { '-', '|' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim() ?? "Unknown";
                        candidateDomain = $"linkedin-{companyName.Replace(" ", "-").ToLowerInvariant()}";
                    }
                    else if (Uri.TryCreate(link, UriKind.Absolute, out var uri))
                    {
                        // Regular website result - use the domain
                        candidateDomain = uri.Host.ToLowerInvariant();
                    }
                    else
                    {
                        continue;
                    }

                    candidates.Add(DomainCandidate.Create(candidateDomain, Name, 0.8, metadata));
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error extracting result element");
                }
            }

            _logger.LogInformation("Extracted {Count} results from Google SERP", candidates.Count);

            // Random delay to be nice to Google (avoid CAPTCHA)
            await Task.Delay(Random.Shared.Next(2000, 5000), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Google SERP scraping");
        }
        finally
        {
            if (browser != null) await browser.DisposeAsync();
            playwright?.Dispose();
        }

        return candidates;
    }
}

/// <summary>
/// Legacy options class kept for backward compatibility with existing DI registrations.
/// No longer used by the Playwright-based implementation.
/// </summary>
public sealed record GoogleCustomSearchOptions
{
    public string ApiKey { get; init; } = string.Empty;
    public string SearchEngineId { get; init; } = string.Empty;
}