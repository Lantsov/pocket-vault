# PocketVault for Android

**Android 1.0.0** is [published on GitHub](https://github.com/Lantsov/pocket-vault/releases/tag/android-v1.0.0). It uses the same `.pvlt` v1 container format as [PocketVault for Windows 1.1.1](https://github.com/Lantsov/pocket-vault/releases/tag/v1.1.1).

- **Requirements:** Android 10+ (API 29), ARM64 (`arm64-v8a`).
- **Interface:** Russian and English, automatic system-language detection and instant manual switching.
- **Privacy:** local file handling through Android's system document picker (SAF); no account, tracking or app network requests.
- **Limit:** a single file up to **32 MiB**. Windows can handle files up to 128 MiB, so not all Windows containers can be opened on Android.
- **Identifier:** `com.lantsov.pocketvault`, version `1.0.0 (1)`.

## Download and install

Get **PocketVault-1.0.0-android-arm64-release.apk** from the [Android release page](https://github.com/Lantsov/pocket-vault/releases/tag/android-v1.0.0). This is the owner-signed release APK, not an unsigned test build. Allow installation from your chosen source when Android asks. PocketVault is not on Google Play.

Published APK SHA-256:

```text
3e2806cbe697ae7a29d75a12b1105e9460581f175fe1c332c3ea17a86f3f0ab0
```

Public SHA-256 fingerprint of the signing certificate:

```text
0e0efeb236a16ea25a4c52835d22bcafc37cbb8687a915da4a211d4c28567bb0
```

Check `SHA256SUMS.txt` and `CERTIFICATE-SHA256.txt` in that release. A checksum confirms bytes, not the identity of the publisher by itself. Keep a trusted copy of the signing fingerprint.

An APK signed with a different key cannot normally update an existing installation with the same ApplicationId. Back up important external files before removing a test installation signed with a debug key.

## Use

1. Tap **Browse files / Обзор файлов** and select a document.
2. Enter and confirm a password; tap **Encrypt / Зашифровать**.
3. Choose the destination in Android's system save dialog. PocketVault suggests a neutral `vault-*.pvlt` name.
4. To restore, open a `.pvlt` with PocketVault, enter its password and select a destination for the recovered file.

You can also open supported containers from a file manager using `ACTION_VIEW` when the manager provides `application/vnd.pocketvault`. For generic MIME types, use **Browse files** inside the app.

**Important:** there is no password recovery. The original document is not deleted. A system document provider may be a cloud provider even though PocketVault itself does not make network requests. If saving is interrupted, the provider may leave a partial file; check the destination before retrying.

## Supported format and security

PocketVault uses AES-256-GCM and PBKDF2-HMAC-SHA256 with 600,000 iterations. The encrypted payload is a ZIP containing one file and its original name. Salt, nonce and authenticated header semantics are identical on Windows and Android. Passwords are never stored in app preferences.

Files are processed in memory; encryption cannot be cancelled midway. There is no secure erasure, guaranteed wiping of managed password strings or independent cryptographic audit. See the [security model](SECURITY_MODEL.md), [format specification](FORMAT.md) and [disclaimer](DISCLAIMER.md).

## Build from source

Requirements: Windows x64, PowerShell 7, stable .NET 10 SDK (see [global.json](../global.json)), `maui-android` workload, Android SDK including API 36/build-tools 36.0.0, and JDK 21. This is a developer toolchain requirement, not the minimum Android device version.

```powershell
dotnet workload install maui-android --skip-manifest-update
# Set ANDROID_HOME and JAVA_HOME for the installed toolchains.
./scripts/build-android.ps1 -Configuration Debug
./scripts/build-android.ps1 -Configuration Release -Unsigned
```

Run commands from the repository root. The script also runs Windows/Core tests by default. Use `-SkipWindowsTests` only when those checks have already succeeded. To target the x86_64 emulator, add `-RuntimeIdentifier android-x64`.

Build output locations:

| Configuration | Location | Status |
| --- | --- | --- |
| Debug ARM64 | `dist/android/debug/android-arm64/` | Development-signed, for testing |
| Release ARM64, unsigned | `dist/android/unsigned/android-arm64/` | **Cannot be installed** |
| Release ARM64, owner-signed | `dist/android/release/android-arm64/` | Distribution build after verified signing |

The script verifies APK identity, architecture, manifest, `zipalign`, signature and SHA-256. Build outputs, signing keys and passwords must not be committed. The Android GitHub Actions workflow compiles Android and runs host checks **without owner signing keys or Android runtime/device execution**.

### Local developer tests

```powershell
adb devices -l
./scripts/build-android.ps1 -RuntimeIdentifier android-x64 -DeviceSerial emulator-5554
```

Use the actual connected device serial, or omit `-DeviceSerial` for a build without runtime tests. The smoke scripts in `scripts/` test Android crypto and document logic. Physical device tests and SAF interop require a connected, authorized device; build success alone is not a device test.

The current UI and localized screenshots are documented in [Android UI verification](ANDROID_PLEX_POLISH.md). The signed release artifact and the distinction between current UI checks and earlier physical-device tests are documented in [release verification](ANDROID_RELEASE_1.0.0.md).

## Signing future updates

**The permanent owner key already exists. Do not create a replacement key for updates to this ApplicationId.** Store the keystore, alias and passwords outside the repository with protected backups; never copy them into GitHub, build logs or chat.

On a trusted development computer, point the existing build script to that key using environment variables. The public certificate fingerprint must match the value published above.

```powershell
$env:POCKETVAULT_KEYSTORE = '<path-to-your-existing-keystore-outside-the-repo>'
$env:POCKETVAULT_KEY_ALIAS = 'pocketvault'
$env:POCKETVAULT_SIGNING_CERT_SHA256 = '0e0efeb236a16ea25a4c52835d22bcafc37cbb8687a915da4a211d4c28567bb0'
$env:POCKETVAULT_STORE_PASS = [Net.NetworkCredential]::new('', (Read-Host 'Keystore password' -AsSecureString)).Password
$env:POCKETVAULT_KEY_PASS = [Net.NetworkCredential]::new('', (Read-Host 'Key password' -AsSecureString)).Password
try {
    ./scripts/build-android.ps1 -Configuration Release
}
finally {
    Remove-Item Env:POCKETVAULT_STORE_PASS, Env:POCKETVAULT_KEY_PASS -ErrorAction SilentlyContinue
}
```

The script supplies passwords to `apksigner` via environment-variable references rather than direct arguments, verifies the expected certificate and writes an APK and checksum into `dist/android/release/android-arm64/`. Secrets are briefly accessible to child processes of the trusted local session. Avoid persistent environment variables, shell history containing passwords, and diagnostic logging of secrets.

## Platform details

`ACTION_VIEW` accepts `content://` URIs with the PocketVault MIME type; it does not automatically decrypt or retain document URI permissions. If an external provider revokes access, select the document again. Broad external-storage permissions are not requested. The normal Release manifest does not request `INTERNET`, disables app data backup and uses `FLAG_SECURE` for the protected interface.

API 29 was tested on an x86_64 emulator, including legacy containers. An ARM64 physical device was tested with an earlier signed UI build; the current IBM Plex Mono UI was verified on the emulator. The published ARM64 APK was separately checked for integrity, signing, manifest and its font resources. That is not a claim that the final UI was retested on a physical device.
