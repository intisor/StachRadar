using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

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
        var csvFile = "ASP.NET websites in Nigeria - 2025-10-12.csv";
        if (!File.Exists(csvFile))
        {
            _logger.LogWarning("BuiltWith CSV file not found: {File}", csvFile);
            yield break;
        }

        var lines = await File.ReadAllLinesAsync(csvFile, cancellationToken);
        if (lines.Length < 2)
        {
            _logger.LogWarning("CSV file appears to be empty or invalid");
            yield break;
        }

        // Parse header to find column indices
        var header = lines[0].Split(',');
        var rootDomainIndex = Array.IndexOf(header, "Root Domain");
        var companyIndex = Array.IndexOf(header, "Company");
        var verticalIndex = Array.IndexOf(header, "Vertical");
        var cityIndex = Array.IndexOf(header, "City");
        var countryIndex = Array.IndexOf(header, "Country");
        var firstDetectedIndex = Array.IndexOf(header, "First Detected");
        var lastFoundIndex = Array.IndexOf(header, "Last Found");
        var technologySpendIndex = Array.IndexOf(header, "Technology Spend");
        var salesRevenueIndex = Array.IndexOf(header, "Sales Revenue");
        var employeesIndex = Array.IndexOf(header, "Employees");

        if (rootDomainIndex == -1)
        {
            _logger.LogError("Root Domain column not found in CSV");
            yield break;
        }

        var totalYielded = 0;
        var limit = request.Limit;

        // Skip header and process each line
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var columns = ParseCsvLine(line);
            if (columns.Length <= rootDomainIndex)
                continue;

            var rootDomain = columns[rootDomainIndex]?.Trim();
            if (string.IsNullOrWhiteSpace(rootDomain))
                continue;

            // Skip non-Nigerian domains (though this file should only contain .ng domains)
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

            // Add additional metadata if available
            if (companyIndex >= 0 && companyIndex < columns.Length && !string.IsNullOrWhiteSpace(columns[companyIndex]))
                metadata["company"] = columns[companyIndex].Trim();

            if (verticalIndex >= 0 && verticalIndex < columns.Length && !string.IsNullOrWhiteSpace(columns[verticalIndex]))
                metadata["vertical"] = columns[verticalIndex].Trim();

            if (cityIndex >= 0 && cityIndex < columns.Length && !string.IsNullOrWhiteSpace(columns[cityIndex]))
                metadata["city"] = columns[cityIndex].Trim();

            if (countryIndex >= 0 && countryIndex < columns.Length && !string.IsNullOrWhiteSpace(columns[countryIndex]))
                metadata["country"] = columns[countryIndex].Trim();

            if (firstDetectedIndex >= 0 && firstDetectedIndex < columns.Length && !string.IsNullOrWhiteSpace(columns[firstDetectedIndex]))
                metadata["firstDetected"] = columns[firstDetectedIndex].Trim();

            if (lastFoundIndex >= 0 && lastFoundIndex < columns.Length && !string.IsNullOrWhiteSpace(columns[lastFoundIndex]))
                metadata["lastFound"] = columns[lastFoundIndex].Trim();

            if (technologySpendIndex >= 0 && technologySpendIndex < columns.Length && !string.IsNullOrWhiteSpace(columns[technologySpendIndex]))
                metadata["technologySpend"] = columns[technologySpendIndex].Trim();

            if (salesRevenueIndex >= 0 && salesRevenueIndex < columns.Length && !string.IsNullOrWhiteSpace(columns[salesRevenueIndex]))
                metadata["salesRevenue"] = columns[salesRevenueIndex].Trim();

            if (employeesIndex >= 0 && employeesIndex < columns.Length && !string.IsNullOrWhiteSpace(columns[employeesIndex]))
                metadata["employees"] = columns[employeesIndex].Trim();

            yield return DomainCandidate.Create(rootDomain, Name, 0.9, metadata);

            totalYielded++;
            if (limit.HasValue && totalYielded >= limit.Value)
                yield break;
        }

        _logger.LogInformation("Processed {Count} domains from BuiltWith CSV", totalYielded);
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        var quoteChar = '"';

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (!inQuotes && c == ',')
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else if (c == quoteChar)
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == quoteChar)
                {
                    // Escaped quote
                    current.Append(quoteChar);
                    i++; // Skip next quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result.ToArray();
    }
}