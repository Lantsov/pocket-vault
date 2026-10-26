param(
    [Parameter(Mandatory)][string]$AndroidSdkDirectory,
    [Parameter(Mandatory)][string]$JavaSdkDirectory,
    [Parameter(Mandatory)][string]$DebugKeystore
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'tests/AndroidIntentProbe'
$stage = Join-Path $root ('.tools/intent-probe/' + [Guid]::NewGuid().ToString('N'))
$buildTools = Join-Path $AndroidSdkDirectory 'build-tools/36.0.0'
$platform = Join-Path $AndroidSdkDirectory 'platforms/android-36/android.jar'
$java = Join-Path $JavaSdkDirectory 'bin/java.exe'
$javac = Join-Path $JavaSdkDirectory 'bin/javac.exe'
$jar = Join-Path $JavaSdkDirectory 'bin/jar.exe'
function Checked([string]$Tool, [string[]]$Arguments) {
    & $Tool @Arguments
    if ($LASTEXITCODE -ne 0) { throw 'Intent probe build failed.' }
}
foreach ($name in @('classes', 'dex', 'assets')) { New-Item -ItemType Directory -Path (Join-Path $stage $name) -Force | Out-Null }
[IO.File]::WriteAllBytes((Join-Path $stage 'assets/fixture.pvlt'),
    [Convert]::FromBase64String((Get-Content (Join-Path $root 'PocketVault.Tests/Fixtures/v1.1.0-container.base64') -Raw)))
Checked $javac @('-encoding', 'UTF-8', '--release', '17', '-classpath', $platform, '-d', "$stage/classes", "$source/IntentProbe.java")
Checked $jar @('cf', "$stage/classes.jar", '-C', "$stage/classes", '.')
Checked $java @('-cp', "$buildTools/lib/d8.jar", 'com.android.tools.r8.D8', '--min-api', '29', '--lib', $platform, '--output', "$stage/dex", "$stage/classes.jar")
Checked "$buildTools/aapt.exe" @('package', '-f', '-M', "$source/AndroidManifest.xml", '-I', $platform, '-A', "$stage/assets", '-F', "$stage/probe-unaligned.apk")
Push-Location "$stage/dex"
try { Checked "$buildTools/aapt.exe" @('add', "$stage/probe-unaligned.apk", 'classes.dex') }
finally { Pop-Location }
Checked "$buildTools/zipalign.exe" @('-f', '4', "$stage/probe-unaligned.apk", "$stage/probe-aligned.apk")
# Standard Android development keystore ONLY. This script cannot sign a distribution release.
Checked $java @('-jar', "$buildTools/lib/apksigner.jar", 'sign', '--ks', $DebugKeystore,
    '--ks-key-alias', 'androiddebugkey', '--ks-pass', 'pass:android', '--key-pass', 'pass:android',
    '--out', "$stage/PocketVault-intent-probe-debug.apk", "$stage/probe-aligned.apk")
Checked $java @('-jar', "$buildTools/lib/apksigner.jar", 'verify', "$stage/PocketVault-intent-probe-debug.apk")
Write-Host "Test-only probe: $stage/PocketVault-intent-probe-debug.apk"
