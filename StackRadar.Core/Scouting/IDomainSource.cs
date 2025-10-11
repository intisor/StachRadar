namespace StackRadar.Core.Scouting;

public interface IDomainSource
{
    string Name { get; }

    IAsyncEnumerable<DomainCandidate> FetchAsync(DomainSourceRequest request, CancellationToken cancellationToken = default);
}
