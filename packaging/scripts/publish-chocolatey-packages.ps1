$ErrorActionPreference = 'Stop'

$packageDirectory = Join-Path $env:RUNNER_TEMP 'chaptarrng-chocolatey-output'
$packages = @(Get-ChildItem -LiteralPath $packageDirectory -Filter '*.nupkg' | Sort-Object Name)

if ($env:LEGACY_BRIDGE_ENABLED -ne 'true') {
  $packages = @($packages | Where-Object { $_.Name -like 'chaptarrng.*.nupkg' })
  Write-Host 'BookshelfNG Chocolatey identity publishing is disabled until the migration bridge gate is enabled.'
}

$expectedCount = if ($env:LEGACY_BRIDGE_ENABLED -eq 'true') { 2 } else { 1 }
if ($packages.Count -ne $expectedCount) {
  throw "Expected $expectedCount package artifact(s) for the configured migration bridge state."
}

foreach ($package in $packages) {
  choco push $package.FullName --source https://push.chocolatey.org/ --api-key $env:CHOCOLATEY_API_KEY
  if ($LASTEXITCODE -ne 0) {
    throw "Chocolatey rejected package $($package.Name)."
  }
}
