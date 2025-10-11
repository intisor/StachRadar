namespace StackRadar.Core.Scouting;

public sealed record DomainSourceRequest
{
    public int? Limit { get; init; }
    public int? MaxPages { get; init; }
    public string? Query { get; init; }
};
