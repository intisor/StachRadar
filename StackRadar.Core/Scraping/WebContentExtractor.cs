using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace StackRadar.Core.Scraping;

/// <summary>
/// Extracts structured data from website HTML content including:
/// - Company metadata (name, description, contact)
/// - Technology stack indicators
/// - Business information (services, industry, location)
/// - Social links and integrations
/// </summary>
public sealed class WebContentExtractor
{
    private readonly ILogger<WebContentExtractor> _logger;

    public WebContentExtractor(ILogger<WebContentExtractor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Extract comprehensive metadata from HTML content
    /// </summary>
    public ExtractedWebContent Extract(string html, string domain)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            return new ExtractedWebContent
            {
                Domain = domain,
                Title = ExtractTitle(doc),
                MetaDescription = ExtractMetaTag(doc, "description"),
                MetaKeywords = ExtractMetaTag(doc, "keywords"),
                OgTitle = ExtractMetaTag(doc, "og:title"),
                OgDescription = ExtractMetaTag(doc, "og:description"),
                OgImage = ExtractMetaTag(doc, "og:image"),
                CompanyName = ExtractCompanyName(doc, domain),
                CompanyDescription = ExtractCompanyDescription(doc),
                EmailAddresses = ExtractEmails(doc, html),
                PhoneNumbers = ExtractPhoneNumbers(html),
                SocialLinks = ExtractSocialLinks(doc),
                TechStack = ExtractTechIndicators(doc, html),
                ContactInfo = ExtractContactInfo(doc),
                LocationInfo = ExtractLocationInfo(doc),
                HeadingStructure = ExtractHeadings(doc),
                TextContent = ExtractMainTextContent(doc),
                RawHtml = html
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting content from {Domain}", domain);
            return new ExtractedWebContent { Domain = domain, RawHtml = html };
        }
    }

    private string? ExtractTitle(HtmlDocument doc)
    {
        var titleNode = doc.DocumentNode.SelectSingleNode("//title");
        return titleNode?.InnerText?.Trim();
    }

    private string? ExtractMetaTag(HtmlDocument doc, string name)
    {
        var node = doc.DocumentNode.SelectSingleNode($"//meta[@name='{name}' or @property='{name}']");
        return node?.GetAttributeValue("content", null)?.Trim();
    }

    private string? ExtractCompanyName(HtmlDocument doc, string domain)
    {
        // Try og:site_name first
        var ogSiteName = ExtractMetaTag(doc, "og:site_name");
        if (!string.IsNullOrWhiteSpace(ogSiteName))
            return ogSiteName;

        // Look for common company name patterns in page
        var h1s = doc.DocumentNode.SelectNodes("//h1")?.Take(1)?.FirstOrDefault()?.InnerText?.Trim();
        if (!string.IsNullOrWhiteSpace(h1s))
            return h1s;

        // Extract from domain
        return ExtractCompanyFromDomain(domain);
    }

    private string? ExtractCompanyFromDomain(string domain)
    {
        // Remove TLDs and common prefixes
        var name = domain
            .Replace(".com.ng", "")
            .Replace(".co.ng", "")
            .Replace(".ng", "")
            .Replace("www.", "")
            .Replace(".com", "");

        // Convert kebab-case to title case
        name = Regex.Replace(name, @"[-_]", " ");
        var textInfo = System.Globalization.CultureInfo.CurrentCulture.TextInfo;
        return textInfo.ToTitleCase(name.ToLower()).Trim();
    }

    private string? ExtractCompanyDescription(HtmlDocument doc)
    {
        // Try meta description first
        var metaDesc = ExtractMetaTag(doc, "description");
        if (!string.IsNullOrWhiteSpace(metaDesc))
            return metaDesc;

        // Look for "About" section
        var aboutSections = doc.DocumentNode.SelectNodes("//*[contains(text(), 'About') or contains(text(), 'WHO WE ARE') or contains(text(), 'Our Story')]");
        if (aboutSections?.Any() == true)
        {
            var aboutDiv = aboutSections.First().SelectSingleNode("./ancestor::*");
            var text = aboutDiv?.InnerText?.Trim();
            if (!string.IsNullOrWhiteSpace(text) && text.Length > 20)
                return text.Length > 500 ? text[..500] + "..." : text;
        }

        return null;
    }

    private List<string> ExtractEmails(HtmlDocument doc, string html)
    {
        var emails = new List<string>();
        var emailRegex = new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.Compiled);

        var matches = emailRegex.Matches(html);
        foreach (Match match in matches)
        {
            var email = match.Value.ToLower();
            if (!email.Contains("example") && !email.Contains("test") && emails.Count < 10)
                emails.Add(email);
        }

        return emails.Distinct().ToList();
    }

    private List<string> ExtractPhoneNumbers(string html)
    {
        var phones = new List<string>();
        var phoneRegex = new Regex(@"(?:\+234|0)[0-9]{10}|\+?[1-9]\d{1,14}", RegexOptions.Compiled);

        var matches = phoneRegex.Matches(html);
        foreach (Match match in matches)
        {
            if (!phones.Contains(match.Value) && phones.Count < 5)
                phones.Add(match.Value);
        }

        return phones;
    }

    private Dictionary<string, string> ExtractSocialLinks(HtmlDocument doc)
    {
        var socials = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var socialPlatforms = new[] { "facebook", "twitter", "linkedin", "instagram", "youtube", "github", "tiktok" };

        foreach (var platform in socialPlatforms)
        {
            var link = doc.DocumentNode
                .SelectNodes($"//a[contains(@href, '{platform}')]")?
                .FirstOrDefault()?
                .GetAttributeValue("href", null);

            if (!string.IsNullOrWhiteSpace(link))
                socials[platform] = link;
        }

        return socials;
    }

    private List<TechIndicator> ExtractTechIndicators(HtmlDocument doc, string html)
    {
        var tech = new List<TechIndicator>();

        // Check for ASP.NET / C# indicators
        var aspNetIndicators = new[]
        {
            ("__VIEWSTATE", "ASP.NET WebForms"),
            ("__EVENTVALIDATION", "ASP.NET WebForms"),
            ("aspNetCore", "ASP.NET Core"),
            ("nonce-", "ASP.NET Core"),
            ("Razor", "ASP.NET Razor"),
            (".NET", ".NET Framework")
        };

        foreach (var (indicator, framework) in aspNetIndicators)
        {
            if (html.Contains(indicator, StringComparison.OrdinalIgnoreCase))
                tech.Add(new TechIndicator { Framework = framework, Found = true });
        }

        // Check meta tags and scripts
        var metaGenerator = ExtractMetaTag(doc, "generator");
        if (!string.IsNullOrWhiteSpace(metaGenerator))
            tech.Add(new TechIndicator { Framework = metaGenerator, Found = true });

        // Look for common JS frameworks
        var scripts = doc.DocumentNode.SelectNodes("//script[@src]")?
            .Select(s => s.GetAttributeValue("src", ""))?
            .ToList() ?? new List<string>();

        var jsIndicators = new[] { "react", "vue", "angular", "jquery", "bootstrap" };
        foreach (var jsLib in jsIndicators)
        {
            if (scripts.Any(s => s.Contains(jsLib, StringComparison.OrdinalIgnoreCase)))
                tech.Add(new TechIndicator { Framework = jsLib, Found = true });
        }

        return tech.DistinctBy(t => t.Framework).ToList();
    }

    private string? ExtractContactInfo(HtmlDocument doc)
    {
        var contactNode = doc.DocumentNode.SelectSingleNode("//*[@class or @id][contains(translate(@class | @id, 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), 'contact')]");
        return contactNode?.InnerText?.Trim();
    }

    private string? ExtractLocationInfo(HtmlDocument doc)
    {
        // Look for address tags or location metadata
        var addressNode = doc.DocumentNode.SelectSingleNode("//address");
        if (addressNode != null)
            return addressNode.InnerText?.Trim();

        // Look for structured data (JSON-LD)
        var jsonLd = doc.DocumentNode.SelectSingleNode("//script[@type='application/ld+json']");
        if (jsonLd != null)
            return jsonLd.InnerText?.Trim();

        return null;
    }

    private List<string> ExtractHeadings(HtmlDocument doc)
    {
        var headings = new List<string>();
        for (int i = 1; i <= 6; i++)
        {
            var nodes = doc.DocumentNode.SelectNodes($"//h{i}");
            if (nodes != null)
            {
                headings.AddRange(nodes.Select(n => n.InnerText?.Trim()).Where(t => !string.IsNullOrWhiteSpace(t))!);
            }
        }

        return headings.Take(20).ToList();
    }

    private string? ExtractMainTextContent(HtmlDocument doc)
    {
        // Remove script and style elements
        foreach (var node in doc.DocumentNode.SelectNodes("//script | //style"))
        {
            node.ParentNode.RemoveChild(node, false);
        }

        var text = doc.DocumentNode.InnerText;
        // Clean up excessive whitespace
        text = Regex.Replace(text, @"\s+", " ");
        return text.Length > 2000 ? text[..2000] : text;
    }
}

/// <summary>
/// Contains extracted structured data from website HTML
/// </summary>
public sealed class ExtractedWebContent
{
    public string Domain { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public string? OgTitle { get; set; }
    public string? OgDescription { get; set; }
    public string? OgImage { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyDescription { get; set; }
    public List<string> EmailAddresses { get; set; } = new();
    public List<string> PhoneNumbers { get; set; } = new();
    public Dictionary<string, string> SocialLinks { get; set; } = new();
    public List<TechIndicator> TechStack { get; set; } = new();
    public string? ContactInfo { get; set; }
    public string? LocationInfo { get; set; }
    public List<string> HeadingStructure { get; set; } = new();
    public string? TextContent { get; set; }
    public string RawHtml { get; set; } = string.Empty;
}

public sealed class TechIndicator
{
    public string Framework { get; set; } = string.Empty;
    public bool Found { get; set; }
}
