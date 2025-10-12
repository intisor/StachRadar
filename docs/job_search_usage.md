# Job Search Feature - Usage Guide

## Overview

The job search feature automates job listing searches from free job boards like Indeed, extracts company names, and generates LinkedIn search links for companies and their staff. This is a personal workflow tool designed to help you find ASP.NET jobs and research the companies hiring.

## Basic Usage

### Simple Job Search

Search for jobs with a basic query:

```bash
stackradar job-search --query "ASP.NET Developer"
```

This will:
- Search Indeed for ASP.NET Developer positions
- Retrieve up to 50 jobs (default)
- Export results to `jobs.csv`

### Search with Location

Narrow your search to a specific location:

```bash
stackradar job-search --query "ASP.NET Core" --location "New York, NY"
```

### Include LinkedIn URLs

Generate LinkedIn search URLs for companies and their staff:

```bash
stackradar job-search --query ".NET Developer" --include-linkedin
```

The CSV output will include:
- `CompanyLinkedInUrl`: Direct link to search for the company on LinkedIn
- `PeopleLinkedInUrl`: Direct link to search for people at that company

### Advanced Options

Customize the search with additional flags:

```bash
stackradar job-search \
  --query "ASP.NET MVC" \
  --location "San Francisco, CA" \
  --limit 100 \
  --pages 5 \
  --output "sf-aspnet-jobs.csv" \
  --include-linkedin \
  --verbose
```

## Command-Line Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--query` | `-q` | Job search query (required) | - |
| `--location` | `-l` | Job location filter | None |
| `--source` | `-s` | Job board to search | `indeed` |
| `--limit` | - | Maximum number of jobs | `50` |
| `--pages` | `-p` | Maximum pages to iterate | `3` |
| `--output` | `-o` | Output CSV file path | `jobs.csv` |
| `--include-linkedin` | - | Add LinkedIn search URLs | `false` |
| `--timeout` | - | Request timeout in seconds | `30` |
| `--verbose` | `-v` | Enable verbose logging | `false` |
| `--help` | `-h` | Show help message | - |

## CSV Output Format

The generated CSV contains the following columns:

| Column | Description |
|--------|-------------|
| `Title` | Job title |
| `Company` | Company name |
| `Location` | Job location |
| `Description` | Job description snippet |
| `Url` | Direct link to the job posting |
| `Source` | Job board source (e.g., "indeed") |
| `CompanyLinkedInUrl` | LinkedIn company search URL (if `--include-linkedin` is used) |
| `PeopleLinkedInUrl` | LinkedIn people search URL (if `--include-linkedin` is used) |
| `RetrievedAt` | Timestamp when the job was retrieved |

## Example Workflow

1. **Search for jobs:**
   ```bash
   stackradar job-search --query "ASP.NET Core Developer" --location "Remote" --limit 100 --include-linkedin
   ```

2. **Open the CSV:**
   Open `jobs.csv` in Excel or import into Airtable

3. **Research companies:**
   - Click the `CompanyLinkedInUrl` to find the company's LinkedIn page
   - Click the `PeopleLinkedInUrl` to find employees (potential hiring managers, recruiters, etc.)

4. **Apply and network:**
   - Visit the job URLs to apply
   - Use LinkedIn to connect with people at the companies

## Rate Limiting & Ethics

The tool includes built-in rate limiting (2-second delays between pages) to be respectful to job board servers. This is intended for **personal use only**. 

**Important Notes:**
- The tool respects rate limits but may not comply with all Terms of Service
- Use responsibly and for personal job searching only
- Do not use for commercial scraping or data reselling
- If you encounter blocks, reduce the frequency of searches

## Supported Job Boards

Currently supported:
- **Indeed** (`--source indeed`)

Future support planned:
- Stack Overflow Jobs
- LinkedIn Jobs (via API if available)
- Dice
- Monster

## Troubleshooting

### No Results Found

If you get "No jobs found", try:
- Broadening your search query
- Removing location filters
- Using fewer keywords
- Adding `--verbose` to see what's happening

### Network Errors

If you see connection errors:
- Check your internet connection
- Try again after a few minutes (you may have been rate-limited)
- Use a VPN if Indeed is blocking your IP

### Invalid HTML Structure

Indeed may change their HTML structure over time. If parsing fails:
- Report the issue with `--verbose` output
- The tool may need updates to adapt to new HTML patterns

## Integration with StackRadar Workflow

You can combine job search with domain scanning:

1. **Find companies hiring for .NET:**
   ```bash
   stackradar job-search --query "ASP.NET" --limit 200 --output jobs.csv
   ```

2. **Extract domains from company names** (manual or with Excel formulas)

3. **Scan domains for technology stack:**
   ```bash
   stackradar --input companies.txt --output tech-stack.csv
   ```

This gives you a complete picture: job opportunities + technology stack used by companies.
