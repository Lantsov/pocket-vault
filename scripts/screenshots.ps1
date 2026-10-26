param([string]$DotnetPath = 'dotnet')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if ($DotnetPath -eq 'dotnet' -and (Test-Path -LiteralPath (Join-Path $projectRoot '.tools\dotnet\dotnet.exe'))) {
    $DotnetPath = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
}
& (Join-Path $PSScriptRoot 'build.ps1') -DotnetPath $DotnetPath
if ($LASTEXITCODE -ne 0) { throw 'Build or tests failed.' }

# A real, neutral path is shown by the unmodified application. No pixels or labels are edited.
$drive = @('P:', 'Q:', 'R:', 'S:', 'T:', 'U:', 'V:', 'W:', 'X:', 'Y:', 'Z:') |
    Where-Object { -not (Test-Path -LiteralPath "$_\") } | Select-Object -First 1
if (-not $drive) { throw 'No free drive letter for the neutral demonstration path.' }
$sampleRoot = Join-Path $projectRoot ('dist\screenshots-demo-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $sampleRoot -Force | Out-Null
& subst.exe $drive $sampleRoot
if ($LASTEXITCODE -ne 0) { throw 'Could not map the demonstration drive.' }
try {
    & $DotnetPath (Join-Path $projectRoot 'PocketVault.Tests\bin\Release\net10.0-windows\PocketVault.Tests.dll') `
        --screenshots (Join-Path $projectRoot 'docs\images') "$drive\PocketVault Demo"
    if ($LASTEXITCODE -ne 0) { throw 'Screenshot capture failed.' }
}
finally {
    & subst.exe $drive /d
    if ($LASTEXITCODE -ne 0) { Write-Warning "Remove the temporary mapping manually: subst $drive /d" }
}
