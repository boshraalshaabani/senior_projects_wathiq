param(
    [string]$Output = "artifacts/performance"
)

$env:WATHIQ_PERFORMANCE_OUTPUT = $Output

dotnet test `
  "tests/eArchiveSystem.PerformanceRunner/eArchiveSystem.PerformanceRunner.csproj" `
  --configuration Release `
  --filter "Layer=Performance"
