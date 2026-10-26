# PocketVault 1.1.1 — Maintenance Release

This is a technical maintenance update to the Windows x64 distributions. The ZIPs now include the project's MIT license, third-party attribution and the applicable original Microsoft .NET license and notice documents, including notices for the compact build's executable host.

There are no changes to encryption algorithms, cryptographic parameters, file processing, the interface or application functionality. The `.pvlt` container remains format v1 and is fully compatible with PocketVault 1.1.0. This release does not claim to fix a cryptographic vulnerability.

## Downloads

- **PocketVault-1.1.1-win-x64-self-contained.zip** — recommended for most users. Includes the .NET runtime; no separate .NET installation is needed. Extract the ZIP and run `PocketVault.exe`, keeping the license/notice files alongside it. Native runtime libraries extract on first launch.
- **PocketVault-1.1.1-win-x64-framework-dependent.zip** — smaller download for users with **.NET Desktop Runtime 10 x64** installed. Extract and keep the entire folder together. This ZIP does not include the runtime.
- **SHA256SUMS.txt** — SHA-256 checksums for both ZIPs. Compare with `Get-FileHash -Algorithm SHA256` before running. Checksums detect differing bytes, not publisher identity if the source is compromised.

The EXE remains **unsigned**; Windows SmartScreen may warn about an unknown publisher. There is no installer. PocketVault has **not undergone an independent cryptographic audit**. Keep backups and use a long, unique passphrase; there is no password recovery.

The existing v1.1.0 tag, release and downloads are retained unchanged.
