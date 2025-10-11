namespace StackRadar.Core.Services;

public sealed class ScannerOptions
{
    public int RetryCount { get; set; } = 2;
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(20);
    public TimeSpan RetryBackoff { get; set; } = TimeSpan.FromSeconds(2);
    public bool AllowHttpFallback { get; set; } = true;
}
