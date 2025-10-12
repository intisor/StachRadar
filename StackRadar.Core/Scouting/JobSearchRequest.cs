namespace StackRadar.Core.Scouting;

public sealed record JobSearchRequest
{
    public string Query { get; init; } = string.Empty;
    public string? Location { get; init; }
    public int? Limit { get; init; }
    public int? MaxPages { get; init; }
}
