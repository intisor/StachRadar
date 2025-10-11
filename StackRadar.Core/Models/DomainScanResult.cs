using StackRadar.Core.Detection;

namespace StackRadar.Core.Models;

public sealed record DomainScanResult(
    string Domain,
    bool? IsAspNet,
    int Score,
    ConfidenceBand Confidence,
    string? Server,
    ScanArtifacts Artifacts,
    IReadOnlyCollection<string> Notes,
    IReadOnlyCollection<DetectionEvidence> Evidence
);
