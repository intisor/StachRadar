using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace StackRadar.Core.Scraping;

/// <summary>
/// Deep Recon Scraper using Playwright to visit websites like a real user.
/// This bypasses modern WAF/firewall blocks that HttpClient encounters.
/// Use this to:
/// 1. Visit a company's website
/// 2. Find and navigate to their "About" or "Team" page
/// 3. Extract text content for Gemma AI analysis
/// </summary>
public sealed class PlaywrightScraper
{
    private readonly ILogger<PlaywrightScraper> _logger;

    public PlaywrightScraper(ILogger<PlaywrightScraper> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Scrapes the About/Team page text from a domain.
    /// This is the "Deep Recon" step that feeds into Gemma AI.
    /// </summary>
    public async Task<string> ScrapeAboutPageTextAsync(string domain, CancellationToken cancellationToken = default)
    {
        IPlaywright? playwright = null;
        IBrowser? browser = null;

        try
        {
            playwright = await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true // Set to false if you want to watch it work!
            });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36"
            });

            var page = await context.NewPageAsync();
            var url = domain.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? domain : $"https://{domain}";

            _logger.LogInformation("Visiting {Url}...", url);
            await page.GotoAsync(url, new PageGotoOptions
            {
                Timeout = 30000,
                WaitUntil = WaitUntilState.DOMContentLoaded
            });

            // 1. Try to find an "About" link and click it
            var aboutLink = page.GetByRole(AriaRole.Link, new PageGetByRoleOptions
            {
                NameRegex = new Regex("About|Team|Company|Leadership|Our Story|Who We Are", RegexOptions.IgnoreCase)
            }).First;

            try
            {
                if (await aboutLink.IsVisibleAsync())
                {
                    _logger.LogInformation("Found About page link, clicking...");
                    await aboutLink.ClickAsync();
                    await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not navigate to About page, using homepage content");
            }

            // 2. Extract all text from the body
            var text = await page.Locator("body").InnerTextAsync();

            // 3. Clean it up for Gemma (remove massive whitespace, short lines)
            var cleanedText = CleanText(text);

            _logger.LogInformation("Extracted {Length} characters from {Domain}", cleanedText.Length, domain);
            return cleanedText;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to scrape {Domain}: {Message}", domain, ex.Message);
            return string.Empty;
        }
        finally
        {
            if (browser != null) await browser.DisposeAsync();
            playwright?.Dispose();
        }
    }

    /// <summary>
    /// Scrapes the full page HTML (useful for tech detection or custom parsing).
    /// </summary>
    public async Task<string> ScrapeFullHtmlAsync(string domain, CancellationToken cancellationToken = default)
    {
        IPlaywright? playwright = null;
        IBrowser? browser = null;

        try
        {
            playwright = await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36"
            });

            var page = await context.NewPageAsync();
            var url = domain.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? domain : $"https://{domain}";

            _logger.LogInformation("Fetching HTML from {Url}...", url);
            await page.GotoAsync(url, new PageGotoOptions
            {
                Timeout = 30000,
                WaitUntil = WaitUntilState.NetworkIdle
            });

            var html = await page.ContentAsync();
            _logger.LogInformation("Fetched {Length} bytes of HTML from {Domain}", html.Length, domain);
            return html;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch HTML from {Domain}", domain);
            return string.Empty;
        }
        finally
        {
            if (browser != null) await browser.DisposeAsync();
            playwright?.Dispose();
        }
    }

    /// <summary>
    /// Extracts contact information (emails, phones) from a page.
    /// </summary>
    public async Task<ContactInfo> ScrapeContactInfoAsync(string domain, CancellationToken cancellationToken = default)
    {
        var text = await ScrapeAboutPageTextAsync(domain, cancellationToken);
        if (string.IsNullOrEmpty(text))
            return new ContactInfo();

        // Extract emails
        var emailRegex = new Regex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.IgnoreCase);
        var emails = emailRegex.Matches(text)
            .Select(m => m.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Extract phone numbers (basic patterns)
        var phoneRegex = new Regex(@"[\+]?[(]?[0-9]{1,3}[)]?[-\s\.]?[(]?[0-9]{1,4}[)]?[-\s\.]?[0-9]{1,4}[-\s\.]?[0-9]{1,9}", RegexOptions.IgnoreCase);
        var phones = phoneRegex.Matches(text)
            .Select(m => m.Value.Trim())
            .Where(p => p.Length >= 7 && p.Length <= 20)
            .Distinct()
            .ToList();

        return new ContactInfo
        {
            Domain = domain,
            Emails = emails,
            PhoneNumbers = phones,
            RawText = text
        };
    }

    /// <summary>
    /// Clean text to save RAM for Gemma processing.
    /// </summary>
    private static string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Simple normalization to save RAM for Gemma
        var lines = text.Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l) && l.Length > 20); // Filter short garbage

        return string.Join("\n", lines);
    }
}

/// <summary>
/// Contact information extracted from a website.
/// </summary>
public sealed class ContactInfo
{
    public string Domain { get; init; } = string.Empty;
    public List<string> Emails { get; init; } = new();
    public List<string> PhoneNumbers { get; init; } = new();
    public string RawText { get; init; } = string.Empty;
}
