using StackRadar.Core.Models;

namespace StackRadar.Core.Services;

public interface IDomainScanner
{
    Task<DomainScanResult> ScanAsync(string domain, CancellationToken cancellationToken = default);
}
