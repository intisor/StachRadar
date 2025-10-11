using System.Text.RegularExpressions;
using StackRadar.Core.Models;

namespace StackRadar.Core.Detection;

public sealed class DetectionEngine
{
    private static readonly Regex RazorDirectiveRegex = new(@"@model\s+[\w\.]+|@\{", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly DetectionOptions _options;

    public DetectionEngine(DetectionOptions options)
    {
        _options = options;
    }

    public DetectionOutcome Evaluate(ScanArtifacts artifacts, IEnumerable<DetectionEvidence>? enrichmentEvidence = null)
    {
        var evidence = new List<DetectionEvidence>();

        EvaluateHeaders(artifacts, evidence);
        EvaluateCookies(artifacts, evidence);
        EvaluateHtml(artifacts, evidence);
        EvaluateFinalUrl(artifacts, evidence);

        if (enrichmentEvidence is not null)
        {
            evidence.AddRange(enrichmentEvidence);
        }

        var score = evidence.Sum(e => GetWeight(e.Signal, e.Weight));
        var confidence = CalculateConfidence(score);
        bool? isAspNet = score >= _options.MinimumScoreForTrue
            ? true
            : score < _options.MinimumScoreForBorderline
                ? false
                : null;

        return new DetectionOutcome(score, confidence, isAspNet, evidence);
    }

    private void EvaluateHeaders(ScanArtifacts artifacts, ICollection<DetectionEvidence> evidence)
    {
        if (TryGetHeader(artifacts.Headers, "x-aspnet-version", out var aspNetVersion))
        {
            evidence.Add(new DetectionEvidence(
                DetectionSignal.HeaderAspNetVersion,
                "Header X-AspNet-Version detected",
                aspNetVersion,
                GetWeight(DetectionSignal.HeaderAspNetVersion)));
        }

        if (TryGetHeader(artifacts.Headers, "x-powered-by", out var poweredBy) && poweredBy.Contains("asp.net", StringComparison.OrdinalIgnoreCase))
        {
            evidence.Add(new DetectionEvidence(
                DetectionSignal.HeaderPoweredByAspNet,
                "Header X-Powered-By contains ASP.NET",
                poweredBy,
                GetWeight(DetectionSignal.HeaderPoweredByAspNet)));
        }

        if (TryGetHeader(artifacts.Headers, "server", out var server) && server.Contains("Microsoft-IIS", StringComparison.OrdinalIgnoreCase))
        {
            evidence.Add(new DetectionEvidence(
                DetectionSignal.HeaderIisServer,
                "Server header indicates Microsoft IIS",
                server,
                GetWeight(DetectionSignal.HeaderIisServer)));
        }
    }

    private void EvaluateCookies(ScanArtifacts artifacts, ICollection<DetectionEvidence> evidence)
    {
        foreach (var cookie in artifacts.Cookies)
        {
            if (cookie.Contains(".AspNetCore", StringComparison.OrdinalIgnoreCase))
            {
                evidence.Add(new DetectionEvidence(
                    DetectionSignal.CookieAspNetCore,
                    "Cookie indicates ASP.NET Core",
                    cookie,
                    GetWeight(DetectionSignal.CookieAspNetCore)));
            }
            else if (cookie.Contains(".AspNet", StringComparison.OrdinalIgnoreCase) || cookie.Contains(".ASPXAUTH", StringComparison.OrdinalIgnoreCase))
            {
                evidence.Add(new DetectionEvidence(
                    DetectionSignal.CookieAspNet,
                    "Cookie indicates ASP.NET",
                    cookie,
                    GetWeight(DetectionSignal.CookieAspNet)));
            }
        }
    }

    private void EvaluateHtml(ScanArtifacts artifacts, ICollection<DetectionEvidence> evidence)
    {
        if (string.IsNullOrEmpty(artifacts.Html))
        {
            return;
        }

        var html = artifacts.Html;

        if (html.Contains("__VIEWSTATE", StringComparison.OrdinalIgnoreCase))
        {
            evidence.Add(new DetectionEvidence(
                DetectionSignal.HtmlViewState,
                "HTML contains __VIEWSTATE",
                "__VIEWSTATE",
                GetWeight(DetectionSignal.HtmlViewState)));
        }

        if (html.Contains(".cshtml", StringComparison.OrdinalIgnoreCase) || html.Contains(".vbhtml", StringComparison.OrdinalIgnoreCase))
        {
            evidence.Add(new DetectionEvidence(
                DetectionSignal.HtmlCshtmlReference,
                "HTML contains Razor view reference",
                ".cshtml",
                GetWeight(DetectionSignal.HtmlCshtmlReference)));
        }

        if (RazorDirectiveRegex.IsMatch(html))
        {
            evidence.Add(new DetectionEvidence(
                DetectionSignal.HtmlRazorDirective,
                "HTML contains Razor directive",
                "Razor directive",
                GetWeight(DetectionSignal.HtmlRazorDirective)));
        }
    }

    private void EvaluateFinalUrl(ScanArtifacts artifacts, ICollection<DetectionEvidence> evidence)
    {
        var path = artifacts.FinalUri?.AbsolutePath ?? string.Empty;
        if (path.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase))
        {
            evidence.Add(new DetectionEvidence(
                DetectionSignal.FinalUrlAspx,
                "Final URL ends with .aspx",
                artifacts.FinalUri?.AbsoluteUri ?? string.Empty,
                GetWeight(DetectionSignal.FinalUrlAspx)));
        }
    }

    private static bool TryGetHeader(IReadOnlyDictionary<string, string> headers, string key, out string value)
    {
        if (headers.TryGetValue(key, out value!))
        {
            return true;
        }

        value = string.Empty;
        return false;
    }

    private int GetWeight(DetectionSignal signal, int? overrideWeight = null)
    {
        if (overrideWeight.HasValue)
        {
            return overrideWeight.Value;
        }

        return _options.SignalWeights.TryGetValue(signal, out var weight) ? weight : 0;
    }

    private ConfidenceBand CalculateConfidence(int score)
    {
        if (score >= _options.MinimumScoreForTrue + 2)
        {
            return ConfidenceBand.Certain;
        }

        if (score >= _options.MinimumScoreForTrue)
        {
            return ConfidenceBand.High;
        }

        if (score >= _options.MinimumScoreForBorderline)
        {
            return ConfidenceBand.Medium;
        }

        return ConfidenceBand.Low;
    }
}
