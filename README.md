# StackRadar

StackRadar is a .NET 8 command-line scanner that fingerprints domains to detect ASP.NET / ASP.NET Core technology usage using multi-signal heuristics (headers, cookies, HTML markers). It complements the engineering spec in `stackradar_spec.md` and the sourcing playbook in `docs/`.

## Projects

- `StackRadar.Core` – detection engine, scoring rules, HTTP scanner implementation, and domain scouting connectors.
- `StackRadar.Cli` – Spectre.Console powered CLI for scouting domains and scanning them with results exported to CSV.
- `docs/` – product and sourcing specifications, detection requirements.

## Quick start

1. Install the .NET 8 SDK (`https://dotnet.microsoft.com/download/dotnet/8.0`).
2. Restore and build:

```bash
dotnet build
```

## Usage

StackRadar provides two main commands:

### 1. Scout - Discover domains automatically

Use the `scout` command to automatically discover .NET domains from online sources:

```bash
# Discover domains from builtwithdot.net
dotnet run --project StackRadar.Cli -- scout --limit 200 --output discovered.txt

# Filter by specific technology
dotnet run --project StackRadar.Cli -- scout --query "Blazor" --limit 100

# Show detailed progress
dotnet run --project StackRadar.Cli -- scout --verbose --limit 50

# Append to existing file instead of overwriting
dotnet run --project StackRadar.Cli -- scout --output domains.txt --append
```

**Scout options:**
- `--source, -s` – Domain source to use (default: `builtwithdotnet`)
- `--limit, -l` – Maximum domains to retrieve (default: `200`)
- `--pages, -p` – Maximum pages to iterate (default: `5`)
- `--query, -q` – Optional technology filter (e.g., "Blazor", "ASP.NET Core")
- `--output, -o` – File to write domains to (default: `discovered.txt`)
- `--append` – Append to output file instead of overwriting
- `--timeout` – Per-request timeout in seconds (default: `30`)
- `--verbose, -v` – Enable verbose logging
- `--help, -h` – Show help message

### 2. Scan - Fingerprint domains

Use the scan command (default) to fingerprint domains and detect ASP.NET usage:

```bash
# Scan domains from a file
dotnet run --project StackRadar.Cli -- --input targets.txt --output prospects.csv

# Adjust concurrency and timeout
dotnet run --project StackRadar.Cli -- --input targets.txt --concurrency 20 --timeout 30

# Enable verbose logging for debugging
dotnet run --project StackRadar.Cli -- --input targets.txt --verbose
```

**Scan options:**
- `--input, -i` – Path to file containing domains (default: `targets.txt`)
- `--output, -o` – Path for output CSV (default: `prospects.csv`)
- `--concurrency, -c` – Number of parallel scans (default: `10`)
- `--retry` – Retry count for transient errors (default: `2`)
- `--timeout` – Per-request timeout in seconds (default: `20`)
- `--no-http-fallback` – Disable HTTP fallback when HTTPS fails
- `--verbose, -v` – Enable verbose logging
- `--help, -h` – Show help message

### Complete workflow example

Discover domains and then scan them:

```bash
# Step 1: Scout for .NET domains
dotnet run --project StackRadar.Cli -- scout --limit 100 --output discovered.txt

# Step 2: Scan the discovered domains
dotnet run --project StackRadar.Cli -- --input discovered.txt --output results.csv --verbose
```

The results are written to CSV including domain, score, confidence band, detected signals, and diagnostic notes.

## Domain sources

Currently supported scouting sources:
- **builtwithdotnet** – Queries the public API at [https://builtwithdot.net](https://builtwithdot.net) to find websites built with .NET technologies

Additional sources (BuiltWith API, search dorks, Certificate Transparency logs) are documented in `docs/domain_scouting_strategy.md` and can be implemented following the `IDomainSource` interface.

## Tests

Automated tests are coming soon; for now rely on `dotnet build` and `dotnet run` to validate behaviour.
