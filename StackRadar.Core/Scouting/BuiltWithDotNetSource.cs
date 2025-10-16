using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using HtmlAgilityPack;

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

    public string Name => "whatruns";

    public async IAsyncEnumerable<DomainCandidate> FetchAsync(DomainSourceRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("scout");
        var maxPages = request.MaxPages ?? 5;
        var totalYielded = 0;
        var limit = request.Limit;
        var query = (request.Query ?? "asp.net").Replace(".", "-");

        for (var page = 1; page <= maxPages; page++)
        {
            var url = BuildUrl(page, query);
            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("WhatRuns responded with {StatusCode} for {Url}", response.StatusCode, url);
                yield break;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("HTML length: {Length}", html.Length);
            var domains = ParseDomains(html);
            _logger.LogInformation("Found {Count} domains", domains.Count);
            if (domains.Count == 0)
            {
                _logger.LogInformation("WhatRuns returned no domains for page {Page}", page);
                yield break;
            }

            foreach (var domain in domains)
            {
                var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["technology"] = query.Replace("-", "."),
                    ["url"] = $"https://{domain}"
                };

                yield return DomainCandidate.Create(domain, Name, 0.8, metadata);
                totalYielded++;

                if (limit.HasValue && totalYielded >= limit.Value)
                {
                    yield break;
                }
            }
        }
    }

    private static string BuildUrl(int page, string technology)
    {
        // BuiltWith websitelist URL for Nigerian ASP.NET sites
        return $"https://trends.builtwith.com/websitelist/{Uri.EscapeDataString(technology)}/Nigeria?page={page}";
    }

    private static IReadOnlyList<string> ParseDomains(string html)
    {
        var domains = new List<string>();
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Look for table rows with website data
        var rows = doc.DocumentNode.SelectNodes("//tr[@data-website]");
        if (rows != null && rows.Count > 0)
        {
            Console.WriteLine($"Found {rows.Count} table rows with websites");
            foreach (var row in rows)
            {
                var website = row.GetAttributeValue("data-website", "");
                if (!string.IsNullOrWhiteSpace(website))
                {
                    domains.Add(website.ToLowerInvariant());
                    Console.WriteLine($"Website: {website}");
                }
            }
        }
        else
        {
            Console.WriteLine("No table rows found, looking for JSON data");
            // Look for JSON data in script tags
            var scripts = doc.DocumentNode.SelectNodes("//script");
            if (scripts != null)
            {
                foreach (var script in scripts)
                {
                    var content = script.InnerText;
                    if (content.Contains("websites") || content.Contains("data"))
                    {
                        // Try to extract domains from JSON-like structures
                        var jsonRegex = new System.Text.RegularExpressions.Regex(@"""website""\s*:\s*""([^""]+)""");
                        var matches = jsonRegex.Matches(content);
                        foreach (System.Text.RegularExpressions.Match match in matches)
                        {
                            var domain = match.Groups[1].Value.ToLowerInvariant();
                            if (!string.IsNullOrWhiteSpace(domain) && domain.Contains('.'))
                            {
                                domains.Add(domain);
                                Console.WriteLine($"JSON Website: {domain}");
                            }
                        }
                    }
                }
            }

            if (domains.Count == 0)
            {
                Console.WriteLine("No JSON data found, falling back to targeted regex");
                // More targeted regex for actual website domains (not tech domains)
                var domainRegex = new System.Text.RegularExpressions.Regex(@"\b[a-zA-Z0-9-]+\.(?:com|org|net|edu|gov|ng|co\.uk|co\.za)\b");
                var matches = domainRegex.Matches(html);
                
                Console.WriteLine($"Found {matches.Count} targeted domain matches");
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var domain = match.Value.ToLowerInvariant();
                    // Filter out known tech domains and builtwith domains
                    if (!domain.Contains("builtwith.com") && 
                        !domain.Contains("shopify.com") && 
                        !domain.Contains("wordpress.org") && 
                        !domain.Contains("adobe.com") && 
                        !domain.Contains("google.com") && 
                        !domain.Contains("amazon.com") && 
                        !domain.Contains("jsdelivr") && 
                        !domain.Contains("cloudfront") && 
                        !domain.Contains("w3.org") &&
                        !domain.Contains("example"))
                    {
                        domains.Add(domain);
                        Console.WriteLine($"Targeted Domain: {domain}");
                    }
                }
            }
        }

        return domains.Distinct().Take(50).ToList();
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
}
