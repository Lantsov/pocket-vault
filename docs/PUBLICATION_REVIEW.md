# Public-release preparation review

Review date: **2026-10-26**. Repository: [Lantsov/pocket-vault](https://github.com/Lantsov/pocket-vault). Work branch: `prepare-public-release`.

## Decision

**Keep the repository private until the owner completes the remaining decisions below.** The source/documentation preparation is implemented; this review is not an independent cryptographic audit, a legal opinion or a guarantee that no secret exists. Cryptographic code, public APIs and format v1 are unchanged.

| Item | Evidence and action |
| --- | --- |
| Author privacy | Both original commits contain the author's name and email in Git metadata. Confirm that publishing these identifiers is intentional before changing visibility. A later commit cannot conceal them. History has not been rewritten. |
| Open-source license | Standard MIT text is prepared for 2026 Lantsov. The owner must confirm the rights to publish the included work and accept the license choice. MIT allows commercial use, modification and redistribution by others, subject to its notice requirements. |
| Existing runtime redistribution | The published self-contained 1.1.0 ZIP contains only `PocketVault.exe`; the compact ZIP contains five application files. Neither includes license notices. CI logs identify SDK 10.0.403/runtime 10.0.14, while the local verification uses 10.0.402/10.0.13. Resolve distribution of the **matching 10.0.14** runtime license and notices with the historical release before public promotion; do not silently substitute local runtime notices or replace its ZIPs. Future packaging now includes notices automatically. |
| Changes on the default branch | Review and merge the preparation branch before expecting the default GitHub page to display these files. No new tag or release is required for documentation. |
| Private reporting | The private-vulnerability-reporting API returned 404 while private. Enable the feature at publication and verify the report form. Until available, SECURITY.md only invites a detail-free request for a private channel. |
| Final page inspection | GitHub Markdown API rendered both READMEs successfully. A real wide/narrow browser inspection could not be completed: localhost timed out and the browser policy rejected local file URLs. Inspect the branch/default README on GitHub before announcing the project. |

## History and disclosure audit

The repository is not shallow. At the start of the review, GitHub listed only `main` and tag `v1.1.0`, matching local refs. The preparation branch was subsequently created and pushed. The available original history consists of:

- `661c57c65ada7af8b13ddd4e17c0aadb75c4d372` — initial WinForms/Core project.
- `2d8915f91906a30ac8c8025c8a11c3ebb9b6dff7` — WPF migration, tagged 1.1.0.

All 42 unique historical file blobs were inspected/scanned, including removed source files, with patterns for common provider tokens, private keys, credential assignments, authenticated URLs, private addresses, email and personal Windows paths. No actual credentials, private endpoints or personal machine paths were found in the source history. Credential-assignment matches are synthetic test passwords in `PocketVault.Tests/Program.cs` (initial commit line 12; WPF commit lines 19, 241, 318 and 378). The public legacy fixture documents its deliberately public password and synthetic contents. It is not a user vault.

The two historical ICOs are the only tracked binary assets in the original history. Their pixels match `scripts/create-icons.py` at all seven embedded sizes. No historical user `.pvlt`, archive, EXE, log, private-key file or screenshot was tracked. No third-party source attribution or third-party NuGet package reference was found. This establishes consistency with the available history, not authorship outside it.

Commit author/committer metadata in **both hashes above** contains personal identifiers. The relevant location is the commit object, not a working-tree file. Confirm consent or plan a separate, explicit history-cleaning operation before publication if those identifiers must remain private. No secret values or full email are repeated here.

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

Before publication, the owner should:

1. Accept the author-metadata exposure and MIT choice, and resolve the historical runtime notices described above.
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
