using System.Net;

namespace StackRadar.Core.Models;

public sealed record ScanArtifacts(
    string Domain,
    HttpStatusCode? StatusCode,
    Uri? FinalUri,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyList<string> Cookies,
    string? Html,
    TimeSpan Elapsed
);
