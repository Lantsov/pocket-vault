param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [string]$DotnetPath = 'dotnet',
    [string]$AndroidSdkDirectory = '',
    [string]$JavaSdkDirectory = '',
    [string]$PackageSource = ''
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
$buildArgs = @('build', 'PocketVault.Android.slnx', '-c', $Configuration, '-m:1', '-nr:false',
    '-p:UseSharedCompilation=false', '-p:AndroidPackageFormats=apk')
if ($AndroidSdkDirectory) { $buildArgs += "-p:AndroidSdkDirectory=$AndroidSdkDirectory" }
if ($JavaSdkDirectory) { $buildArgs += "-p:JavaSdkDirectory=$JavaSdkDirectory" }
if ($PackageSource) { $buildArgs += @('--source', $PackageSource) }
Push-Location $projectRoot
try {
    & $DotnetPath @buildArgs
    if ($LASTEXITCODE -ne 0) { throw 'Сборка Android не прошла.' }
}
finally { Pop-Location }
