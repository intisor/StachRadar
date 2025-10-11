# StackRadar Engineering Specification

**Tech Stack Scanner / Prospect Finder**

A founder-level technical requirement and design blueprint for building a real-world engineering solution.

---

## ⚙️ 1. PRODUCT OVERVIEW

### Product Name (working title)

**StackRadar** — a C#-based web technology fingerprinting and prospect-mining tool focused on discovering websites built with .NET / ASP.NET technologies.

### Core Mission

> Identify, analyze, and rank live websites using .NET/C# stacks (legacy or modern) — enabling you to find upgrade opportunities, pitch optimization, and generate potential client leads.

### Core Value

* You **discover real companies** running outdated or inefficient .NET-based sites.
* You **help them modernize**, optimize, or migrate — for a fee.
* You build a **repeatable data-driven pipeline** that feeds your consulting hustle.

---

## 🧠 2. SYSTEM REQUIREMENTS

### 2.1 Functional Requirements

| ID    | Requirement                      | Description                                                                                      |
| ----- | -------------------------------- | ------------------------------------------------------------------------------------------------ |
| FR-1  | Domain Source Input              | Accept list of domains via `targets.txt`, CSV, or URL search.                                    |
| FR-2  | Tech Fingerprinting              | Identify if a site uses ASP.NET, ASP.NET Core, or related frameworks using multiple heuristics.  |
| FR-3  | Metadata Extraction              | Fetch headers, cookies, HTML snippets, and server info.                                          |
| FR-4  | Heuristic Scoring                | Assign confidence scores based on detected signals (headers, HTML, cookies).                     |
| FR-5  | Concurrency & Rate-Limiting      | Perform concurrent scans (e.g., 10–50 simultaneous requests) with rate control to avoid bans.    |
| FR-6  | Result Output                    | Export structured data in CSV/JSON (for Airtable or Excel).                                      |
| FR-7  | Optional GitHub/WHOIS Enrichment | Query GitHub Search API and WHOIS to enrich company/org details.                                 |
| FR-8  | Command-Line Interface (CLI)     | Provide interactive CLI with options and progress display.                                       |
| FR-9  | Resume Support                   | Allow scanning to resume from the last point if interrupted.                                     |
| FR-10 | Summary Reporting                | Generate a scan summary (e.g., number of ASP.NET sites found, total scanned, avg response time). |

### 2.2 Non-Functional Requirements

| ID    | Category        | Requirement                                                                       |
| ----- | --------------- | --------------------------------------------------------------------------------- |
| NFR-1 | Performance     | Must handle 1000+ domains per run on a mid-range laptop with <2GB RAM usage.      |
| NFR-2 | Reliability     | Retry failed requests up to 3 times with exponential backoff.                     |
| NFR-3 | Scalability     | Modular design for future scaling (e.g., microservice or Azure Function version). |
| NFR-4 | Portability     | Cross-platform (Windows, Linux, macOS) via .NET 8 runtime.                        |
| NFR-5 | Security        | No sensitive credential logging. HTTPS-only requests.                             |
| NFR-6 | Maintainability | Clear modular structure and documented code (XML comments).                       |
| NFR-7 | Ethics          | Must respect robots.txt (optional flag for compliance mode).                      |

---

## 🧩 3. SYSTEM ARCHITECTURE DESIGN

### 3.1 High-Level Architecture

```
+------------------------------------------------------------+
|                        StackRadar CLI                      |
|-------------------------------------------------------------|
| 1. Input Layer      | 2. Scanner Engine  | 3. Output Layer  |
|-------------------------------------------------------------|
| - File Reader       | - HttpClient Pool  | - CSV Exporter   |
| - Search Fetcher    | - Detector Engine  | - JSON Exporter  |
| - Domain Validator  | - Scoring System   | - Report Builder |
|-------------------------------------------------------------|
| 4. Enrichment Layer (Optional)                             |
| - GitHub API Client  - WHOIS Lookup  - DNS Resolver         |
+------------------------------------------------------------+
```

### 3.2 Component Details

#### **Input Layer**

* **FileInputService** → Reads domain list from file.
* **SearchInputService (future)** → Uses Bing Custom Search API or open web scraping to find sites matching "ASP.NET", ".aspx", ".net CMS", etc.
* **DomainValidator** → Validates domain syntax, ensures unique entries.
* **DomainSourceConnectors** → Modular integrations (BuiltWith, public tech directories, CT logs) implementing a common `IDomainSource` contract to stream candidate domains into the scanner queue.
* **DomainQueue** → Lightweight SQLite/Redis store that deduplicates domains across sources and supports resume functionality for long-running scouts.

**CLI Sourcing Commands**

* `stackradar scout --source builtwith --max 500` → Pulls latest domains tagged with ASP.NET from the BuiltWith API.
* `stackradar scout --source search --query "ViewState" --pages 3` → Executes search dorks via Bing Custom Search to uncover long-tail leads.
* `stackradar scout --ingest targets.csv` → Ingests bulk CSV exports (e.g., BuiltWith lists) and normalizes into the domain queue.

#### **Scanner Engine**

* **HttpClientFactory** → Manages a pool of clients for concurrent requests.
* **DetectionEngine** → Core logic that inspects:
  * Headers (`X-AspNet-Version`, `X-Powered-By`)
  * HTML (`__VIEWSTATE`, `.aspx`, Razor syntax)
  * Cookies (`.AspNet`, `.AspNetCore.Antiforgery`)
* **ScoreEngine** → Assigns scores (0–10) for likelihood of .NET usage.
* **ResultAggregator** → Collects, deduplicates, and normalizes results.

#### **Output Layer**

* **CsvExporter** → Outputs `prospects.csv`.
* **JsonExporter** → Optional JSON output for API or dashboards.
* **ReportBuilder** → Creates readable summary (counts, percentages, timestamps).

#### **Enrichment Layer (optional)**

* **GitHubEnricher** → Uses GitHub Search API to find repos linked to domain.
* **WhoisEnricher** → Gets WHOIS `OrgName`, `CreatedDate`, and `Country`.
* **DnsEnricher** → Pulls MX and NS records (indirect company insight).

#### **CLI Interface**

* Built with **Spectre.Console**:
  * Flags:
    * `--input` : file path
    * `--concurrency` : number of parallel scans
    * `--github` : enable GitHub enrichment
    * `--whois` : enable WHOIS enrichment
    * `--output` : output CSV name
  * Real-time progress bar with per-domain status (OK, Timeout, Error).

---

## 🧱 4. DATA DESIGN

### 4.1 Input Format

**targets.txt**

```
example.com
contoso.net
adventureworks.com
```

### 4.2 Output Format

**prospects.csv**

| domain      | isAspNet | score | server             | headers          | htmlSignals | cookies                 | whoisOrg    | githubHits | notes                  |
| ----------- | -------- | ----- | ------------------ | ---------------- | ----------- | ----------------------- | ----------- | ---------- | ---------------------- |
| example.com | true     | 8     | Microsoft-IIS/10.0 | X-AspNet-Version | __VIEWSTATE | .AspNetCore.Antiforgery | Contoso Ltd | 3          | Likely legacy WebForms |
| contoso.net | false    | 0     | Apache             |                  |             |                         |             | 0          |                        |

---

## 🧰 5. TECHNOLOGY STACK

| Layer        | Tool / Library                               | Reason                        |
| ------------ | -------------------------------------------- | ----------------------------- |
| Core Runtime | .NET 8 SDK                                   | Cross-platform, performant    |
| HTTP         | `System.Net.Http`, `HttpClientFactory`       | Reliable and async            |
| HTML Parsing | `HtmlAgilityPack` or `AngleSharp`            | HTML inspection               |
| CLI          | `Spectre.Console`                            | Rich CLI experience           |
| CSV          | `CsvHelper`                                  | Clean, fast CSV I/O           |
| Logging      | `Serilog`                                    | Structured logs               |
| Concurrency  | `Task.WhenAll`, `Polly`                      | Resilience and retry          |
| Enrichment   | `Octokit` (GitHub), `Whois.NET`, `DnsClient` | Data enrichment               |
| Build        | `dotnet tool`                                | Portable global CLI installer |
| Testing      | `xUnit`, `Moq`                               | Unit and integration tests    |

---

## 🧮 6. LOGIC & ALGORITHMIC FLOW

**Pseudocode Overview**

```
Main()
 ├─ Load domains from input file
 ├─ Initialize HttpClientFactory
 ├─ For each domain (async concurrent)
 │   ├─ Fetch headers & content
 │   ├─ Detect ASP.NET signatures
 │   ├─ Score confidence
 │   ├─ If enrichment enabled:
 │   │    ├─ WHOIS + GitHub lookups
 │   ├─ Append results to memory
 ├─ Export to CSV
 ├─ Show summary report
 └─ Exit
```

**Detection Heuristics Table**

| Signal                           | Type   | Weight |
| -------------------------------- | ------ | ------ |
| Header: `X-AspNet-Version`       | Header | +3     |
| Header: `X-Powered-By: ASP.NET`  | Header | +2     |
| Server: `Microsoft-IIS/*`        | Header | +2     |
| Cookie: `.AspNet`                | Cookie | +2     |
| HTML: `__VIEWSTATE`              | HTML   | +3     |
| HTML: `.aspx`                    | HTML   | +2     |
| HTML: `aspnet_form`              | HTML   | +1     |
| HTML: `@model` / `Razor` markers | HTML   | +2     |

**Scoring Rule:** Score > 5 → `isAspNet = true`.

---

## 📊 7. MVP PHASE ROADMAP (0–3 months)

| Phase                      | Duration  | Goals                                            |
| -------------------------- | --------- | ------------------------------------------------ |
| Phase 1: Core Scanner      | Week 1–2  | Implement CLI input, basic detection, CSV output |
| Phase 2: Heuristic Engine  | Week 3–4  | Add HTML parsing, cookies, scoring system        |
| Phase 3: Enrichment        | Week 5–6  | WHOIS + GitHub                                   |
| Phase 4: UX + Optimization | Week 7–8  | CLI polish, summary report                       |
| Phase 5: Cloud Ready       | Week 9–12 | Add Web API wrapper or dashboard                 |

---

## 🚀 8. FUTURE EXPANSION PATH

1. **Dashboard / SaaS UI**  
   Host results in a web dashboard (Blazor/Next.js frontend).

2. **API-as-a-Service**  
   "StackRadar API" — endpoint where anyone can submit domains for scanning.

3. **Lead Scoring**  
   Use ML (or rules) to predict likelihood of "needs upgrade".

4. **Automated Outreach**  
   Integrate email templates for discovered prospects (with consent).

5. **.NET Site Analyzer Plugin**  
   Add deep audits (e.g., ASP.NET version, potential vulnerabilities, page speed).

---

## 💡 9. KEY DIFFERENTIATORS

* **Focused niche** — specifically for .NET/C# stacks (not generic).
* **Self-hosted + free** — no dependence on paid APIs.
* **Multi-signal detection** — headers + cookies + HTML + DNS + WHOIS.
* **Extensible design** — add new detectors or enrichment sources easily.
* **Consulting-first use case** — turns technical reconnaissance into billable work.

---

## 🎯 Next Steps

Choose your path forward:

1. **Technical architecture document** (with class diagrams + flow diagrams + interface design)
2. **Code skeleton** (Program.cs + folders + sample output) for MVP implementation