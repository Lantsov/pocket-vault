param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [ValidateSet('android-arm64', 'android-x64')][string]$RuntimeIdentifier = 'android-arm64',
    [string]$DotnetPath = 'dotnet',
    [string]$AndroidSdkDirectory = '',
    [string]$JavaSdkDirectory = '',
    [string]$PackageSource = '',
    [switch]$Unsigned,
    [switch]$SkipWindowsTests,
    [string]$DeviceSerial = ''
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if ($Unsigned -and $Configuration -ne 'Release') { throw '-Unsigned requires Release.' }
if ($DeviceSerial -and $Configuration -ne 'Debug') { throw 'Automated runtime entry points require Debug.' }
if ($Configuration -eq 'Release' -and !$Unsigned -and $RuntimeIdentifier -ne 'android-arm64') {
    throw 'Distribution Release must target android-arm64.'
}
if ($DotnetPath -eq 'dotnet' -and (Test-Path -LiteralPath (Join-Path $projectRoot '.tools/dotnet/dotnet.exe'))) {
    $DotnetPath = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
}
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = '1'
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools/cli-home'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools/packages'
if (!$AndroidSdkDirectory) { $AndroidSdkDirectory = $env:ANDROID_HOME }
if (!$AndroidSdkDirectory) { $AndroidSdkDirectory = $env:ANDROID_SDK_ROOT }
if (!$AndroidSdkDirectory -and $env:LOCALAPPDATA) { $AndroidSdkDirectory = Join-Path $env:LOCALAPPDATA 'Android/Sdk' }
if (!$JavaSdkDirectory) { $JavaSdkDirectory = $env:JAVA_HOME }
if (!$AndroidSdkDirectory -or !$JavaSdkDirectory) { throw 'Set -AndroidSdkDirectory and -JavaSdkDirectory (JDK 21), or ANDROID_HOME/JAVA_HOME.' }
$AndroidSdkDirectory = (Resolve-Path -LiteralPath $AndroidSdkDirectory).Path
$JavaSdkDirectory = (Resolve-Path -LiteralPath $JavaSdkDirectory).Path
$java = Join-Path $JavaSdkDirectory 'bin/java.exe'
$tools = Join-Path $AndroidSdkDirectory 'build-tools/36.0.0'
$signer = Join-Path $tools 'lib/apksigner.jar'
$aapt = Join-Path $tools 'aapt.exe'
$zipalign = Join-Path $tools 'zipalign.exe'
foreach ($file in @($java, $signer, $aapt, $zipalign, (Join-Path $AndroidSdkDirectory 'platforms/android-36/android.jar'))) {
    if (!(Test-Path -LiteralPath $file)) { throw "Required SDK file missing: $file" }
}
$javaVersion = (& $java -version 2>&1) -join "`n"
if ($LASTEXITCODE -ne 0 -or $javaVersion -notmatch 'version "21\.') { throw 'JDK 21 is required.' }

# Passwords never enter MSBuild properties, logs or binlogs. Sign outside MSBuild.
if ($Configuration -eq 'Release' -and !$Unsigned) {
    foreach ($name in @('POCKETVAULT_KEYSTORE', 'POCKETVAULT_KEY_ALIAS', 'POCKETVAULT_STORE_PASS', 'POCKETVAULT_KEY_PASS', 'POCKETVAULT_SIGNING_CERT_SHA256')) {
        if (![Environment]::GetEnvironmentVariable($name)) { throw "Release signing requires $name. See docs/ANDROID.md; use -Unsigned for validation only." }
    }
    $keyPath = (Resolve-Path -LiteralPath $env:POCKETVAULT_KEYSTORE).Path
    $rootPrefix = [IO.Path]::GetFullPath($projectRoot).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if ($keyPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Keep the distribution keystore outside the repository.' }
    $expectedCert = ($env:POCKETVAULT_SIGNING_CERT_SHA256 -replace ':', '').ToLowerInvariant()
    if ($expectedCert -notmatch '^[0-9a-f]{64}$') { throw 'Expected certificate SHA-256 must contain 64 hex digits.' }
}
# MSBuild imports environment variables as properties. Hide signing passwords
# from build/test children, restoring them only for apksigner and for the caller.
$savedSigningPasswords = @{}
foreach ($name in @('POCKETVAULT_STORE_PASS', 'POCKETVAULT_KEY_PASS')) {
    $savedSigningPasswords[$name] = [Environment]::GetEnvironmentVariable($name)
    [Environment]::SetEnvironmentVariable($name, $null)
}
Push-Location $projectRoot
try {
    $sdkVersion = (& $DotnetPath --version) -join ''
    if ($LASTEXITCODE -ne 0 -or $sdkVersion -notmatch '^10\.0\.\d+$') { throw 'A stable .NET 10 SDK compatible with global.json is required.' }
    $workloads = (& $DotnetPath workload list) -join "`n"
    if ($LASTEXITCODE -ne 0 -or $workloads -notmatch 'maui-android') { throw 'Install the maui-android workload for the selected SDK.' }
    if (!$SkipWindowsTests) {
        & (Join-Path $PSScriptRoot 'build.ps1') -DotnetPath $DotnetPath -PackageSource $PackageSource
    }
    $stage = Join-Path $projectRoot ('.tools/android-build/' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    $buildArgs = @('publish', 'PocketVault.Android/PocketVault.Android.csproj', '-c', $Configuration,
        '-r', $RuntimeIdentifier, '-o', $stage, '-m:1', '-nr:false', '-v:minimal',
        '-p:UseSharedCompilation=false', '-p:AndroidPackageFormats=apk',
        '-p:AndroidKeyStore=false', "-p:AndroidSdkDirectory=$AndroidSdkDirectory", "-p:JavaSdkDirectory=$JavaSdkDirectory")
    if ($PackageSource) { $buildArgs += @('--source', $PackageSource) }
    & $DotnetPath @buildArgs
    if ($LASTEXITCODE -ne 0) { throw 'Android build failed.' }
    $unsignedApk = Join-Path $stage 'com.lantsov.pocketvault.apk'
    $debugApk = Join-Path $stage 'com.lantsov.pocketvault-Signed.apk'
    if ($Configuration -eq 'Release') {
        $aligned = Join-Path $stage 'aligned-unsigned.apk'
        & $zipalign -P 16 -f 4 $unsignedApk $aligned
        if ($LASTEXITCODE -ne 0) { throw 'APK alignment failed.' }
        $unsignedApk = $aligned
    }
    $kind = if ($Configuration -eq 'Debug') { 'debug' } elseif ($Unsigned) { 'unsigned' } else { 'release' }
    $output = Join-Path $projectRoot "dist/android/$kind/$RuntimeIdentifier"
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $artifact = Join-Path $output "PocketVault-1.0.0-$RuntimeIdentifier-$kind.apk"
    $apk = Join-Path $stage ([IO.Path]::GetFileName($artifact))
    $sums = Join-Path $output 'SHA256SUMS.txt'
    if ($kind -eq 'release') {
        try {
            foreach ($name in $savedSigningPasswords.Keys) { [Environment]::SetEnvironmentVariable($name, $savedSigningPasswords[$name]) }
            & $java -jar $signer sign --ks $keyPath --ks-key-alias $env:POCKETVAULT_KEY_ALIAS `
                --ks-pass env:POCKETVAULT_STORE_PASS --key-pass env:POCKETVAULT_KEY_PASS --out $apk $unsignedApk
            if ($LASTEXITCODE -ne 0) { throw 'Owner-key signing failed.' }
        }
        finally { foreach ($name in $savedSigningPasswords.Keys) { [Environment]::SetEnvironmentVariable($name, $null) } }
    }
    else { Copy-Item -LiteralPath $(if ($Unsigned) { $unsignedApk } else { $debugApk }) -Destination $apk -Force }
    & $zipalign -c -P 16 4 $apk
    if ($LASTEXITCODE -ne 0) { throw 'APK alignment verification failed.' }
    $badging = (& $aapt dump badging $apk) -join "`n"
    if ($LASTEXITCODE -ne 0 -or $badging -notmatch "package: name='com.lantsov.pocketvault'.*versionName='1.0.0'" -or
        $badging -notmatch "sdkVersion:'29'") { throw 'APK identity/version/minimum SDK check failed.' }
    $abi = if ($RuntimeIdentifier -eq 'android-arm64') { 'arm64-v8a' } else { 'x86_64' }
    if ($badging -notmatch "native-code: '$abi'\s*(\r?\n|$)") { throw 'APK must contain only the requested ABI.' }
    $manifest = (& $aapt dump xmltree $apk AndroidManifest.xml) -join "`n"
    if ($LASTEXITCODE -ne 0 -or $manifest -notmatch 'android:allowBackup[^\r\n]*0x0') { throw 'APK backup must be disabled.' }
    if ($badging -match "android.permission.(MANAGE_EXTERNAL_STORAGE|READ_EXTERNAL_STORAGE|WRITE_EXTERNAL_STORAGE)") { throw 'Unexpected storage permission.' }
    if ($Configuration -eq 'Release' -and ($badging -match 'android.permission.INTERNET' -or $manifest -match 'android:debuggable[^\r\n]*0xffffffff')) {
        throw 'Release must not be debuggable or request INTERNET.'
    }
    $signature = (& $java -jar $signer verify --verbose --print-certs $apk 2>&1) -join "`n"
    $verified = $LASTEXITCODE -eq 0
    if ($Unsigned) {
        if ($verified -or $signature -notmatch 'DOES NOT VERIFY') { throw 'Expected a genuinely unsigned validation APK.' }
        $signature = 'UNSIGNED VALIDATION BUILD: cannot be installed; not a distribution release.'
        # apksigner must fail for this mode. PowerShell/Actions otherwise propagates
        # its expected exit code even after every subsequent artifact check succeeds.
        $global:LASTEXITCODE = 0
    }
    else {
        if (!$verified) { throw 'APK signature verification failed.' }
        if ($kind -eq 'release' -and ($signature -match 'CN=Android Debug' -or
            $signature -notmatch "certificate SHA-256 digest: $expectedCert")) { throw 'APK signer does not match the expected owner certificate.' }
    }
    # Only a fully verified candidate can enter the distributable directory.
    # In particular, a wrong/debug signing certificate must leave no release APK there.
    $hash = (Get-FileHash -LiteralPath $apk -Algorithm SHA256).Hash.ToLowerInvariant()
    if (Test-Path -LiteralPath $sums) { Remove-Item -LiteralPath $sums }
    Copy-Item -LiteralPath $apk -Destination $artifact -Force
    $apk = $artifact
    if ((Get-FileHash -LiteralPath $apk -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash) { throw 'APK copy checksum mismatch.' }
    $signature | Set-Content -LiteralPath (Join-Path $output 'SIGNATURE.txt') -Encoding utf8
    $manifest | Set-Content -LiteralPath (Join-Path $output 'manifest.txt') -Encoding utf8
    @("SDK: $sdkVersion", "JDK: $javaVersion", "Workloads: $workloads", "Kind: $kind", "RID: $RuntimeIdentifier",
        "Host checks skipped: $SkipWindowsTests", "Device serial: $DeviceSerial") |
        Set-Content -LiteralPath (Join-Path $output 'BUILD.txt') -Encoding utf8
    "$hash  $([IO.Path]::GetFileName($apk))" | Set-Content -LiteralPath $sums -Encoding ascii
    if ((Get-FileHash -LiteralPath $apk -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash) { throw 'APK checksum mismatch.' }
    Write-Host "APK ($kind): $apk ($((Get-Item -LiteralPath $apk).Length) bytes); SHA256: $hash"
    if ($DeviceSerial) {
        $adb = Join-Path $AndroidSdkDirectory 'platform-tools/adb.exe'
        & (Join-Path $PSScriptRoot 'test-android-crypto.ps1') -AdbPath $adb -Serial $DeviceSerial -ApkPath $apk
        & (Join-Path $PSScriptRoot 'test-android-documents.ps1') -AdbPath $adb -Serial $DeviceSerial -ApkPath $apk
    }
    else { Write-Host 'No device runtime tests requested. Compilation is not a device test.' }
    if ($kind -ne 'release') { Write-Host 'Android 1.0.0 distribution release is NOT ready: owner-key signing is still required.' }
}
finally {
    foreach ($name in $savedSigningPasswords.Keys) { [Environment]::SetEnvironmentVariable($name, $savedSigningPasswords[$name]) }
    $savedSigningPasswords.Clear()
    Pop-Location
}
