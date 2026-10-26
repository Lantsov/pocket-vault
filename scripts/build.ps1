param(
    [string]$DotnetPath = 'dotnet',
    [string]$PackageSource = '',
    [switch]$Publish
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if ($DotnetPath -eq 'dotnet' -and (Test-Path -LiteralPath (Join-Path $projectRoot '.tools\dotnet\dotnet.exe'))) {
    $DotnetPath = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = '1'
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\cli-home'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools\packages'
$sourceArgs = @()
if ($PackageSource) { $sourceArgs = @('--source', $PackageSource) }
Push-Location $projectRoot
try {
    & $DotnetPath build PocketVault.slnx -c Release -m:1 -nr:false -p:UseSharedCompilation=false @sourceArgs
    if ($LASTEXITCODE -ne 0) { throw 'Ошибка сборки.' }
    New-Item -ItemType Directory -Path 'dist' -Force | Out-Null
    $env:PVLT_SCREENSHOT = Join-Path $projectRoot 'dist\interface.png'
    & $DotnetPath 'PocketVault.Tests\bin\Release\net10.0-windows\PocketVault.Tests.dll' | Tee-Object -FilePath 'dist\test-results.txt'
    if ($LASTEXITCODE -ne 0) { throw 'Тесты не пройдены.' }
    if ($Publish) {
        foreach ($profile in @('FrameworkDependent', 'SelfContained')) {
            & $DotnetPath publish PocketVault.App -p:PublishProfile=$profile -m:1 -nr:false -p:UseSharedCompilation=false @sourceArgs
            if ($LASTEXITCODE -ne 0) { throw "Ошибка публикации: $profile" }
        }
    }
}
finally { Pop-Location }
