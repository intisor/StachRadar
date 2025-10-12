using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using CsvHelper;
using CsvHelper.Configuration;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using StackRadar.Core.Detection;
using StackRadar.Core.Models;
using StackRadar.Core.Services;
using StackRadar.Core.Scouting;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
	if (args.Length > 0 && string.Equals(args[0], "scout", StringComparison.OrdinalIgnoreCase))
	{
		var scoutArgs = args.Skip(1).ToArray();
		return await RunScoutAsync(scoutArgs);
	}

	if (args.Length > 0 && string.Equals(args[0], "job-search", StringComparison.OrdinalIgnoreCase))
	{
		var jobSearchArgs = args.Skip(1).ToArray();
		return await RunJobSearchAsync(jobSearchArgs);
	}

	return await RunScanAsync(args);
}

static async Task<int> RunScanAsync(string[] args)
{
	var options = CliOptions.Parse(args);
	if (options.ShowHelp)
	{
		CliOptions.PrintUsage();
		return 0;
	}

	if (!File.Exists(options.InputPath))
	{
		AnsiConsole.MarkupLine($"[red]Input file '{options.InputPath}' was not found.[/]");
		return 1;
	}

	using var cts = new CancellationTokenSource();
	Console.CancelKeyPress += (_, eventArgs) =>
	{
		AnsiConsole.MarkupLine("[yellow]\nCancellation requested...[/]");
		cts.Cancel();
		eventArgs.Cancel = true;
	};

	using var host = Host.CreateDefaultBuilder(args)
	.ConfigureLogging(logging =>
	{
		logging.ClearProviders();
		logging.AddSimpleConsole(o =>
		{
			o.SingleLine = true;
			o.TimestampFormat = "HH:mm:ss ";
		});
		logging.SetMinimumLevel(options.Verbose ? LogLevel.Debug : LogLevel.Information);
	})
	.ConfigureServices(services =>
	{
		services.AddSingleton(new DetectionOptions());
		services.AddSingleton(new ScannerOptions
		{
			RetryCount = options.RetryCount,
			RequestTimeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds),
			AllowHttpFallback = options.AllowHttpFallback
		});
		services.AddSingleton<DetectionEngine>();
		services.AddHttpClient("scanner", client =>
		{
			client.Timeout = Timeout.InfiniteTimeSpan;
			client.DefaultRequestHeaders.UserAgent.ParseAdd("StackRadar/0.1 (+https://github.com/)");
			client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
		})
		.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
		{
			AutomaticDecompression = DecompressionMethods.All,
			AllowAutoRedirect = true
		});
		services.AddSingleton<IDomainScanner, DomainScanner>();
	})
	.Build();

	var scanner = host.Services.GetRequiredService<IDomainScanner>();

	var domains = await LoadDomainsAsync(options.InputPath, cts.Token);
	if (domains.Count == 0)
	{
		AnsiConsole.MarkupLine("[yellow]No domains to scan.[/]");
		return 0;
	}

	var results = new ConcurrentBag<DomainScanResult>();
	var errors = new ConcurrentBag<(string Domain, string Message)>();

	try
	{
		await AnsiConsole.Progress().AutoClear(false).StartAsync(async ctx =>
		{
			var task = ctx.AddTask("Scanning domains", maxValue: domains.Count);

			await Parallel.ForEachAsync(domains, new ParallelOptions
			{
				MaxDegreeOfParallelism = Math.Max(1, options.Concurrency),
				CancellationToken = cts.Token
			}, async (domain, token) =>
			{
				try
				{
					var result = await scanner.ScanAsync(domain, token);
					results.Add(result);
				}
				catch (Exception ex)
				{
					errors.Add((Domain: domain, Message: ex.Message));
					if (options.Verbose)
					{
						AnsiConsole.MarkupLine($"[red]{domain}[/]: {ex.Message}");
					}
				}
				finally
				{
					task.Increment(1);
				}
			});
		});
	}
	catch (OperationCanceledException)
	{
		AnsiConsole.MarkupLine("[yellow]Scan cancelled by user.[/]");
	}

	var orderedResults = results.OrderBy(r => r.Domain).ToList();
	await WriteCsvAsync(options.OutputPath, orderedResults, cts.Token);
	PrintSummary(orderedResults, errors, domains.Count);

	AnsiConsole.MarkupLine($"[green]Results written to {options.OutputPath}[/]");

	return 0;
}

static async Task<int> RunJobSearchAsync(string[] args)
{
	var options = JobSearchOptions.Parse(args);
	if (options.ShowHelp)
	{
		JobSearchOptions.PrintUsage();
		return 0;
	}

	if (string.IsNullOrWhiteSpace(options.Query))
	{
		AnsiConsole.MarkupLine("[red]Job search query is required. Use --query or -q to specify.[/]");
		JobSearchOptions.PrintUsage();
		return 1;
	}

	using var cts = new CancellationTokenSource();
	Console.CancelKeyPress += (_, eventArgs) =>
	{
		AnsiConsole.MarkupLine("[yellow]\nCancellation requested...[/]");
		cts.Cancel();
		eventArgs.Cancel = true;
	};

	using var host = Host.CreateDefaultBuilder(args)
		.ConfigureLogging(logging =>
		{
			logging.ClearProviders();
			logging.AddSimpleConsole(o =>
			{
				o.SingleLine = true;
				o.TimestampFormat = "HH:mm:ss ";
			});
			logging.SetMinimumLevel(options.Verbose ? LogLevel.Debug : LogLevel.Information);
		})
		.ConfigureServices(services =>
		{
			services.AddHttpClient("jobsearch", client =>
			{
				client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
				client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
				client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
			})
			.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
			{
				AutomaticDecompression = DecompressionMethods.All,
				AllowAutoRedirect = true
			});

			services.AddTransient<IndeedJobSource>();
		})
		.Build();

	IJobBoardSource source = options.Source.ToLowerInvariant() switch
	{
		"indeed" => host.Services.GetRequiredService<IndeedJobSource>(),
		_ => throw new InvalidOperationException($"Unknown job board source '{options.Source}'.")
	};

	var request = new JobSearchRequest
	{
		Query = options.Query,
		Location = options.Location,
		Limit = options.Limit,
		MaxPages = options.MaxPages
	};

	var jobs = new List<JobListing>();

	try
	{
		await AnsiConsole.Status().AutoRefresh(true).StartAsync("Searching for jobs...", async ctx =>
		{
			await foreach (var job in source.SearchAsync(request, cts.Token))
			{
				jobs.Add(job);
				if (options.Verbose)
				{
					ctx.Status = $"Found {jobs.Count} jobs";
				}

				if (options.Limit.HasValue && jobs.Count >= options.Limit.Value)
				{
					break;
				}
			}
		});
	}
	catch (OperationCanceledException)
	{
		AnsiConsole.MarkupLine("[yellow]Job search cancelled by user.[/]");
	}
	catch (Exception ex)
	{
		AnsiConsole.MarkupLine($"[red]Job search failed: {ex.Message}[/]");
		return 1;
	}

	if (jobs.Count == 0)
	{
		AnsiConsole.MarkupLine("[yellow]No jobs found matching your criteria.[/]");
		return 0;
	}

	await WriteJobsCsvAsync(options.OutputPath, jobs, options.IncludeLinkedIn, cts.Token);

	var table = new Table().Border(TableBorder.Rounded);
	table.AddColumn("Source");
	table.AddColumn("Jobs Found");
	table.AddColumn("Output");
	table.AddRow(options.Source, jobs.Count.ToString(CultureInfo.InvariantCulture), options.OutputPath);
	AnsiConsole.Write(table);

	if (options.IncludeLinkedIn)
	{
		AnsiConsole.MarkupLine("[green]LinkedIn search URLs included in CSV output.[/]");
	}

	return 0;
}

static async Task<int> RunScoutAsync(string[] args)
{
	var options = ScoutOptions.Parse(args);
	if (options.ShowHelp)
	{
		ScoutOptions.PrintUsage();
		return 0;
	}

	using var cts = new CancellationTokenSource();
	Console.CancelKeyPress += (_, eventArgs) =>
	{
		AnsiConsole.MarkupLine("[yellow]\nCancellation requested...[/]");
		cts.Cancel();
		eventArgs.Cancel = true;
	};

	using var host = Host.CreateDefaultBuilder(args)
		.ConfigureLogging(logging =>
		{
			logging.ClearProviders();
			logging.AddSimpleConsole(o =>
			{
				o.SingleLine = true;
				o.TimestampFormat = "HH:mm:ss ";
			});
			logging.SetMinimumLevel(options.Verbose ? LogLevel.Debug : LogLevel.Information);
		})
		.ConfigureServices(services =>
		{
			services.AddHttpClient("scout", client =>
			{
				client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
				client.DefaultRequestHeaders.UserAgent.ParseAdd("StackRadar/0.1 (+https://github.com/)");
				client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
			})
			.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
			{
				AutomaticDecompression = DecompressionMethods.All,
				AllowAutoRedirect = true
			});

			services.AddTransient<BuiltWithDotNetSource>();
		})
		.Build();

	IDomainSource source = options.Source.ToLowerInvariant() switch
	{
		"builtwithdotnet" => host.Services.GetRequiredService<BuiltWithDotNetSource>(),
		_ => throw new InvalidOperationException($"Unknown source '{options.Source}'.")
	};

	var request = new DomainSourceRequest
	{
		Limit = options.Limit,
		MaxPages = options.MaxPages,
		Query = options.Query
	};

	var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	try
	{
		await AnsiConsole.Status().AutoRefresh(true).StartAsync("Fetching domains...", async ctx =>
		{
			await foreach (var candidate in source.FetchAsync(request, cts.Token))
			{
				if (discovered.Add(candidate.Domain) && options.Verbose)
				{
					ctx.Status = $"Fetched {discovered.Count} domains";
				}

				if (options.Limit.HasValue && discovered.Count >= options.Limit.Value)
				{
					break;
				}
			}
		});
	}
	catch (OperationCanceledException)
	{
		AnsiConsole.MarkupLine("[yellow]Scouting cancelled by user.[/]");
	}
	catch (Exception ex)
	{
		AnsiConsole.MarkupLine($"[red]Scouting failed: {ex.Message}[/]");
		return 1;
	}

	if (discovered.Count == 0)
	{
		AnsiConsole.MarkupLine("[yellow]No domains discovered from the source.[/]");
		return 0;
	}

	var ordered = discovered.OrderBy(d => d).ToList();
	await WriteTargetsAsync(options.OutputPath, ordered, options.Append, cts.Token);

	var table = new Table().Border(TableBorder.Rounded);
	table.AddColumn("Source");
	table.AddColumn("Domains");
	table.AddColumn("Output");
	table.AddRow(options.Source, ordered.Count.ToString(CultureInfo.InvariantCulture), options.OutputPath);
	AnsiConsole.Write(table);

	return 0;
}

static async Task<IReadOnlyList<string>> LoadDomainsAsync(string path, CancellationToken cancellationToken)
{
	var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	await foreach (var line in File.ReadLinesAsync(path, cancellationToken))
	{
		var trimmed = line.Trim();
		if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
		{
			continue;
		}

		if (!trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase))
		{
			trimmed = trimmed.TrimStart('.');
		}

		set.Add(trimmed);
	}

	return set.ToList();
}

static void PrintSummary(IEnumerable<DomainScanResult> results, IEnumerable<(string Domain, string Message)> errors, int total)
{
	var list = results.ToList();
	var aspNetCount = list.Count(r => r.IsAspNet == true);
	var borderline = list.Count(r => r.IsAspNet is null);
	var avgScore = list.Count > 0 ? list.Average(r => r.Score) : 0;

	var table = new Table().Border(TableBorder.Rounded);
	table.AddColumn("Metric");
	table.AddColumn("Value");
	table.AddRow("Scanned", total.ToString(CultureInfo.InvariantCulture));
	table.AddRow("Completed", list.Count.ToString(CultureInfo.InvariantCulture));
	table.AddRow("Likely ASP.NET", aspNetCount.ToString(CultureInfo.InvariantCulture));
	table.AddRow("Borderline", borderline.ToString(CultureInfo.InvariantCulture));
	table.AddRow("Average Score", avgScore.ToString("0.0", CultureInfo.InvariantCulture));
	table.AddRow("Errors", errors.Count().ToString(CultureInfo.InvariantCulture));

	AnsiConsole.Write(table);

	if (errors.Any())
	{
		var rows = errors.Select(e => $"[red]{e.Domain}[/]: {e.Message}");
		var panel = new Panel(new Markup(string.Join(Environment.NewLine, rows)))
		{
			Header = new PanelHeader("Errors"),
			Border = BoxBorder.Rounded,
			Expand = true
		};
		AnsiConsole.Write(panel);
	}
}

static async Task WriteCsvAsync(string outputPath, IEnumerable<DomainScanResult> results, CancellationToken cancellationToken)
{
	Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");

	await using var stream = File.Create(outputPath);
	await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
	await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
	{
		HasHeaderRecord = true
	});

	csv.WriteHeader<CsvResultRow>();
	await csv.NextRecordAsync();

	foreach (var result in results)
	{
		var row = new CsvResultRow
		{
			Domain = result.Domain,
			IsAspNet = result.IsAspNet?.ToString() ?? "Unknown",
			Score = result.Score,
			Confidence = result.Confidence.ToString(),
			Server = result.Server ?? string.Empty,
			Signals = string.Join(" | ", result.Evidence.Select(e => $"{e.Signal}:{e.Weight}")),
			Notes = string.Join(" | ", result.Notes)
		};

		csv.WriteRecord(row);
		await csv.NextRecordAsync();
	}
}

static async Task WriteTargetsAsync(string path, IEnumerable<string> domains, bool append, CancellationToken cancellationToken)
{
	var fullPath = Path.GetFullPath(path);
	Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
	await using var stream = new FileStream(fullPath, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read);
	await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
	foreach (var domain in domains)
	{
		cancellationToken.ThrowIfCancellationRequested();
		await writer.WriteLineAsync(domain);
	}
}

static async Task WriteJobsCsvAsync(string outputPath, IEnumerable<JobListing> jobs, bool includeLinkedIn, CancellationToken cancellationToken)
{
	Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");

	await using var stream = File.Create(outputPath);
	await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
	await using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
	{
		HasHeaderRecord = true
	});

	csv.WriteHeader<JobCsvRow>();
	await csv.NextRecordAsync();

	foreach (var job in jobs)
	{
		var row = new JobCsvRow
		{
			Title = job.Title,
			Company = job.Company,
			Location = job.Location,
			Description = job.Description,
			Url = job.Url,
			Source = job.Source,
			CompanyLinkedInUrl = includeLinkedIn ? job.GenerateCompanyLinkedInUrl() : string.Empty,
			PeopleLinkedInUrl = includeLinkedIn ? job.GeneratePeopleLinkedInUrl() : string.Empty,
			RetrievedAt = job.RetrievedAt.ToString("yyyy-MM-dd HH:mm:ss")
		};

		csv.WriteRecord(row);
		await csv.NextRecordAsync();
	}
}

internal sealed record JobCsvRow
{
	public string Title { get; init; } = string.Empty;
	public string Company { get; init; } = string.Empty;
	public string Location { get; init; } = string.Empty;
	public string Description { get; init; } = string.Empty;
	public string Url { get; init; } = string.Empty;
	public string Source { get; init; } = string.Empty;
	public string CompanyLinkedInUrl { get; init; } = string.Empty;
	public string PeopleLinkedInUrl { get; init; } = string.Empty;
	public string RetrievedAt { get; init; } = string.Empty;
}

internal sealed record CsvResultRow
{
	public string Domain { get; init; } = string.Empty;
	public string IsAspNet { get; init; } = string.Empty;
	public int Score { get; init; }
	public string Confidence { get; init; } = string.Empty;
	public string Server { get; init; } = string.Empty;
	public string Signals { get; init; } = string.Empty;
	public string Notes { get; init; } = string.Empty;
}

internal sealed record CliOptions
{
	public string InputPath { get; init; } = "targets.txt";
	public string OutputPath { get; init; } = "prospects.csv";
	public int Concurrency { get; init; } = 10;
	public bool Verbose { get; init; }
	public bool ShowHelp { get; init; }
	public int RetryCount { get; init; } = 2;
	public int RequestTimeoutSeconds { get; init; } = 20;
	public bool AllowHttpFallback { get; init; } = true;

	public static CliOptions Parse(string[] args)
	{
		var options = new CliOptions();
		for (var i = 0; i < args.Length; i++)
		{
			var arg = args[i];
			switch (arg)
			{
				case "--input" or "-i":
					options = options with { InputPath = RequireValue(args, ref i, "--input") };
					break;
				case "--output" or "-o":
					options = options with { OutputPath = RequireValue(args, ref i, "--output") };
					break;
				case "--concurrency" or "-c":
					if (int.TryParse(RequireValue(args, ref i, "--concurrency"), out var concurrency))
					{
						options = options with { Concurrency = Math.Clamp(concurrency, 1, 64) };
					}
					break;
				case "--retry":
					if (int.TryParse(RequireValue(args, ref i, "--retry"), out var retry))
					{
						options = options with { RetryCount = Math.Clamp(retry, 0, 5) };
					}
					break;
				case "--timeout":
					if (int.TryParse(RequireValue(args, ref i, "--timeout"), out var timeout))
					{
						options = options with { RequestTimeoutSeconds = Math.Clamp(timeout, 5, 120) };
					}
					break;
				case "--no-http-fallback":
					options = options with { AllowHttpFallback = false };
					break;
				case "--verbose" or "-v":
					options = options with { Verbose = true };
					break;
				case "--help" or "-h":
					options = options with { ShowHelp = true };
					break;
				default:
					AnsiConsole.MarkupLine($"[yellow]Unknown argument: {arg}[/]");
					options = options with { ShowHelp = true };
					break;
			}
		}

		return options;

		static string RequireValue(string[] args, ref int index, string flag)
		{
			if (index + 1 >= args.Length)
			{
				throw new ArgumentException($"Flag {flag} requires a value");
			}

			index++;
			return args[index];
		}
	}

	public static void PrintUsage()
	{
		var table = new Table().Border(TableBorder.HeavyEdge);
		table.AddColumn("Option");
		table.AddColumn("Description");
		table.AddRow("--input, -i", "Path to a targets.txt or CSV containing domains (default: targets.txt)");
		table.AddRow("--output, -o", "Path for the output CSV (default: prospects.csv)");
		table.AddRow("--concurrency, -c", "Number of parallel scans (default: 10)");
		table.AddRow("--retry", "Retry count for transient errors (default: 2)");
		table.AddRow("--timeout", "Per-request timeout in seconds (default: 20)");
		table.AddRow("--no-http-fallback", "Disable HTTP fallback when HTTPS fails");
		table.AddRow("--verbose, -v", "Enable verbose logging output");
		table.AddRow("--help, -h", "Show this help message");
		AnsiConsole.Write(table);
	}
}

internal sealed record ScoutOptions
{
	public string Source { get; init; } = "builtwithdotnet";
	public int? Limit { get; init; } = 200;
	public int? MaxPages { get; init; } = 5;
	public string OutputPath { get; init; } = "discovered.txt";
	public string? Query { get; init; }
	public bool Append { get; init; }
	public bool Verbose { get; init; }
	public bool ShowHelp { get; init; }
	public int TimeoutSeconds { get; init; } = 30;

	public static ScoutOptions Parse(string[] args)
	{
		var options = new ScoutOptions();
		for (var i = 0; i < args.Length; i++)
		{
			var arg = args[i];
			switch (arg)
			{
				case "--source" or "-s":
					options = options with { Source = RequireValue(args, ref i, "--source") };
					break;
				case "--limit" or "-l":
					if (int.TryParse(RequireValue(args, ref i, "--limit"), out var limit))
					{
						options = options with { Limit = Math.Clamp(limit, 1, 10000) };
					}
					break;
				case "--pages" or "-p":
					if (int.TryParse(RequireValue(args, ref i, "--pages"), out var pages))
					{
						options = options with { MaxPages = Math.Clamp(pages, 1, 100) };
					}
					break;
				case "--output" or "-o":
					options = options with { OutputPath = RequireValue(args, ref i, "--output") };
					break;
				case "--query" or "-q":
					options = options with { Query = RequireValue(args, ref i, "--query") };
					break;
				case "--append":
					options = options with { Append = true };
					break;
				case "--timeout":
					if (int.TryParse(RequireValue(args, ref i, "--timeout"), out var timeout))
					{
						options = options with { TimeoutSeconds = Math.Clamp(timeout, 5, 120) };
					}
					break;
				case "--verbose" or "-v":
					options = options with { Verbose = true };
					break;
				case "--help" or "-h":
					options = options with { ShowHelp = true };
					break;
				default:
					AnsiConsole.MarkupLine($"[yellow]Unknown argument: {arg}[/]");
					options = options with { ShowHelp = true };
					break;
			}
		}

		return options;

		static string RequireValue(string[] args, ref int index, string flag)
		{
			if (index + 1 >= args.Length)
			{
				throw new ArgumentException($"Flag {flag} requires a value");
			}

			index++;
			return args[index];
		}
	}

	public static void PrintUsage()
	{
		var table = new Table().Border(TableBorder.HeavyEdge);
		table.AddColumn("Option");
		table.AddColumn("Description");
		table.AddRow("--source, -s", "Domain source to run (default: builtwithdotnet)");
		table.AddRow("--limit, -l", "Maximum domains to retrieve (default: 200)");
		table.AddRow("--pages, -p", "Maximum pages to iterate (default: 5)");
		table.AddRow("--query, -q", "Optional filter (e.g., technology flag)");
		table.AddRow("--output, -o", "File to write domains to (default: discovered.txt)");
		table.AddRow("--append", "Append to output instead of overwrite");
		table.AddRow("--timeout", "Per-request timeout in seconds (default: 30)");
		table.AddRow("--verbose, -v", "Enable verbose logs");
		table.AddRow("--help, -h", "Show this help message");
		AnsiConsole.Write(table);
	}
}

internal sealed record JobSearchOptions
{
	public string Query { get; init; } = string.Empty;
	public string? Location { get; init; }
	public string Source { get; init; } = "indeed";
	public int? Limit { get; init; } = 50;
	public int? MaxPages { get; init; } = 3;
	public string OutputPath { get; init; } = "jobs.csv";
	public bool IncludeLinkedIn { get; init; }
	public bool Verbose { get; init; }
	public bool ShowHelp { get; init; }
	public int TimeoutSeconds { get; init; } = 30;

	public static JobSearchOptions Parse(string[] args)
	{
		var options = new JobSearchOptions();
		for (var i = 0; i < args.Length; i++)
		{
			var arg = args[i];
			switch (arg)
			{
				case "--query" or "-q":
					options = options with { Query = RequireValue(args, ref i, "--query") };
					break;
				case "--location" or "-l":
					options = options with { Location = RequireValue(args, ref i, "--location") };
					break;
				case "--source" or "-s":
					options = options with { Source = RequireValue(args, ref i, "--source") };
					break;
				case "--limit":
					if (int.TryParse(RequireValue(args, ref i, "--limit"), out var limit))
					{
						options = options with { Limit = Math.Clamp(limit, 1, 500) };
					}
					break;
				case "--pages" or "-p":
					if (int.TryParse(RequireValue(args, ref i, "--pages"), out var pages))
					{
						options = options with { MaxPages = Math.Clamp(pages, 1, 10) };
					}
					break;
				case "--output" or "-o":
					options = options with { OutputPath = RequireValue(args, ref i, "--output") };
					break;
				case "--include-linkedin":
					options = options with { IncludeLinkedIn = true };
					break;
				case "--timeout":
					if (int.TryParse(RequireValue(args, ref i, "--timeout"), out var timeout))
					{
						options = options with { TimeoutSeconds = Math.Clamp(timeout, 5, 120) };
					}
					break;
				case "--verbose" or "-v":
					options = options with { Verbose = true };
					break;
				case "--help" or "-h":
					options = options with { ShowHelp = true };
					break;
				default:
					AnsiConsole.MarkupLine($"[yellow]Unknown argument: {arg}[/]");
					options = options with { ShowHelp = true };
					break;
			}
		}

		return options;

		static string RequireValue(string[] args, ref int index, string flag)
		{
			if (index + 1 >= args.Length)
			{
				throw new ArgumentException($"Flag {flag} requires a value");
			}

			index++;
			return args[index];
		}
	}

	public static void PrintUsage()
	{
		var table = new Table().Border(TableBorder.HeavyEdge);
		table.AddColumn("Option");
		table.AddColumn("Description");
		table.AddRow("--query, -q", "Job search query (e.g., 'ASP.NET Developer') (required)");
		table.AddRow("--location, -l", "Job location (e.g., 'New York, NY')");
		table.AddRow("--source, -s", "Job board source (default: indeed)");
		table.AddRow("--limit", "Maximum jobs to retrieve (default: 50)");
		table.AddRow("--pages, -p", "Maximum pages to iterate (default: 3)");
		table.AddRow("--output, -o", "CSV file to write results (default: jobs.csv)");
		table.AddRow("--include-linkedin", "Include LinkedIn search URLs for companies and staff");
		table.AddRow("--timeout", "Per-request timeout in seconds (default: 30)");
		table.AddRow("--verbose, -v", "Enable verbose logs");
		table.AddRow("--help, -h", "Show this help message");
		AnsiConsole.Write(table);
	}
}
