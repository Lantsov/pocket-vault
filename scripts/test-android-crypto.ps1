param(
    [string]$AdbPath = 'adb',
    [string]$Serial = '',
    [string]$ApkPath = '',
    [ValidateRange(10, 600)][int]$TimeoutSeconds = 180
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (!$ApkPath) {
    $ApkPath = Join-Path $projectRoot 'PocketVault.Android\bin\Debug\net10.0-android\com.lantsov.pocketvault-Signed.apk'
}
$ApkPath = (Resolve-Path -LiteralPath $ApkPath).Path
if ($AdbPath -eq 'adb') {
    foreach ($sdk in @($env:ANDROID_HOME, $env:ANDROID_SDK_ROOT, (Join-Path $env:LOCALAPPDATA 'Android\Sdk'))) {
        if ($sdk -and (Test-Path -LiteralPath (Join-Path $sdk 'platform-tools\adb.exe'))) {
            $AdbPath = Join-Path $sdk 'platform-tools\adb.exe'
            break
        }
    }
}
if (!$Serial) {
    $devices = @(& $AdbPath devices)
    if ($LASTEXITCODE -ne 0) { throw 'ADB недоступен.' }
    $connected = @($devices | Where-Object { $_ -match '^\S+\s+device\s*$' } | ForEach-Object { ($_ -split '\s+')[0] })
    if ($connected.Count -ne 1) { throw 'Нужно одно доступное устройство; при нескольких задайте -Serial.' }
    $Serial = $connected[0]
}
function Invoke-AdbChecked([string[]]$Arguments) {
    & $AdbPath -s $Serial @Arguments
    if ($LASTEXITCODE -ne 0) { throw "ADB завершился с ошибкой: $($Arguments -join ' ')" }
}
$package = 'com.lantsov.pocketvault'
$runId = [Guid]::NewGuid().ToString('N')
Invoke-AdbChecked -Arguments @('install', '-r', $ApkPath)
Invoke-AdbChecked -Arguments @('shell', 'run-as', $package, 'pwd') # Requires a debuggable APK.
Invoke-AdbChecked -Arguments @('shell', 'am', 'force-stop', $package)
Invoke-AdbChecked -Arguments @('shell', 'am', 'start', '-W', '-n', "$package/.MainActivity", '--es', 'crypto_smoke_run_id', $runId)
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
while ([DateTime]::UtcNow -lt $deadline) {
    $raw = & $AdbPath -s $Serial shell run-as $package cat files/crypto-smoke-result.json 2>$null
    if ($LASTEXITCODE -eq 0 -and $raw) {
        try { $report = ($raw -join "`n") | ConvertFrom-Json }
        catch { $report = $null } # Retry while the app is still writing.
        if ($report -and $report.RunId -eq $runId) {
            $output = Join-Path $projectRoot 'dist\android'
            New-Item -ItemType Directory -Path $output -Force | Out-Null
            $reportPath = Join-Path $output "crypto-smoke-$runId.json"
            $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding utf8
            $report.Checks | Format-Table Name, Success, Detail -Wrap
            Write-Host "Runtime: $($report.Runtime); API: $($report.AndroidApi)"
            Write-Host "Отчёт: $reportPath"
            if (!$report.Success -or $report.Runtime -notlike 'Android *' -or $report.AndroidApi -lt 29 -or
                @($report.Checks).Count -ne 11 -or @($report.Checks | Where-Object { !$_.Success }).Count -gt 0) {
                throw 'Android crypto smoke-test не пройден.'
            }
            return
        }
    }
    Start-Sleep -Seconds 1
}
& $AdbPath -s $Serial logcat -d -s PocketVaultCrypto:V AndroidRuntime:E
throw "Android smoke-test не вернул свежий отчёт за $TimeoutSeconds секунд (runId=$runId)."
