# StackRadar

StackRadar is a .NET 8 command-line tool that helps you:
1. **Scan domains** to detect ASP.NET / ASP.NET Core technology usage using multi-signal heuristics (headers, cookies, HTML markers)
2. **Search job boards** for .NET positions and extract company information with LinkedIn integration
3. **Discover domains** running .NET technologies from various sources

It complements the engineering spec in `stackradar_spec.md` and the sourcing playbook in `docs/`.

## Projects

- `StackRadar.Core` – detection engine, scoring rules, HTTP scanner implementation, job board scraping.
- `StackRadar.Cli` – Spectre.Console powered CLI for scanning domains, searching jobs, and exporting results to CSV.
- `docs/` – product and sourcing specifications, detection requirements, usage guides.

## Commands

StackRadar supports three main commands:

1. **Default (scan)** - Scan domains for ASP.NET technology stack
2. **scout** - Discover domains running .NET from various sources  
3. **job-search** - Search job boards and generate LinkedIn URLs (NEW)

## Quick start

### Technology Stack Scanning

1. Install the .NET 8 SDK (`https://dotnet.microsoft.com/download/dotnet/8.0`).
2. Restore and build:

```pwsh
cd stackTracer
 dotnet build
```

3. Prepare a `targets.txt` file in the workspace root (one domain per line).
4. Run the scanner:

```pwsh
 dotnet run --project StackRadar.Cli -- --input targets.txt --output prospects.csv --concurrency 10
```

> Tip: pass `--verbose` to see detailed logging, `--retry` and `--timeout` to tune resilience, and `--no-http-fallback` to force HTTPS-only scans.

The results are written to `prospects.csv` including scores, confidence bands, detected signals, and notes.

### Job Search & LinkedIn Integration (New!)

Search job boards for ASP.NET positions and generate LinkedIn URLs to research companies and their staff:

```pwsh
# Basic job search
dotnet run --project StackRadar.Cli -- job-search --query "ASP.NET Developer" --output jobs.csv

# Include LinkedIn URLs for company and people research
dotnet run --project StackRadar.Cli -- job-search --query ".NET Core" --location "New York" --include-linkedin

# Advanced search with more results
dotnet run --project StackRadar.Cli -- job-search --query "C# Developer" --limit 100 --pages 5 --verbose
```

**Key Features:**
- Automated scraping from Indeed job boards
- Company name extraction from job listings
- LinkedIn search URL generation for companies and staff
- CSV export compatible with Excel and Airtable
- Built-in rate limiting and respectful scraping

See `docs/job_search_usage.md` for complete documentation and usage examples.

### Domain Discovery

Discover domains running .NET technologies from various sources:

```pwsh
dotnet run --project StackRadar.Cli -- scout --source builtwithdotnet --limit 200 --output discovered.txt
```

## Tests

Automated tests are coming soon; for now rely on `dotnet build` and `dotnet run` to validate behaviour.
