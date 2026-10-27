# Changelog

Published PocketVault releases. Windows and Android have independent application version numbers; both use container format `.pvlt` v1.

## [Android 1.0.0](https://github.com/Lantsov/pocket-vault/releases/tag/android-v1.0.0) — 2026-10-27

First signed ARM64 APK for Android 10+:

- Local encryption and decryption using the existing compatible PocketVault Core and Android system document picker.
- Shared `.pvlt` v1 files with PocketVault for Windows; one file at a time, up to 32 MiB on Android.
- Dark mobile UI with IBM Plex Mono, Russian and English, instant language switching and responsive layouts.
- Password clearing and masking, protected app screenshots, disabled app backups, no broad storage permission or app network access.
- Signed APK, published SHA-256 and certificate fingerprint, and a separate Android CI build.
- Android 10/API 29 runtime checks, device testing of the earlier functional build and emulator UI checks for the final IBM Plex version. See [release verification](docs/ANDROID_RELEASE_1.0.0.md) for the precise scope.

## [Windows 1.1.1](https://github.com/Lantsov/pocket-vault/releases/tag/v1.1.1) — 2026-10-26

- Added distribution licenses and third-party notices for the packaged .NET runtime and executable host.
- Improved repeatable release packaging and validation.
- No changes to encryption, format v1, file processing or the Windows interface; compatible with v1.1.0.

## [Windows 1.1.0](https://github.com/Lantsov/pocket-vault/releases/tag/v1.1.0) — 2026-10-26

- Replaced the WinForms UI with a minimal dark WPF interface.
- Added background operations, password visibility/clearing, drag and drop, inline status, DPI/layout adjustments and keyboard accessibility.
- Added WPF interaction tests, screenshot tooling and legacy container-compatibility coverage.
- Published Windows x64 self-contained and framework-dependent ZIPs.

## Initial implementation — 2026-10-26

Commit [`661c57c`](https://github.com/Lantsov/pocket-vault/commit/661c57c65ada7af8b13ddd4e17c0aadb75c4d372) introduced password-based AES-256-GCM/PBKDF2 v1, in-memory ZIP, the original Windows UI, optional verified deletion, Explorer association and tests. It has no separate release tag.
