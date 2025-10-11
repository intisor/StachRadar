# StackRadar Domain Scouting Strategy

## Goal
Design a repeatable pipeline that continuously discovers candidate domains with a high likelihood of running on .NET / ASP.NET stacks. The sourcing layer should feed fresh leads into the main scanner without manual curation, operate within reasonable API budgets, and comply with legal/ethical constraints.

## Discovery Channels

| Channel | Description | Access Method | Notes |
| --- | --- | --- | --- |
| BuiltWith Technology Lookup | Search for sites classified under ASP.NET, IIS, or other .NET signatures. | REST API (`https://api.builtwith.com/`), requires API key. | High precision, paid tiers; cache responses locally. |
| BuiltWith Lists (CSV exports) | Bulk technology and category lists. | Scheduled export jobs + ingestion. | Useful for seeding `targets.txt` with 1k+ domains. |
| Public Tech Directories | Example: Wappalyzer, WhatRuns, Netcraft, SimilarTech. | RSS feeds, public datasets, or partner APIs. | Mind licensing restrictions; some require attribution. |
| Search Engine Dorks | Bing, Google, DuckDuckGo queries like `site:.aspx "ViewState"` or `"Powered by ASP.NET"`. | Bing Custom Search API (paid) or serpapi.com. | Rotate keywords monthly to avoid stale results. |
| GitHub Code Search | Find repos mentioning `.aspx` plus company domains. | GitHub Search API; rate-limited. | Cross-reference `company.com` mentions to infer domain usage. |
| WHOIS Zone Dumps | Country-code TLD registries publishing zone files. | Bulk download where permitted. | Filter by hostnames containing `aspx`, `iis`, etc. |
| Social Signals | LinkedIn, job boards listing ASP.NET requirements. | Scrape (respect ToS) or use APIs. | Good for prospecting companies before domains. |

## Pipeline Architecture

```
+------------------+      +----------------------+      +---------------------+
| Source Connectors| ---> | Candidate Normalizer | ---> | Domain Queue        |
+------------------+      +----------------------+      +---------------------+
         |                          |                             |
         v                          v                             v
 BuiltWith API              Deduplicate (case-insensitive)   Persist to SQLite
 Search API                 Validate DNS resolution          Tag with source
 CSV Imports                Score trustworthiness            Schedule scans
```

### Source Connectors
- Implement each connector as `IDomainSource` with `Task<IAsyncEnumerable<DomainCandidate>> FetchAsync(SourceOptions options)`.
- Provide built-in connectors for BuiltWith, Bing Custom Search, CSV/targets file.
- Back each connector with Polly retry policies and exponential backoff.

### Candidate Normalizer
- Normalize to lower-case punycode domains.
- Reject internal or parked domains (`*.cloudapp.net`, `*.azurewebsites.net`).
- Tag metadata: `{ Source, Confidence, RetrievedAt, RawPayloadHash }`.

### Domain Queue
- Use lightweight SQLite database (`Microsoft.Data.Sqlite`) to persist queued domains.
- Columns: `Domain`, `Source`, `FirstSeen`, `LastFetched`, `Confidence`, `Status`.
- Enforce uniqueness with `UNIQUE(Domain, Source)` constraint.

## BuiltWith Integration Blueprint

1. **Authentication**: store API key encrypted in `appsettings.json` (User Secrets in dev).
2. **Rate Limits**: default 1 request/sec; tune based on subscription tier.
3. **Query Strategy**:
   - Use `/v20/techlookup.json?tech=ASP.NET` for broad lists.
   - Paginate through results; skip domains scanned within last 30 days.
   - Cache responses (ETag-aware) in `Cache/BuiltWith/{hash}.json` to reduce costs.
4. **Error Handling**:
   - Handle HTTP 402 (payment required) gracefully; downgrade to cached data.
   - On 429 (too many requests), pause connector with jittered backoff.

## Hidden/Long-Tail Discovery

- **Reverse DNS on known IIS IP ranges**: map IPs from hosting providers known for .NET hosting (e.g., Azure App Service). Use `DnsClient` to enumerate hostnames, then filter by `.aspx` response signatures.
- **SSL Certificate Transparency (CT) Feeds**: consume `certstream` to find freshly issued certs; look for SAN entries containing `.aspx` or `aspnet` keywords.
- **Passive DNS datasets**: leverage `securitytrails.com` API (paid) or `RiskIQ` for historical hostnames referencing `.ashx`, `.asmx`.
- **Historical HTML archives**: query `waybackpack` for domains with `.aspx` snapshots, reintroduce them to queue if not recently scanned.

## Automation & Scheduling

- Cron-like scheduler (`Quartz.NET`) triggers connectors nightly.
- Each connector publishes `DomainCandidate` records to a shared channel (`System.Threading.Channels`) consumed by the normalizer.
- Maintain per-source health metrics (success rate, average new domains/day).
- Expose CLI command `stackradar scout --source builtwith --max 500` to run ad-hoc fetches.

## Compliance & Ethics

- Respect API terms of service; log request counts and reauth attempts.
- Allow opt-out list for domains requesting removal.
- Avoid scraping sections blocked by `robots.txt` unless user passes `--override-robots` flag with explicit acknowledgement.
- Store source payloads securely; redact personally identifiable information when exporting prospect data.

## Next Implementation Steps

1. Define `IDomainSource` interface and DTOs in `StackRadar.Scouting` project.
2. Implement `BuiltWithDomainSource` with configuration-driven API keys.
3. Add SQLite-backed queue with EF Core or Dapper micro-ORM.
4. Expose `scout` command in CLI and integrate with main scanning workflow.
