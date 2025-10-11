namespace StackRadar.Core.Detection;

public enum ConfidenceBand
{
    Low,
    Medium,
    High,
    Certain
}

public sealed record DetectionEvidence(
    DetectionSignal Signal,
    string Description,
    string Value,
    int Weight
);

public sealed record DetectionOutcome(
    int Score,
    ConfidenceBand Confidence,
    bool? IsAspNet,
    IReadOnlyCollection<DetectionEvidence> Evidence
);
