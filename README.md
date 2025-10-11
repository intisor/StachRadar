# StackRadar

StackRadar is a .NET 8 command-line scanner that fingerprints domains to detect ASP.NET / ASP.NET Core technology usage using multi-signal heuristics (headers, cookies, HTML markers). It complements the engineering spec in `stackradar_spec.md` and the sourcing playbook in `docs/`.

## Projects

- `StackRadar.Core` – detection engine, scoring rules, HTTP scanner implementation.
- `StackRadar.Cli` – Spectre.Console powered CLI for scanning a list of domains and exporting results to CSV.
- `docs/` – product and sourcing specifications, detection requirements.

## Quick start

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

## Tests

Automated tests are coming soon; for now rely on `dotnet build` and `dotnet run` to validate behaviour.
