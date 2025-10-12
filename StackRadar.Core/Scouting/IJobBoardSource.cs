namespace StackRadar.Core.Scouting;

public interface IJobBoardSource
{
    string Name { get; }

    IAsyncEnumerable<JobListing> SearchAsync(JobSearchRequest request, CancellationToken cancellationToken = default);
}
