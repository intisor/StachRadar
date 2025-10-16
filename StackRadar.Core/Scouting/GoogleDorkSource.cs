using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace StackRadar.Core.Scouting;

public sealed class GoogleDorkSource : IDomainSource
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoogleDorkSource> _logger;
    private readonly GoogleCustomSearchOptions _options;

    public GoogleDorkSource(IHttpClientFactory httpClientFactory, ILogger<GoogleDorkSource> logger, IOptions<GoogleCustomSearchOptions> options)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options.Value;
    }

    public string Name => "googledork";

    public async IAsyncEnumerable<DomainCandidate> FetchAsync(DomainSourceRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("scout");
        var query = BuildQuery(request.Query ?? ".NET");
        var maxPages = request.MaxPages ?? 5;
        var totalYielded = 0;
        var limit = request.Limit;

        for (var page = 1; page <= maxPages; page++)
        {
            var startIndex = (page - 1) * 10 + 1; // Google returns 10 results per page
            var url = $"https://www.googleapis.com/customsearch/v1?key={_options.ApiKey}&cx={_options.SearchEngineId}&q={Uri.EscapeDataString(query)}&start={startIndex}";

            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google Custom Search API responded with {StatusCode}", response.StatusCode);
                yield break;
            }

            var payload = await response.Content.ReadFromJsonAsync<SearchResponse>(cancellationToken: cancellationToken);
            if (payload?.Items is null || payload.Items.Count == 0)
            {
                _logger.LogInformation("No results for page {Page}", page);
                yield break;
            }

            foreach (var item in payload.Items)
            {
                var domain = ExtractDomain(item.Link);
                if (string.IsNullOrWhiteSpace(domain))
                    continue;

                var metadata = new Dictionary<string, string>
                {
                    ["title"] = item.Title ?? string.Empty,
                    ["snippet"] = item.Snippet ?? string.Empty,
                    ["link"] = item.Link ?? string.Empty
                };

                yield return DomainCandidate.Create(domain, Name, 0.7, metadata);

                totalYielded++;
                if (limit.HasValue && totalYielded >= limit.Value)
                    yield break;
            }
        }
    }

    private static string BuildQuery(string keyword)
    {
        var keywords = string.IsNullOrWhiteSpace(keyword)
            ? new[] { "ASP.NET", ".NET", "C#", "VB.NET", "Entity Framework" }
            : new[] { keyword };

        var keywordQuery = string.Join(" OR ", keywords.Select(k => $"\"{k}\""));
        
        // Focus on finding actual websites powered by ASP.NET in Nigeria
        return $"({keywordQuery}) (site:*.ng OR site:*.com.ng OR \"Nigeria\" OR \"Lagos\" OR \"Abuja\") -inurl:jobs -inurl:careers -inurl:indeed -inurl:linkedin -inurl:glassdoor (\"powered by\" OR \"built with\" OR \"developed by\" OR \"©\" OR \"all rights reserved\")";
    }

    private static string? ExtractDomain(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;
        return uri.Host.ToLowerInvariant();
    }

    public sealed record GoogleCustomSearchOptions
    {
        public string ApiKey { get; init; } = string.Empty;
        public string SearchEngineId { get; init; } = string.Empty;
    }

    private sealed record SearchResponse(
        [property: JsonPropertyName("items")] IReadOnlyList<SearchItem> Items
    );

    private sealed record SearchItem(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("link")] string? Link,
        [property: JsonPropertyName("snippet")] string? Snippet
    );
}