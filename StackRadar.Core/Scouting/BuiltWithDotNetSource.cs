using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace StackRadar.Core.Scouting;

public sealed class BuiltWithDotNetSource : IDomainSource
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BuiltWithDotNetSource> _logger;

    public BuiltWithDotNetSource(IHttpClientFactory httpClientFactory, ILogger<BuiltWithDotNetSource> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "builtwithdotnet";

    public async IAsyncEnumerable<DomainCandidate> FetchAsync(DomainSourceRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("scout");
        var maxPages = request.MaxPages ?? 5;
        var totalYielded = 0;
        var limit = request.Limit;
        var query = request.Query;

        for (var page = 1; page <= maxPages; page++)
        {
            var url = BuildUrl(page, query);
            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("BuiltWithDotNet responded with {StatusCode} for {Url}", response.StatusCode, url);
                yield break;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<WebsiteResponse>(stream, cancellationToken: cancellationToken);
            if (payload?.Data is null || payload.Data.Count == 0)
            {
                _logger.LogInformation("BuiltWithDotNet returned no data for page {Page}", page);
                yield break;
            }

            foreach (var site in payload.Data)
            {
                var domain = NormalizeDomain(site.Domain ?? site.Url ?? site.Host ?? site.Name);
                if (string.IsNullOrWhiteSpace(domain))
                {
                    continue;
                }

                var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["name"] = site.Name ?? string.Empty,
                    ["technology"] = site.Technology ?? string.Empty,
                    ["url"] = site.Url ?? string.Empty
                };

                yield return DomainCandidate.Create(domain, Name, 0.6, metadata);
                totalYielded++;

                if (limit.HasValue && totalYielded >= limit.Value)
                {
                    yield break;
                }
            }

            if (payload.LastPage.HasValue && page >= payload.LastPage.Value)
            {
                yield break;
            }
        }
    }

    private static string BuildUrl(int page, string? technology)
    {
        var baseUrl = $"https://builtwithdot.net/api/websites?page={page}";
        if (!string.IsNullOrWhiteSpace(technology))
        {
            baseUrl += $"&technology={Uri.EscapeDataString(technology)}";
        }

        return baseUrl;
    }

    private static string? NormalizeDomain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            {
                return uri.Host.ToLowerInvariant();
            }
        }

        trimmed = trimmed.TrimStart('.');
        if (trimmed.Contains('/'))
        {
            trimmed = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries)[0];
        }

        return trimmed.ToLowerInvariant();
    }

    private sealed record WebsiteResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<WebsiteEntry> Data,
        [property: JsonPropertyName("current_page")] int? CurrentPage,
        [property: JsonPropertyName("last_page")] int? LastPage
    );

    private sealed record WebsiteEntry(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("domain")] string? Domain,
        [property: JsonPropertyName("host")] string? Host,
        [property: JsonPropertyName("technology")] string? Technology
    );
}
