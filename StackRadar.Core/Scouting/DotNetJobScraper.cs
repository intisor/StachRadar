using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using StackRadar.Core.Models;

namespace StackRadar.Core.Scouting;

/// <summary>
/// DotNetJobScraper (disabled)
/// The previous DotNet job scraper was brittle and often blocked. Per the new "Sniper"
/// architecture, prefer Google-first discovery and Playwright SERP scraping. This class
/// is intentionally a no-op implementation that logs guidance.
/// </summary>
public sealed class DotNetJobScraper : IDomainSource
{
	private readonly ILogger<DotNetJobScraper> _logger;

	public DotNetJobScraper(ILogger<DotNetJobScraper> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public string Name => "dotnetjobs-disabled";

	public async IAsyncEnumerable<DomainCandidate> FetchAsync(DomainSourceRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		_logger.LogInformation("DotNetJobScraper is disabled. Use GoogleDorkSource or a Playwright SERP scraper instead.");
		await Task.CompletedTask;
		yield break;
	}
}

/// <summary>
/// Job site configuration type left in place for compatibility if needed.
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
