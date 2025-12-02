using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using CsvHelper;
using CsvHelper.Configuration;

namespace StackRadar.Core.Scouting;

public sealed class BuiltWithCsvSource : IDomainSource
{
    private readonly ILogger<BuiltWithCsvSource> _logger;

    public BuiltWithCsvSource(ILogger<BuiltWithCsvSource> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "builtwithcsv";

    public async IAsyncEnumerable<DomainCandidate> FetchAsync(DomainSourceRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var csvFile = "ASP.NET websites in Nigeria - 2025-10-12.csv"; // Or pass via config
        if (!File.Exists(csvFile))
        {
            _logger.LogWarning("BuiltWith CSV file not found: {File}", csvFile);
            yield break;
        }

        var totalYielded = 0;
        var limit = request.Limit;

        using var reader = new StreamReader(csvFile);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null, // Ignore missing columns gracefully
            BadDataFound = null       // Ignore bad data gracefully
        });

        // Dynamic reading allows flexibility without a rigid class
        var records = csv.GetRecordsAsync<dynamic>(cancellationToken);

        await foreach (var rec in records)
        {
            // Cast to dictionary to access fields safely
            var dict = (IDictionary<string, object?>)rec;

            dict.TryGetValue("Root Domain", out var rd);
            var rootDomain = rd?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(rootDomain))
                continue;

            // Skip non-Nigerian domains
            if (!rootDomain.EndsWith(".ng", StringComparison.OrdinalIgnoreCase) &&
                !rootDomain.Contains(".com.ng") &&
                !rootDomain.Contains(".edu.ng") &&
                !rootDomain.Contains(".gov.ng"))
            {
                continue;
            }

            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["technology"] = "ASP.NET",
                ["url"] = $"https://{rootDomain}",
                ["source"] = "BuiltWith CSV Report",
                ["reportDate"] = "2025-10-12"
            };

            if (dict.TryGetValue("Company", out var company) && company is not null)
                metadata["company"] = company.ToString()!;

            if (dict.TryGetValue("Vertical", out var vertical) && vertical is not null)
                metadata["vertical"] = vertical.ToString()!;

            if (dict.TryGetValue("City", out var city) && city is not null)
                metadata["city"] = city.ToString()!;

            if (dict.TryGetValue("Country", out var country) && country is not null)
                metadata["country"] = country.ToString()!;

            if (dict.TryGetValue("First Detected", out var firstDetected) && firstDetected is not null)
                metadata["firstDetected"] = firstDetected.ToString()!;

            if (dict.TryGetValue("Last Found", out var lastFound) && lastFound is not null)
                metadata["lastFound"] = lastFound.ToString()!;

            if (dict.TryGetValue("Technology Spend", out var techSpend) && techSpend is not null)
                metadata["technologySpend"] = techSpend.ToString()!;

            if (dict.TryGetValue("Sales Revenue", out var salesRevenue) && salesRevenue is not null)
                metadata["salesRevenue"] = salesRevenue.ToString()!;

            if (dict.TryGetValue("Employees", out var employees) && employees is not null)
                metadata["employees"] = employees.ToString()!;

            yield return DomainCandidate.Create(rootDomain, Name, 0.9, metadata);

            totalYielded++;
            if (limit.HasValue && totalYielded >= limit.Value)
                yield break;
        }

        _logger.LogInformation("Processed {Count} domains from BuiltWith CSV", totalYielded);
    }

    // CSV parsing delegated to CsvHelper (GetRecords<dynamic>()), no manual parser required
}