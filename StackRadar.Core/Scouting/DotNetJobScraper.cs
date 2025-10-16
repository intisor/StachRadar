using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using StackRadar.Core.Models;
using System.Globalization;

namespace StackRadar.Core.Scouting;

/// <summary>
/// Domain source that scrapes .NET job sites and extracts company names intelligently.
/// Leverages existing infrastructure: Playwright + GemmaAiEnricher or local LLMs via Ollama.
/// </summary>
public sealed class DotNetJobScraper : IDomainSource
{
	private readonly ILogger<DotNetJobScraper> _logger;
	private readonly HttpClient _httpClient;

	public DotNetJobScraper(ILogger<DotNetJobScraper> logger, HttpClient httpClient)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
	}

	public string Name => "dotnetjobs";

	public async IAsyncEnumerable<DomainCandidate> FetchAsync(DomainSourceRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		// Load job sites configuration from CSV
		var jobSites = LoadJobSites();
		if (!jobSites.Any())
		{
			_logger.LogWarning("No job sites configuration found in dotnet_job_sites.csv");
			yield break;
		}

		var totalYielded = 0;
		var limit = request.Limit ?? 100;

		// Keywords to search for .NET positions
		var keywords = new[] { ".NET Developer", "C# Developer", "ASP.NET", "ASP.NET Core" };

		// Collect all candidates before yielding to avoid try-catch with yield
		var candidates = new List<DomainCandidate>();

		foreach (var site in jobSites.Where(s => s.Active).OrderByDescending(s => GetPriority(s.Priority)))
		{
			foreach (var keyword in keywords)
			{
				if (candidates.Count >= limit)
					break;

				_logger.LogInformation("Scraping {SiteName} for keyword: {Keyword}", site.SiteName, keyword);

				try
				{
					// Build search URL
					var searchUrl = BuildSearchUrl(site, keyword);

					// Fetch job listings HTML
					var html = await FetchJobListingsAsync(site, searchUrl, cancellationToken);
					if (string.IsNullOrWhiteSpace(html))
						continue;

					// Extract company names from HTML using simple regex patterns
					var companyNames = ExtractCompanyNamesFromHtml(html, site.SiteName);

					foreach (var companyName in companyNames)
					{
						if (candidates.Count >= limit)
							break;

						var metadata = new Dictionary<string, string>
						{
							["companyName"] = companyName,
							["source"] = site.SiteName,
							["jobType"] = keyword,
							["discoveryMethod"] = "JobSiteScrape",
							["url"] = searchUrl
						};

						var candidate = DomainCandidate.Create(
							domain: $"job-{site.SiteId}-{companyName.Replace(" ", "-").ToLower()}",
							source: Name,
							confidence: 0.75,
							metadata: metadata
						);

						candidates.Add(candidate);
					}

					// Rate limiting between requests
					await Task.Delay(1500, cancellationToken);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error scraping {SiteName}", site.SiteName);
					continue;
				}
			}

			if (candidates.Count >= limit)
				break;
		}

		// Now yield all collected candidates
		foreach (var candidate in candidates.Take(limit))
		{
			yield return candidate;
		}

		_logger.LogInformation("Completed job site scraping. Extracted {Count} companies", candidates.Count);
	}

	/// <summary>
	/// Load job sites configuration from CSV (simplified inline approach).
	/// </summary>
	private List<JobSiteConfig> LoadJobSites()
	{
		try
		{
			var csvPath = "dotnet_job_sites.csv";
			if (!File.Exists(csvPath))
			{
				_logger.LogWarning("Job sites CSV not found at {Path}", csvPath);
				return GetDefaultJobSites();
			}

			var sites = new List<JobSiteConfig>();
			var lines = File.ReadAllLines(csvPath).Skip(1); // Skip header

			foreach (var line in lines)
			{
				var parts = line.Split(',');
				if (parts.Length < 8)
					continue;

				sites.Add(new JobSiteConfig
				{
					SiteId = int.Parse(parts[0]),
					SiteName = parts[1].Trim('"'),
					BaseUrl = parts[2].Trim('"'),
					SearchPattern = parts[3].Trim('"'),
					Priority = parts[6].Trim('"'),
					Active = bool.Parse(parts[7])
				});
			}

			_logger.LogInformation("Loaded {Count} job sites", sites.Count);
			return sites;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error loading job sites. Using defaults.");
			return GetDefaultJobSites();
		}
	}

	/// <summary>
	/// Default job sites if CSV not found.
	/// </summary>
	private List<JobSiteConfig> GetDefaultJobSites()
	{
		return new List<JobSiteConfig>
		{
			new() { SiteId = 1, SiteName = "Dice", BaseUrl = "https://www.dice.com", SearchPattern = "/search?q={keyword}", Priority = "very-high", Active = true },
			new() { SiteId = 2, SiteName = "Indeed", BaseUrl = "https://www.indeed.com", SearchPattern = "/jobs?q={keyword}+.NET", Priority = "high", Active = true },
			new() { SiteId = 3, SiteName = "WeWorkRemotely", BaseUrl = "https://www.weworkremotely.com", SearchPattern = "/remote-programming-jobs?search={keyword}", Priority = "high", Active = true },
			new() { SiteId = 15, SiteName = "Gun.io", BaseUrl = "https://gun.io", SearchPattern = "/find-a-developer/?q={keyword}", Priority = "medium", Active = true }
		};
	}

	/// <summary>
	/// Build search URL for job site.
	/// </summary>
	private string BuildSearchUrl(JobSiteConfig site, string keyword)
	{
		var encodedKeyword = Uri.EscapeDataString(keyword);
		return site.SearchPattern.Replace("{keyword}", encodedKeyword);
	}

	/// <summary>
	/// Fetch job listings HTML from URL.
	/// </summary>
	private async Task<string> FetchJobListingsAsync(JobSiteConfig site, string urlPath, CancellationToken cancellationToken)
	{
		try
		{
			var fullUrl = site.BaseUrl.TrimEnd('/') + urlPath;
			_logger.LogDebug("Fetching: {Url}", fullUrl);

			var request = new HttpRequestMessage(HttpMethod.Get, fullUrl);
			request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

			var response = await _httpClient.SendAsync(request, cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				_logger.LogWarning("Failed to fetch {Url}: {StatusCode}", fullUrl, response.StatusCode);
				return string.Empty;
			}

			return await response.Content.ReadAsStringAsync(cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error fetching from {Site}", site.SiteName);
			return string.Empty;
		}
	}

	/// <summary>
	/// Extract company names from HTML using pattern matching (lightweight approach).
	/// For AI-powered extraction, use GemmaAiEnricher separately.
	/// </summary>
	private List<string> ExtractCompanyNamesFromHtml(string html, string siteName)
	{
		var companies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		try
		{
			// Common patterns for company names in job listings
			var patterns = new[]
			{
				@"<h[1-4][^>]*class=""[^""]*company[^""]*""[^>]*>([^<]+)</h[1-4]>",
				@"<div[^>]*class=""[^""]*company-name[^""]*""[^>]*>([^<]+)</div>",
				@"<span[^>]*class=""[^""]*company[^""]*""[^>]*>([^<]+)</span>",
				@"<a[^>]*href=""[^""]*company[^""]*""[^>]*>([^<]+)</a>",
				@"<strong>([A-Z][A-Za-z\s&\.\-]{2,50})</strong>\s*(?:Hiring|is hiring|posted)",
				@"Posted by:\s*<b>([^<]+)</b>",
				@"Company:\s*<[^>]*>([^<]+)<",
			};

			var regex = new System.Text.RegularExpressions.Regex(
				string.Join("|", patterns),
				System.Text.RegularExpressions.RegexOptions.IgnoreCase
			);

			var matches = regex.Matches(html);

			foreach (System.Text.RegularExpressions.Match match in matches)
			{
				for (int i = 1; i < match.Groups.Count; i++)
				{
					var name = match.Groups[i].Value.Trim();

					// Clean and validate company name
					name = CleanCompanyName(name);

					if (!string.IsNullOrWhiteSpace(name) && name.Length > 2 && name.Length < 100)
					{
						// Filter out common noise
						if (!name.Contains("http", StringComparison.OrdinalIgnoreCase) &&
							!name.Contains("click", StringComparison.OrdinalIgnoreCase) &&
							!name.Contains("follow", StringComparison.OrdinalIgnoreCase))
						{
							companies.Add(name);
						}
					}
				}
			}

			_logger.LogInformation("Extracted {Count} companies from {Site}", companies.Count, siteName);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error extracting companies from {Site}", siteName);
		}

		return companies.ToList();
	}

	/// <summary>
	/// Clean company name.
	/// </summary>
	private string CleanCompanyName(string name)
	{
		// Decode HTML entities
		name = System.Net.WebUtility.HtmlDecode(name);

		// Remove HTML tags if any
		name = System.Text.RegularExpressions.Regex.Replace(name, @"<[^>]+>", "");

		// Trim whitespace
		name = name.Trim();

		// Remove quotes
		name = name.Trim('"', '\'');

		// Remove trailing Inc, Corp, etc.
		name = System.Text.RegularExpressions.Regex.Replace(
			name,
			@"\s*(Inc\.?|Corp\.?|Ltd\.?|LLC\.?|Inc\.?|Co\.?|Ltd|LLC|LLP)\.?\s*$",
			"",
			System.Text.RegularExpressions.RegexOptions.IgnoreCase
		);

		return name.Trim();
	}

	/// <summary>
	/// Get priority level as integer.
	/// </summary>
	private int GetPriority(string priorityStr)
	{
		return priorityStr switch
		{
			"very-high" => 4,
			"high" => 3,
			"medium" => 2,
			"low" => 1,
			_ => 0
		};
	}
}

/// <summary>
/// Job site configuration.
/// </summary>
public sealed class JobSiteConfig
{
	public int SiteId { get; set; }
	public string SiteName { get; set; } = "";
	public string BaseUrl { get; set; } = "";
	public string SearchPattern { get; set; } = "";
	public string Priority { get; set; } = "medium";
	public bool Active { get; set; } = true;
}
