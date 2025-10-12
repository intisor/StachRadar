using System.Runtime.CompilerServices;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace StackRadar.Core.Scouting;

public sealed class IndeedJobSource : IJobBoardSource
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IndeedJobSource> _logger;

    public IndeedJobSource(IHttpClientFactory httpClientFactory, ILogger<IndeedJobSource> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "indeed";

    public async IAsyncEnumerable<JobListing> SearchAsync(JobSearchRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            _logger.LogWarning("Job search query is empty");
            yield break;
        }

        var client = _httpClientFactory.CreateClient("jobsearch");
        var maxPages = request.MaxPages ?? 3;
        var totalYielded = 0;
        var limit = request.Limit;

        for (var page = 0; page < maxPages; page++)
        {
            var start = page * 10;
            var url = BuildIndeedUrl(request.Query, request.Location, start);
            
            _logger.LogDebug("Fetching Indeed jobs from {Url}", url);

            string? html = null;
            try
            {
                using var response = await client.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Indeed responded with {StatusCode} for {Url}", response.StatusCode, url);
                    yield break;
                }

                html = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error fetching jobs from Indeed on page {Page}", page);
                yield break;
            }

            if (string.IsNullOrEmpty(html))
            {
                yield break;
            }

            var jobs = ParseIndeedHtml(html, url);

            var hasResults = false;
            foreach (var job in jobs)
            {
                hasResults = true;
                yield return job;
                totalYielded++;

                if (limit.HasValue && totalYielded >= limit.Value)
                {
                    yield break;
                }
            }

            if (!hasResults)
            {
                _logger.LogInformation("No more results found on page {Page}", page);
                yield break;
            }

            // Rate limiting to be respectful
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }

    private static string BuildIndeedUrl(string query, string? location, int start)
    {
        var queryParam = Uri.EscapeDataString(query);
        var locationParam = string.IsNullOrWhiteSpace(location) ? "" : $"&l={Uri.EscapeDataString(location)}";
        var startParam = start > 0 ? $"&start={start}" : "";
        
        return $"https://www.indeed.com/jobs?q={queryParam}{locationParam}{startParam}";
    }

    private IEnumerable<JobListing> ParseIndeedHtml(string html, string sourceUrl)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Indeed's job cards use various selectors - we try multiple patterns
        var jobCards = doc.DocumentNode.SelectNodes("//div[contains(@class, 'job_seen_beacon')]") 
                       ?? doc.DocumentNode.SelectNodes("//div[contains(@class, 'jobsearch-SerpJobCard')]")
                       ?? doc.DocumentNode.SelectNodes("//div[@class='slider_container']//div[@data-jk]")
                       ?? doc.DocumentNode.SelectNodes("//td[@id='resultsCol']//div[@data-jk]");

        if (jobCards == null || jobCards.Count == 0)
        {
            _logger.LogWarning("No job cards found in HTML. Indeed may have changed its structure.");
            yield break;
        }

        foreach (var card in jobCards)
        {
            // Try to extract title
            var titleNode = card.SelectSingleNode(".//h2[contains(@class, 'jobTitle')]//span[@title]")
                           ?? card.SelectSingleNode(".//a[contains(@class, 'jcs-JobTitle')]")
                           ?? card.SelectSingleNode(".//h2//a");
            
            var title = titleNode?.GetAttributeValue("title", null) 
                       ?? titleNode?.InnerText?.Trim() 
                       ?? "Unknown Title";

            // Try to extract company
            var companyNode = card.SelectSingleNode(".//span[@data-testid='company-name']")
                             ?? card.SelectSingleNode(".//span[contains(@class, 'companyName')]")
                             ?? card.SelectSingleNode(".//span[@class='company']");
            
            var company = companyNode?.InnerText?.Trim();
            if (string.IsNullOrWhiteSpace(company))
            {
                _logger.LogDebug("Skipping job card without company name");
                continue;
            }

            // Try to extract location
            var locationNode = card.SelectSingleNode(".//div[@data-testid='text-location']")
                              ?? card.SelectSingleNode(".//div[contains(@class, 'companyLocation')]")
                              ?? card.SelectSingleNode(".//span[@class='location']");
            
            var location = locationNode?.InnerText?.Trim() ?? string.Empty;

            // Try to extract snippet/description
            var descNode = card.SelectSingleNode(".//div[contains(@class, 'job-snippet')]")
                          ?? card.SelectSingleNode(".//div[@class='summary']");
            
            var description = descNode?.InnerText?.Trim() ?? string.Empty;

            // Try to extract job URL
            var jobKey = card.GetAttributeValue("data-jk", null);
            var jobUrl = !string.IsNullOrWhiteSpace(jobKey) 
                ? $"https://www.indeed.com/viewjob?jk={jobKey}"
                : sourceUrl;

            JobListing? listing = null;
            try
            {
                listing = JobListing.Create(
                    title,
                    company,
                    location,
                    description,
                    jobUrl,
                    Name);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to create job listing");
            }

            if (listing != null)
            {
                yield return listing;
            }
        }
    }
}
