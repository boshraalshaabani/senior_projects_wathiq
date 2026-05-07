# Wathiq Performance Baseline

## Purpose

This baseline focuses on the two runtime experiences users feel directly in Wathiq:

1. how quickly OCR callback processing persists extracted text
2. how quickly documents can be found through role-aware search

## Runner

The repository includes a local performance smoke runner:

- Project: `tests/eArchiveSystem.PerformanceRunner`
- Helper script: `tools/run-performance-baseline.ps1`
- Execution style: filtered test run (`Layer=Performance`)

## What It Measures

1. `OCR callback persistence`
   - Endpoint: `POST /api/ocr/callback`
   - Measures how quickly OCR text is stored and indexing is triggered

2. `Callback to searchable`
   - Flow: OCR callback followed by repeated search checks until the document appears
   - Measures how fast a processed document becomes discoverable

3. `Search latency`
   - Endpoint: `POST /api/documents/search`
   - Measures role-aware search handling over a seeded corpus

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
- Use these metrics to compare code changes on the `Testing` branch and to detect slowdowns in OCR callback or search behavior.
