# Public-release preparation review

Review date: **2026-10-26**. Repository: [Lantsov/pocket-vault](https://github.com/Lantsov/pocket-vault). Work branch: `prepare-public-release`.

## v1.1.1 release decision

The owner has confirmed publication of existing Git author names/email and the MIT license, including commercial use, modification and redistribution. Keep the repository **private**. Preserve the existing `v1.1.0` tag, release and all three assets unchanged. Prepare `1.1.1` on `prepare-public-release`; merge and successful main CI must precede its tag and manual release. These decisions supersede the earlier audit's pending author/license questions.

This maintenance release corrects distribution attribution. No encryption, cryptographic parameters, format v1, UI or file-processing changes are made. The new fixture is generated from the unchanged Core source at tag v1.1.0; the earlier pre-WPF legacy fixture remains intact. There has been no independent cryptographic audit.

The packaging script includes project MIT terms, original exact-version NETCore and WindowsDesktop runtime-pack licenses/notices, and the matching Windows .NET distribution's .NET Library terms and runtime third-party notices. The compact ZIP also carries notices for its native apphost without bundling a runtime. Sources and Windows binary licensing distinctions are documented in [third-party notices](THIRD_PARTY_NOTICES.md).

### Current validation

- Windows x64; official SDK **10.0.403**, NETCore/WindowsDesktop runtime packs **10.0.14**. SDK SHA-512 matched Microsoft's release metadata. Runtime packs were downloaded from the official NuGet flat-container endpoint; restored exact versions were checked. The online self-contained restore stalled and was cancelled; the successful complete pipeline used those downloaded packages as a local source without disabling package auditing. This is not a claim of a refreshed online vulnerability database.
- Release build: zero errors/warnings. Standalone runner: **43 passed, 0 failed** (all 42 existing tests plus the v1.1.0-source fixture). Both the pre-WPF legacy fixture and the v1.1.0-source fixture recover successfully.
- Core, format, cryptographic parameters, app behavior and association implementation compare byte-for-byte with tag v1.1.0. Only the App project version changes to 1.1.1; EXE file version is **1.1.1.0**. Both EXEs remain unsigned.
- Both publish profiles and actual EXE window startup/normal exit passed. Both extracted ZIP builds passed the same smoke check. The compact build also passed with an isolated host/Core/Desktop **10.0.14** directory containing no SDK or ASP.NET runtime. The self-contained build started with SDK/runtime paths removed from PATH and DOTNET_ROOT; no system-wide .NET 10 installation was required.
- The built assembly's existing association methods passed registration, repeated registration, quoted executable/`%1` command, icon bytes/decoding, unregister and previous-association restoration checks using a process-local isolated HKCU mapping. The real registration was not changed; any prior icon bytes were restored. WPF tests cover `.pvlt` selection and restoration. Interactive Explorer default-app selection was not changed or claimed as tested.
- Both ZIPs include project MIT terms, exact upstream runtime-pack documents and matching Windows .NET Library terms/runtime third-party notices. The self-contained ZIP additionally includes the full Windows SDK license for WPF's D3DCompiler component. Upstream document bytes, ZIP CRC, exact manifests, published binary bytes and both SHA-256 sums passed `check-release.py`. No SDK binaries, source files, tests or PDBs are included. These local working-tree packages are validation outputs; publish only artifacts from the successful exact tagged main commit.
- Documentation checker: 16 Markdown files and local destinations passed; final counts are printed by CI. External HTTP checks passed for public upstream/documentation targets. Private repository URLs return anonymous 404; authenticated GitHub API verified the repository, latest release, workflow and historical commit/compare targets. Dynamic private-repository badges remain unverifiable anonymously until a later public launch. No v1.1.1 download link is presented as already published.
- `git diff --check` passed. No v1.1.1 tag/release was created, and repository visibility stays private. The v1.1.0 tag and release asset IDs/digests were checked before and after preparation.
- CI retains manual release control and now verifies archive integrity, checksums, notices and extracted EXE startup before artifact upload. The current preparation commit's Windows CI result is available in the Actions run referenced in the handoff report; successful main CI is still required after merge.

Historical results in the later audit sections refer to the previous preparation stage and are not new 1.1.1 CI evidence.

### Remaining owner actions

Follow [the exact release sequence](RELEASE_1.1.1.md): merge the branch, wait for successful CI on the resulting main commit, tag that verified commit, then manually create v1.1.1 with [the prepared English notes](RELEASE_NOTES_1.1.1.md) and the ZIPs/checksums from that exact main run. Do not reuse older working-branch outputs after source/configuration changes. Repository visibility remains private throughout this task.

The following sections retain the earlier publication audit as historical evidence. Its local SDK/runtime versions and test counts are specific to that earlier stage.

## History and disclosure audit

The repository is not shallow. At the start of the review, GitHub listed only `main` and tag `v1.1.0`, matching local refs. The preparation branch was subsequently created and pushed. The available original history consists of:

- `661c57c65ada7af8b13ddd4e17c0aadb75c4d372` — initial WinForms/Core project.
- `2d8915f91906a30ac8c8025c8a11c3ebb9b6dff7` — WPF migration, tagged 1.1.0.

All 42 unique historical file blobs were inspected/scanned, including removed source files, with patterns for common provider tokens, private keys, credential assignments, authenticated URLs, private addresses, email and personal Windows paths. No actual credentials, private endpoints or personal machine paths were found in the source history. Credential-assignment matches are synthetic test passwords in `PocketVault.Tests/Program.cs` (initial commit line 12; WPF commit lines 19, 241, 318 and 378). The public legacy fixture documents its deliberately public password and synthetic contents. It is not a user vault.

The two historical ICOs are the only tracked binary assets in the original history. Their pixels match `scripts/create-icons.py` at all seven embedded sizes. No historical user `.pvlt`, archive, EXE, log, private-key file or screenshot was tracked. No third-party source attribution or third-party NuGet package reference was found. This establishes consistency with the available history, not authorship outside it.

Commit author/committer metadata in **both hashes above** contains personal identifiers. The relevant location is the commit object, not a working-tree file. The owner has since confirmed that these identifiers may be published; no history-cleaning operation is requested. No secret values or full email are repeated here.

Both available Actions runs were successful and their full logs were downloaded and scanned: `40316040168` and `40320455869`. No matching secrets/private endpoints/personal user paths were found. The repository Actions-secret list was empty; this does not inspect account/organization-level secrets or unavailable/deleted logs. GitHub runner paths are ordinary ephemeral CI paths. No history/log deletion or rewriting was performed. Raw downloaded logs and audit data remain in ignored `.tools/publication-audit/`.

The implementation commit `318c963064725bfa0d7100bb0ff7822491b0c4c8` and its additional file blobs were scanned again, including the five new PNGs reviewed visually. The new commit uses the owner's GitHub noreply identity. `git fsck --full --no-reflogs --unreachable` reported no unreachable objects. The new CI run's log was also scanned without matching secrets/private endpoints/personal user paths.

The ignore rules cover local tools, build output, logs, vaults, IDE files, `.env*`, private-key/certificate formats, dumps and `.pvlt-*.tmp`. Ignore rules do not protect already tracked history; future additions still need review.

## Published 1.1.0 assets

The existing release points to `2d8915f91906a30ac8c8025c8a11c3ebb9b6dff7`. Both downloaded archives pass ZIP CRC validation. Their computed SHA-256 values match **both** the published `SHA256SUMS.txt` and GitHub asset digests:

| Archive | SHA-256 |
| --- | --- |
| `PocketVault-1.1.0-win-x64-self-contained.zip` | `289055c5b76009521a667fd64d3c432f1a9716ab4f5e12ca8534285ceef300ec` |
| `PocketVault-1.1.0-win-x64-framework-dependent.zip` | `e42c55464607a6cd1732ca2c1f08893c92e817bc4a872e016e4597ccfa799828` |

Both published EXEs identify version `1.1.0+2d8915f91906a30ac8c8025c8a11c3ebb9b6dff7` and are unsigned. Existing release/tag/assets have not been recreated, changed or deleted. Locally generated archives with notices are validation outputs, **not replacements for the existing release**.

## Validation

- Windows x64; local .NET SDK 10.0.402, runtime 10.0.13.
- Release build: passed, zero errors/warnings.
- Existing standalone runner: **42 passed, 0 failed**, including Core, file operations, real WPF interactions, layout and legacy recovery. Existing tests retained.
- Both publish profiles: passed using cached official runtime packs via `-PackageSource`. The direct NuGet attempt failed with NU1900/network access, and a network retry did not complete; package auditing was not disabled. This local-source run is not a successful online vulnerability-data refresh.
- Both published EXEs: real windows started and closed normally; self-contained launch had SDK/runtime paths removed. A repository-local native extraction directory was used.
- Packaging: two ZIPs, project license and upstream runtime notices produced; ZIP integrity and generated SHA256SUMS verified. Exact runtime versions come from restore metadata, not the newest cached directory.
- `git diff --check`: passed. App/Core source unchanged.
- Documentation checker validates local destinations, image presence/alt text and code fences. English/Russian README mechanisms, limits, download links and warnings were compared.
- Documentation: 14 Markdown files and 71 local destinations passed the checker. Both READMEs rendered successfully through GitHub's GFM API.
- External links: 21 unique destinations checked. Ten returned HTTP 200, including Microsoft downloads/docs, GitHub configuration docs and badges; eleven unauthenticated project URLs returned 404 while private. A 200 response from a dynamic badge does not itself confirm that private repository data can be resolved; no access token was embedded. Repository/release/workflow/commit/compare targets were independently verified using authenticated GitHub API. The reporting form is not yet verified usable. Public link/badge checks must be repeated after visibility changes.
- [GitHub Windows CI run 40328883811](https://github.com/Lantsov/pocket-vault/actions/runs/40328883811) **passed** on implementation commit `318c963`: build, 42 tests, both publishes, both EXE launches, documentation checks, archive/checksum generation and four artifact uploads. CI used SDK 10.0.403/runtime 10.0.14. The subsequent report-only update does not change that executable/workflow code; its local documentation checks pass separately.

## Visual assets

- `docs/images/pocketvault-main.png` — real WPF encryption screen, `project-notes.txt`, masked demonstration password.
- `docs/images/pocketvault-decrypt.png` — real WPF decryption selection of the generated demonstration vault.
- `docs/images/pocketvault-processing.png` — actual encryption operation before its completion returns through the dispatcher.
- `docs/images/pocketvault-logo.png` — monochrome derivative of the existing ICO.
- `docs/images/social-preview.png` — 1280 × 640 dark card with that logo and rasterized system typography; no font files redistributed.

The three window images are 1020 × 1020, rendered at 144 DPI after a normal resize to 680 × 680 DIP. All were visually inspected for clipping and personal information, and losslessly compressed. Only a neutral temporary drive path is shown; no personal source files, real passwords or machine-specific data were used. The screenshot script removes its drive mapping. No generated UI mockups were used.

## GitHub metadata and owner actions

Description and the following topics were applied through GitHub API, with visibility confirmed **private**:

> Minimal offline file encryption for Windows. AES-256-GCM, password protection, ZIP compression and a clean WPF interface.

`encryption`, `file-encryption`, `windows`, `wpf`, `dotnet`, `aes-256-gcm`, `privacy`, `desktop-app`, `open-source`.

The tags describe existing functionality or the prepared license; obsolete WinForms metadata was removed. They do not imply independent review or certification.

For a later, separately controlled public launch (outside this release-preparation task), the owner should:

1. Author-metadata exposure and the MIT choice are now approved. Preserve v1.1.0 unchanged and use the newly prepared v1.1.1 distributions with notices for the next release.
2. Review the preparation branch and its CI result, merge it, and inspect both README languages on GitHub at desktop/mobile widths.
3. Upload `docs/images/social-preview.png` under **Settings → General → Social preview**. Description/topics are already updated.
4. Change Repository Visibility only after the preceding decisions. This operation remains entirely with the owner.
5. Immediately enable **Private vulnerability reporting** in the repository security settings and verify the form from a non-admin account; recheck public release links, workflow badge and release badge.

See [GitHub's reporting setup guide](https://docs.github.com/en/code-security/how-tos/report-and-fix-vulnerabilities/configure-vulnerability-reporting/configure-for-a-repository), [the standard MIT license](https://opensource.org/license/mit) and [upstream .NET terms](THIRD_PARTY_NOTICES.md).

## Changed and created files

Changed: `README.md`, `.gitignore`, `.github/workflows/build.yml`, `PocketVault.Tests/Program.cs` (explicit documentation capture mode only).

Created:

- `LICENSE`, `CHANGELOG.md`, `SECURITY.md`, `CONTRIBUTING.md`.
- `.github/ISSUE_TEMPLATE/bug_report.yml`, `.github/ISSUE_TEMPLATE/feature_request.yml`, `.github/pull_request_template.md`.
- `docs/README.ru.md`, `docs/USAGE.md`, `docs/SECURITY_MODEL.md`, `docs/FORMAT.md`, `docs/BUILD.md`, `docs/DISCLAIMER.md`, `docs/THIRD_PARTY_NOTICES.md`, this report.
- The five PNGs listed above.
- `scripts/screenshots.ps1`, `scripts/create-doc-assets.py`, `scripts/check-docs.py`, `scripts/package-release.ps1`.

No app/Core functionality, existing tests, fixtures, release tags or container algorithms were removed or changed.
