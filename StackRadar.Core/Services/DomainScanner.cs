using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using StackRadar.Core.Detection;
using StackRadar.Core.Models;

namespace StackRadar.Core.Services;

public sealed class DomainScanner : IDomainScanner
{
    private static readonly Regex CookieSplitter = new(",(?=(?:[^\\\"]*\\\"[^\\\"]*\\\")*[^\\\"]*$)", RegexOptions.Compiled);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DetectionEngine _detectionEngine;
    private readonly DetectionOptions _detectionOptions;
    private readonly ScannerOptions _scannerOptions;
    private readonly ILogger<DomainScanner> _logger;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public DomainScanner(
        IHttpClientFactory httpClientFactory,
        DetectionEngine detectionEngine,
        DetectionOptions detectionOptions,
        ScannerOptions scannerOptions,
        ILogger<DomainScanner> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _detectionEngine = detectionEngine ?? throw new ArgumentNullException(nameof(detectionEngine));
        _detectionOptions = detectionOptions ?? throw new ArgumentNullException(nameof(detectionOptions));
        _scannerOptions = scannerOptions ?? throw new ArgumentNullException(nameof(scannerOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(msg => (int?)msg.StatusCode is >= 500 or null)
            .WaitAndRetryAsync(
                _scannerOptions.RetryCount,
                retryAttempt => TimeSpan.FromMilliseconds(_scannerOptions.RetryBackoff.TotalMilliseconds * Math.Pow(2, retryAttempt - 1)),
                (outcome, delay, attempt, _) =>
                {
                    if (outcome.Exception is not null)
                    {
                        _logger.LogWarning(outcome.Exception, "Retry {Attempt} after exception when scanning", attempt);
                    }
                    else
                    {
                        _logger.LogWarning("Retry {Attempt} after HTTP status {StatusCode}", attempt, outcome.Result.StatusCode);
                    }
                });
    }

    public async Task<DomainScanResult> ScanAsync(string domain, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new ArgumentException("Domain must be provided", nameof(domain));
        }

        domain = domain.Trim();
        if (!domain.Contains('.'))
        {
            throw new ArgumentException("Domain must contain a TLD", nameof(domain));
        }

        var stopwatch = Stopwatch.StartNew();
        var notes = new List<string>();
        HttpResponseMessage? response = null;
        HttpRequestException? lastException = null;

        try
        {
            response = await TryFetchAsync(domain, useHttps: true, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            var normalized = ex as HttpRequestException ?? new HttpRequestException("Request timed out", ex);
            lastException = normalized;
            notes.Add($"HTTPS request failed: {normalized.Message}");
            if (_scannerOptions.AllowHttpFallback)
            {
                response = await TryFetchAsync(domain, useHttps: false, cancellationToken);
            }
        }

        if (response is null)
        {
            var exception = (Exception?)lastException ?? new InvalidOperationException("Failed to obtain any HTTP response");
            throw exception;
        }

    using var responseContent = response.Content;
    var html = await ReadContentAsync(responseContent, _detectionOptions.MaxHtmlBytes, cancellationToken);
        var headers = FlattenHeaders(response.Headers, response.Content.Headers);
        var cookies = ExtractCookies(response.Headers);
        var serverHeader = headers.TryGetValue("server", out var server) ? server : null;

        stopwatch.Stop();

        try
        {
            var artifacts = new ScanArtifacts(
                domain,
                response.StatusCode,
                response.RequestMessage?.RequestUri,
                headers,
                cookies,
                html,
                stopwatch.Elapsed);

            var detectionOutcome = _detectionEngine.Evaluate(artifacts);

            return new DomainScanResult(
                domain,
                detectionOutcome.IsAspNet,
                detectionOutcome.Score,
                detectionOutcome.Confidence,
                serverHeader,
                artifacts,
                notes,
                detectionOutcome.Evidence);
        }
        finally
        {
            response.Dispose();
        }
    }

    private async Task<HttpResponseMessage> TryFetchAsync(string domain, bool useHttps, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("scanner");
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_scannerOptions.RequestTimeout);

        var response = await _retryPolicy.ExecuteAsync(async ct =>
        {
            var scheme = useHttps ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
            var uriBuilder = new UriBuilder(scheme, domain);
            using var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
            request.Headers.UserAgent.ParseAdd("StackRadar/0.1 (+https://github.com/)");
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

            return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }, timeoutCts.Token).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        return response;
    }

    private static async Task<string?> ReadContentAsync(HttpContent content, int maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        var totalRead = 0;
        int read;
        while (true)
        {
            var remaining = maxBytes - totalRead;
            if (remaining <= 0)
            {
                break;
            }

            var toRead = Math.Min(buffer.Length, remaining);
            read = await stream.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken);
            if (read <= 0)
            {
                break;
            }

            await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            totalRead += read;
        }

        if (memory.Length == 0)
        {
            return null;
        }

        return Encoding.UTF8.GetString(memory.ToArray());
    }

    private static IReadOnlyDictionary<string, string> FlattenHeaders(HttpResponseHeaders responseHeaders, HttpContentHeaders contentHeaders)
    {
        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in responseHeaders)
        {
            dictionary[header.Key.ToLowerInvariant()] = string.Join("; ", header.Value);
        }

        foreach (var header in contentHeaders)
        {
            dictionary[header.Key.ToLowerInvariant()] = string.Join("; ", header.Value);
        }

        return dictionary;
    }

    private static IReadOnlyList<string> ExtractCookies(HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues("Set-Cookie", out var setCookieValues))
        {
            return Array.Empty<string>();
        }

        var cookies = new List<string>();
        foreach (var header in setCookieValues)
        {
            foreach (var cookie in CookieSplitter.Split(header))
            {
                if (!string.IsNullOrWhiteSpace(cookie))
                {
                    cookies.Add(cookie.Trim());
                }
            }
        }

        return cookies;
    }
}
