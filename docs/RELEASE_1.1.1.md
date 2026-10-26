# Controlled release of PocketVault 1.1.1

Preparation takes place on `prepare-public-release`. Keep the repository private. Do not move `v1.1.0`, replace its assets, rewrite history, or create `v1.1.1` before the merge and successful main CI. Release creation remains a manual owner action; CI only uploads artifacts.

## Merge and validate main

1. Review and merge `prepare-public-release` into `main` through GitHub. Ensure any required review/checks pass.
2. Wait for the **Windows x64 · .NET 10** job on the resulting main commit to succeed. Check build, all standalone tests, both publishes, EXE smoke checks, documentation, packaging, ZIP/licenses/SHA-256 verification and extracted-EXE smoke checks. Do not use a green run for a different commit.
3. Record the run ID and full `headSha`. Download **PocketVault-release-packages** from that exact main run. GitHub wraps the artifact in an outer ZIP; extract it to obtain the two product ZIPs and `SHA256SUMS.txt`. Preserve the inner ZIPs unchanged.
4. Independently compare both ZIP hashes with `SHA256SUMS.txt`, check ZIP integrity, inspect notice folders and smoke-test both extracted builds. The framework-dependent build needs .NET Desktop Runtime 10 x64. Record the runtime version from that run; a later stable .NET 10 patch is acceptable.

Optional CLI checks (replace every placeholder with the verified value):

```powershell
gh run view MAIN_RUN_ID --repo Lantsov/pocket-vault --json headBranch,headSha,status,conclusion,url
gh run download MAIN_RUN_ID --repo Lantsov/pocket-vault --name PocketVault-release-packages --dir release-1.1.1
Get-FileHash release-1.1.1/PocketVault-1.1.1-win-x64-*.zip -Algorithm SHA256
Get-Content release-1.1.1/SHA256SUMS.txt
```

## Tag the checked commit and upload

After all checks above succeed:

```powershell
git fetch origin
git switch main
git pull --ff-only origin main
git rev-parse HEAD
# Confirm HEAD equals the successful main run's full headSha.
git tag -a v1.1.1 VERIFIED_MAIN_SHA -m "PocketVault 1.1.1"
git push origin refs/tags/v1.1.1
gh release create v1.1.1 --repo Lantsov/pocket-vault --verify-tag --title "PocketVault 1.1.1 — Maintenance Release" --notes-file docs/RELEASE_NOTES_1.1.1.md release-1.1.1/PocketVault-1.1.1-win-x64-self-contained.zip release-1.1.1/PocketVault-1.1.1-win-x64-framework-dependent.zip release-1.1.1/SHA256SUMS.txt
```

Use the notes file from the tagged commit. Do not upload test screenshots, unwrapped raw publish outputs or the outer Actions artifact ZIP as product downloads. Do not use artifacts built on the preparation branch if the merge commit differs, or if any source/configuration changed afterward. Rebuild and verify on main when necessary. The tag-triggered workflow is deliberately not required: use the already successful main run for the exact tagged commit.

Check that the tag resolves to the recorded commit and that all three uploaded assets and downloaded hashes match. Mark v1.1.1 as the latest stable release. Both READMEs use `/releases/latest`, so they resolve to v1.1.1 after publication; their preparation notice can then be removed in a later documentation update. Do not change visibility as part of this release process.

See [the preparation evidence](PUBLICATION_REVIEW.md), [build instructions](BUILD.md) and [English release notes](RELEASE_NOTES_1.1.1.md).
