# PocketVault Android 1.0.0 — release verification

**Published 2026-10-27:** [Android 1.0.0](https://github.com/Lantsov/pocket-vault/releases/tag/android-v1.0.0), commit [`7a1d450`](https://github.com/Lantsov/pocket-vault/commit/7a1d45046b9bfa90110e69ac5f6fbd9d588b482a). This document distinguishes the **published IBM Plex Mono build** from earlier functional tests; do not apply results for an older APK to the final one.

## Published artifact

| Property | Value |
| --- | --- |
| APK | `PocketVault-1.0.0-android-arm64-release.apk` |
| Size | 18,180,100 bytes |
| Version | 1.0.0 (versionCode 1) |
| ApplicationId | `com.lantsov.pocketvault` |
| Architecture | `arm64-v8a` |
| Android SDK | min 29 (Android 10), target 36 |
| Signature | Owner certificate, APK Signature Scheme v3, RSA 3072 |
| Release format | `.pvlt` v1, compatible with Windows 1.1.1 |

APK SHA-256:

```text
3e2806cbe697ae7a29d75a12b1105e9460581f175fe1c332c3ea17a86f3f0ab0
```

Public signing certificate SHA-256:

```text
0e0efeb236a16ea25a4c52835d22bcafc37cbb8687a915da4a211d4c28567bb0
```

GitHub release assets include `SHA256SUMS.txt`, `CERTIFICATE-SHA256.txt` and `VERIFICATION.json`. Verification covered signing, alignment, package identity, ARM64 architecture, manifest permissions and IBM Plex Mono resources. No keystore, passwords or release-signing secrets are in the repository.

## Runtime testing scope

**Earlier owner-signed functional build, physical Android 16 ARM64 device:** actual SAF selection and saving, Windows ↔ Android file transfer with matching names/bytes, invalid password, damaged container, background/launch handling, screenshot protection and an incompressible 32 MiB roundtrip. These results establish the underlying workflow on a physical device, **not** the final Plex interface on that device.

**Final IBM Plex Mono interface:** real Android 10/API 29 x86_64 emulator screenshots and interface checks, Russian/English switching, system language, font coverage, 100%/200% text, compact/landscape layouts, document selection and a brief encryption flow. See [Android UI verification](ANDROID_PLEX_POLISH.md). The published owner-signed ARM64 APK was verified for signing and package contents, but was not re-run on the physical device in this validation.

**Additional checks:** Windows host tests and independent CI builds completed; legacy `.pvlt` fixtures were recovered by Android runtime tests. Host-only GitHub Actions do not run Android emulator tests.

## Known limitations

- One file, maximum **32 MiB** on Android (Windows supports 128 MiB).
- In-memory crypto, no operation cancellation, password recovery or secure erasure.
- No independent cryptographic audit.
- Android document providers can leave partially saved files if interrupted. Other applications/providers may retain plaintext copies or metadata.
- Physical ARM64 Android 10 and every third-party document provider were not individually tested.

See [installation/build/signing instructions](ANDROID.md), [security model](SECURITY_MODEL.md) and the [release page](https://github.com/Lantsov/pocket-vault/releases/tag/android-v1.0.0) for download details.
