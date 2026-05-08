# Wathiq Performance Baseline

## Purpose

This baseline focuses on the runtime paths that users feel directly in Wathiq and that are most useful for spotting regressions on the `Testing` branch.

## Runner

The repository includes a local performance smoke runner:

- Project: `tests/eArchiveSystem.PerformanceRunner`
- Helper script: `tools/run-performance-baseline.ps1`
- Execution style: filtered test run (`Layer=Performance`)

## What It Measures

1. `Login latency`
   - Endpoint: `POST /api/auth/login`
   - Measures credential validation and token generation cost

2. `Dashboard totals latency`
   - Endpoint: `GET /api/dashboard/totals`
   - Measures aggregation work over documents, users, and audit data

3. `Search latency`
   - Endpoint: `POST /api/documents/search`
   - Measures role-aware search handling over a seeded corpus

4. `OCR callback persistence`
   - Endpoint: `POST /api/ocr/callback`
   - Measures how quickly OCR text is stored and indexing is triggered

5. `Callback to searchable`
   - Flow: OCR callback followed by repeated search checks until the document appears
   - Measures how fast a processed document becomes discoverable

## How To Run

From the repository root:

```powershell
./tools/run-performance-baseline.ps1
```

Or directly:

```powershell
$env:WATHIQ_PERFORMANCE_OUTPUT="artifacts/performance"
dotnet test tests/eArchiveSystem.PerformanceRunner/eArchiveSystem.PerformanceRunner.csproj --configuration Release --filter "Layer=Performance"
```

## Output

The runner writes two files under `artifacts/performance`:

- `performance-summary.json`
- `performance-summary.md`

## Interpretation Notes

- The runner uses the in-memory ASP.NET Core test host, so it is ideal for regression tracking.
- The results are not meant to represent production SLA values.
- The markdown report includes a `Bottleneck Focus` section that ranks the measured scenarios by average latency.
- Use that ranking to identify which path is currently slowest before optimizing.
- In the current local baseline, `Dashboard totals latency` is the slowest measured path, which suggests the dashboard aggregation flow is the best first optimization candidate.
