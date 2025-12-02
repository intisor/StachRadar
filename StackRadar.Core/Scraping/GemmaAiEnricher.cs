using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace StackRadar.Core.Scraping;

/// <summary>
/// Uses Gemma AI (local or via Ollama) to analyze scraped web content
/// and provide intelligent business insights and classifications
/// </summary>
public sealed class GemmaAiEnricher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GemmaAiEnricher> _logger;
    private readonly GemmaOptions _options;

    public GemmaAiEnricher(HttpClient httpClient, ILogger<GemmaAiEnricher> logger, GemmaOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Analyze web content and extract business insights using Gemma
    /// </summary>
    public async Task<AiEnrichedContent> AnalyzeContentAsync(ExtractedWebContent content, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await CheckGemmaAvailabilityAsync(cancellationToken))
            {
                _logger.LogWarning("Gemma service not available at {Endpoint}", _options.Endpoint);
                return CreateFallbackEnrichment(content);
            }

            var enriched = new AiEnrichedContent { Domain = content.Domain };

            // Analyze business type and services
            enriched.BusinessType = await AnalyzeBusinessTypeAsync(content, cancellationToken);
            enriched.Services = await ExtractServicesAsync(content, cancellationToken);
            enriched.Industry = await ClassifyIndustryAsync(content, cancellationToken);
            enriched.CompetencyLevel = await AssessCompetencyAsync(content, cancellationToken);
            enriched.DigitalMaturity = await AssessDigitalMaturityAsync(content, cancellationToken);
            enriched.KeyFindings = await GenerateKeyFindingsAsync(content, cancellationToken);
            enriched.RecommendedActions = await GenerateRecommendationsAsync(content, cancellationToken);

            return enriched;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enriching content for {Domain} with Gemma", content.Domain);
            return CreateFallbackEnrichment(content);
        }
    }

    private async Task<bool> CheckGemmaAvailabilityAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var response = await _httpClient.GetAsync($"{_options.Endpoint}/api/tags", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> AnalyzeBusinessTypeAsync(ExtractedWebContent content, CancellationToken cancellationToken)
    {
        var prompt = $@"Analyze this company information and determine their primary business type in 1-2 words.

Company: {content.CompanyName}
Description: {content.CompanyDescription}
Tech Stack: {string.Join(", ", content.TechStack.Select(t => t.Framework))}

Respond with ONLY the business type (e.g., 'Software Development', 'E-commerce', 'Consulting', 'Manufacturing').";

        return await CallGemmaAsync(prompt, cancellationToken);
    }

    private async Task<List<string>> ExtractServicesAsync(ExtractedWebContent content, CancellationToken cancellationToken)
    {
        var prompt = $@"Based on this website content, list the main services or products offered. Be specific and concise.

Company: {content.CompanyName}
Description: {content.CompanyDescription}
Headings: {string.Join(", ", content.HeadingStructure.Take(10))}

Respond with a comma-separated list of services/products (e.g., 'Web Development, Mobile Apps, Consulting').";

        var response = await CallGemmaAsync(prompt, cancellationToken);
        return response.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
    }

    private async Task<string> ClassifyIndustryAsync(ExtractedWebContent content, CancellationToken cancellationToken)
    {
        var prompt = $@"Classify this company into one of these industries: IT/Technology, Manufacturing, Retail/E-commerce, Services, Finance, Healthcare, Education, Energy, Other.

Company: {content.CompanyName}
Description: {content.CompanyDescription}
Keywords: {content.MetaKeywords}
Services: {string.Join(", ", content.TechStack.Select(t => t.Framework))}

Respond with ONLY the industry name.";

        return await CallGemmaAsync(prompt, cancellationToken);
    }

    private async Task<string> AssessCompetencyAsync(ExtractedWebContent content, CancellationToken cancellationToken)
    {
        var prompt = $@"Assess the technical competency level of this company based on their tech stack and website quality.
Rate as: Beginner, Intermediate, Advanced, Expert.

Tech Stack: {string.Join(", ", content.TechStack.Select(t => t.Framework))}
Website Title: {content.Title}
Has Email: {(content.EmailAddresses.Any() ? "Yes" : "No")}
Has Social Links: {content.SocialLinks.Count} platforms

Respond with ONLY the competency level.";

        return await CallGemmaAsync(prompt, cancellationToken);
    }

    private async Task<string> AssessDigitalMaturityAsync(ExtractedWebContent content, CancellationToken cancellationToken)
    {
        var prompt = $@"Assess the digital maturity of this company on a scale of 1-5.
1 = Basic static website, 5 = Modern SaaS/full-stack application.

Tech Stack: {string.Join(", ", content.TechStack.Select(t => t.Framework))}
Has Meta Tags: {(!string.IsNullOrEmpty(content.MetaDescription) ? "Yes" : "No")}
Has Social Links: {content.SocialLinks.Count > 0}
Has Email: {content.EmailAddresses.Any()}
Has Phone: {content.PhoneNumbers.Any()}

Respond with a score 1-5 followed by brief reason (e.g., '3 - Modern backend but outdated UI').";

        return await CallGemmaAsync(prompt, cancellationToken);
    }

    private async Task<List<string>> GenerateKeyFindingsAsync(ExtractedWebContent content, CancellationToken cancellationToken)
    {
        var prompt = $@"Generate 3-5 key findings about this company based on their website. Be specific and actionable.

Company: {content.CompanyName}
Tech Stack: {string.Join(", ", content.TechStack.Select(t => t.Framework))}
Description: {content.CompanyDescription}
Contact Methods: Email({content.EmailAddresses.Count}), Phone({content.PhoneNumbers.Count}), Social({content.SocialLinks.Count})

Format: bullet points, one per line.";

        var response = await CallGemmaAsync(prompt, cancellationToken);
        return response.Split('\n').Select(s => s.Trim().TrimStart('-', '•', '*'))
            .Where(s => !string.IsNullOrEmpty(s)).Take(5).ToList();
    }

    private async Task<List<string>> GenerateRecommendationsAsync(ExtractedWebContent content, CancellationToken cancellationToken)
    {
        var prompt = $@"Generate 3-4 specific, actionable recommendations to modernize this company's digital presence.

Company: {content.CompanyName}
Tech Stack: {string.Join(", ", content.TechStack.Select(t => t.Framework))}
Website Quality: Professional contact info available
Description: {content.CompanyDescription}

Focus on technology improvements, modernization, and business impact.
Format: bullet points, one per line.";

        var response = await CallGemmaAsync(prompt, cancellationToken);
        return response.Split('\n').Select(s => s.Trim().TrimStart('-', '•', '*'))
            .Where(s => !string.IsNullOrEmpty(s)).Take(4).ToList();
    }

    private async Task<string> CallGemmaAsync(string prompt, CancellationToken cancellationToken)
    {
        try
        {
            var request = new GemmaRequest
            {
                Model = _options.Model,
                Prompt = prompt,
                Stream = false,
                Temperature = 0.7f,
                TopP = 0.9f
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            var response = await _httpClient.PostAsJsonAsync(
                $"{_options.Endpoint}/api/generate",
                request,
                cancellationToken: cts.Token
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemma API returned {StatusCode}", response.StatusCode);
                return string.Empty;
            }

            var result = await response.Content.ReadFromJsonAsync<GemmaResponse>(cancellationToken: cancellationToken);
            return result?.Response?.Trim() ?? string.Empty;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Gemma API call timed out");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Gemma API");
            return string.Empty;
        }
    }

    private static AiEnrichedContent CreateFallbackEnrichment(ExtractedWebContent content)
    {
        return new AiEnrichedContent
        {
            Domain = content.Domain,
            BusinessType = "Unknown",
            Services = new(),
            Industry = "Technology",
            CompetencyLevel = DetermineCompetencyFallback(content),
            DigitalMaturity = DetermineMaturityFallback(content),
            KeyFindings = GenerateKeyFindingsFallback(content),
            RecommendedActions = GenerateRecommendationsFallback(content)
        };
    }

    private static string DetermineCompetencyFallback(ExtractedWebContent content)
    {
        if (content.TechStack.Count == 0) return "Beginner";
        if (content.TechStack.Count <= 2) return "Intermediate";
        if (content.EmailAddresses.Any() && content.SocialLinks.Any()) return "Advanced";
        return "Intermediate";
    }

    private static string DetermineMaturityFallback(ExtractedWebContent content)
    {
        var score = 2;
        if (!string.IsNullOrEmpty(content.MetaDescription)) score++;
        if (content.SocialLinks.Count > 0) score++;
        if (content.EmailAddresses.Any()) score++;
        if (content.TechStack.Count > 3) score++;
        return $"{Math.Min(5, score)}";
    }

    private static List<string> GenerateKeyFindingsFallback(ExtractedWebContent content)
    {
        return new()
        {
            $"Website built with {(content.TechStack.Count > 0 ? string.Join(", ", content.TechStack.Select(t => t.Framework).Take(2)) : "modern technologies")}",
            $"Active on {content.SocialLinks.Count} social platforms",
            $"Contact: {content.EmailAddresses.Count} email(s), {content.PhoneNumbers.Count} phone(s)"
        };
    }

    private static List<string> GenerateRecommendationsFallback(ExtractedWebContent content)
    {
        return new()
        {
            "Update website with modern responsive design practices",
            "Implement SEO best practices and structured data",
            "Consider modernizing backend infrastructure"
        };
    }
}

public sealed class GemmaOptions
{
    public string Endpoint { get; set; } = "http://localhost:11434";
    // Use the smaller gemma:2b model by default for low-RAM machines
    public string Model { get; set; } = "gemma:2b";
    public bool Enabled { get; set; } = true;
}

public sealed class AiEnrichedContent
{
    public string Domain { get; set; } = string.Empty;
    public string BusinessType { get; set; } = string.Empty;
    public List<string> Services { get; set; } = new();
    public string Industry { get; set; } = string.Empty;
    public string CompetencyLevel { get; set; } = string.Empty;
    public string DigitalMaturity { get; set; } = string.Empty;
    public List<string> KeyFindings { get; set; } = new();
    public List<string> RecommendedActions { get; set; } = new();
}

internal sealed record GemmaRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("temperature")]
    public float Temperature { get; set; } = 0.7f;

    [JsonPropertyName("top_p")]
    public float TopP { get; set; } = 0.9f;
}

internal sealed record GemmaResponse
{
    [JsonPropertyName("response")]
    public string? Response { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }
}
