# Detection Decision Requirements

## Purpose
Define deterministic rules for classifying whether a scanned domain is likely powered by ASP.NET or related .NET web stacks. The detection logic must be explainable, tunable, and resilient to noisy signals.

## Signal Categories

1. **HTTP Headers**
   - `X-AspNet-Version`: version string, e.g., `4.0.30319`. High-confidence.
   - `X-Powered-By`: contains `ASP.NET` or `ASP.NET Core`.
   - `Server`: contains `Microsoft-IIS`, `Kestrel`, or `Azure-FrontDoor`.
   - `X-SourceFiles`: specific to Visual Studio debugging deployments (legacy sites).
2. **HTML Markers**
   - `__VIEWSTATE`, `__EVENTVALIDATION` hidden fields (WebForms).
   - Form IDs containing `aspnetForm`, `ctl00`, or `form1`.
   - Razor directives: `@model`, `@{`, `asp-validation-summary`.
   - Static asset references: `.axd`, `.aspx`, `.cshtml`, `.vbhtml`.
3. **Cookies**
   - Names starting with `.AspNet`, `.ASPXAUTH`, `.AspNetCore`.
   - Antiforgery tokens: `.AspNetCore.Antiforgery.*` or `__RequestVerificationToken`.
4. **Response Metadata**
   - Status code 200–399 (successful retrieval).
   - TLS certificate issuer referencing `Microsoft` or `Azure` (optional enrichment).
   - Redirect chains that land on `.aspx` endpoints.
5. **Fallback Enrichment (optional)**
   - DNS TXT entries containing `azurewebsites.net`, `trafficmanager.net`.
   - WHOIS organization referencing Microsoft hosting partners.
   - GitHub repositories referencing `<domain> AND .NET`.

## Confidence Scoring

| Score Band | Meaning | Criteria |
| --- | --- | --- |
| 0–2 | Low confidence | Only indirect signals (e.g., IIS without ASP.NET markers). |
| 3–5 | Medium | Mixed signals; requires follow-up or rescan. |
| 6–8 | High | Multiple direct markers (e.g., `X-AspNet-Version` + `__VIEWSTATE`). |
| 9–10 | Certain | Header + HTML + cookie alignment or verified enrichment. |

### Weight Table

| Signal | Weight | Notes |
| --- | --- | --- |
| `X-AspNet-Version` | +4 | High-fidelity; rarely spoofed. |
| `X-Powered-By: ASP.NET` | +3 | Common header for legacy sites. |
| `Server: Microsoft-IIS/*` | +2 | IIS can host non-ASP.NET; medium weight. |
| `Cookie: .AspNet` | +3 | Strong indicator of ASP.NET runtime. |
| `Cookie: .AspNetCore` | +3 | Strong indicator of ASP.NET Core. |
| HTML `__VIEWSTATE` | +4 | WebForms-specific. |
| HTML `.cshtml` reference | +2 | Could be static mention; moderate weight. |
| HTML Razor directives (`@model`) | +3 | Strong indicator of Razor views. |
| Response final URL ends with `.aspx` | +2 | Might be marketing page; moderate weight. |
| WHOIS org indicates Microsoft hosting | +1 | Supplemental. |
| GitHub repo match | +1 | Supplemental; cross-check company stack. |

### Classification Rules

1. **Immediate Accept**: Score ≥ 7 **and** at least one direct marker (headers or HTML). Mark as `IsAspNet = true`.
2. **Borderline Review**: Score between 4 and 6. Mark as `IsAspNet = undetermined` and enqueue for manual review or rescan.
3. **Reject**: Score ≤ 3. Mark as `IsAspNet = false` unless enrichment indicates otherwise.
4. **Override**: Allow configuration flag to force `IsAspNet = true/false` for known domains (whitelist/blacklist).

## False-Positive Mitigation

- Ignore `Server: Microsoft-Azure-Application-Gateway` without supporting evidence.
- Treat minified JS containing `.aspx` as noise unless the substring occurs in HTML markup.
- Collapse duplicate signals from redirects to avoid double-counting.
- Use charset-insensitive comparisons; normalize to lowercase before matching.

## Rescan Logic

- If the score is 4–6 or the request failed, schedule a rescan after `RescanInterval` (default: 7 days).
- Maintain `SignalHistory` table to compare drift; if signals disappear, downgrade confidence.

## Telemetry Requirements

- Log each evaluated signal with weight and source (`Header`, `HTML`, `Cookie`, `Enrichment`).
- Emit metrics: `TotalSignals`, `PositiveSignals`, `Score`, `ConfidenceBand`.
- Store raw HTML snippet (max 1 KB) for high-confidence matches to review heuristics.

## Configuration Options

- `Detection.MaxHtmlBytes` (default 256 KB) to limit download size.
- `Detection.EnableJavaScriptRendering` (default false) for future headless scans.
- `Detection.MinimumScoreForTrue` (default 7) to adjust classification threshold.
- `Detection.RescanIntervalDays` (default 7) controlling borderline rechecks.

## Acceptance Criteria

1. Given a response with `X-AspNet-Version`, `__VIEWSTATE`, and `.AspNetCore` cookie, the computed score must be ≥ 9 and classification `true`.
2. Given only `Server: Microsoft-IIS`, score ≤ 2 and classification `false`.
3. Given `X-Powered-By: ASP.NET` plus `.AspNetCore` cookie, score ≥ 6; classification toggles true when threshold set to ≤ 6.
4. Borderline cases trigger rescan scheduling with status `PendingReview`.
5. Telemetry entries persist each signal evaluation with timestamp and domain.
