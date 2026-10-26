# Build, test and package

## Requirements

- Windows x64 with an interactive desktop for WPF tests and EXE smoke checks.
- Stable **.NET SDK 10 x64**, including WPF/Desktop targeting support.
- PowerShell 7 for the scripts.
- Internet access to official NuGet sources for missing runtime/targeting packs and vulnerability metadata.

`global.json` requests .NET 10.0.100 with `latestFeature` roll-forward and disallows prereleases. The checked-in ICO assets require no image tooling to build. Python with Pillow is needed only to regenerate the artwork.

## Build and tests

From the repository root:

```powershell
dotnet build PocketVault.slnx -c Release
dotnet run --project PocketVault.Tests -c Release
```

The tests are a **standalone console runner**, not an xUnit/MSTest project. `dotnet test` does not run them. The runner returns a nonzero exit code on failure. It exercises Core, file handling, a fixed legacy v1 container, actual WPF interactions, layout and dialogs. Registry registration and Windows default-app selection are not performed. Tests create synthetic files beside the runner and remove their test directory in `finally`.

Run the complete build/test/publish pipeline with:

```powershell
.\scripts\build.ps1 -Publish
# Optional SDK location:
.\scripts\build.ps1 -DotnetPath 'D:\Tools\dotnet\dotnet.exe' -Publish
# Optional local source containing all required official packages:
.\scripts\build.ps1 -PackageSource 'D:\Packages' -Publish
```

The script uses repository-local SDK/cache directories under ignored `.tools/`, disables SDK telemetry and fails if a build, test or publish command fails. `-PackageSource` changes the package source; keep vulnerability checking enabled and provide the needed metadata/network access. A network failure such as NU1900 is a build failure, not evidence that packages were audited.

## Two publish models

```powershell
dotnet publish PocketVault.App -p:PublishProfile=FrameworkDependent
dotnet publish PocketVault.App -p:PublishProfile=SelfContained
```

| Output | Runtime | Distribution |
| --- | --- | --- |
| `dist/framework-dependent/` | Installed .NET Desktop Runtime 10 x64 | Keep EXE, DLLs, JSON and accompanying notices together. |
| `dist/self-contained/` | Included | Single EXE plus distribution notices. Native libraries extract on first launch. |

Both profiles use Release/win-x64. The self-contained profile does not trim WPF assemblies. The EXE is unsigned; SmartScreen behavior varies. Native extraction normally uses the runtime's directory under `%TEMP%\.net`; `DOTNET_BUNDLE_EXTRACT_BASE_DIR` overrides its base. See [Microsoft's single-file documentation](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/).

## Verify the published EXE

```powershell
.\scripts\smoke-publish.ps1
# Also verify the compact build with the specified runtime:
.\scripts\smoke-publish.ps1 -DotnetRoot 'D:\Tools\dotnet'
# Optional writable extraction location:
.\scripts\smoke-publish.ps1 -BundleExtractBaseDirectory 'D:\Temp\PocketVault'
```

The script starts the self-contained build without SDK/runtime paths in `PATH` or `DOTNET_ROOT`, checks the actual window and normal exit, then optionally checks the framework-dependent build. This checks startup, not every application operation or every supported Windows release.

## Real screenshots

`build.ps1` stores ignored test results in `dist/test-results.txt` and real WPF render captures in `dist/interface*.png`, including multiple DPI scales and a busy state. These test captures can include local file paths and must not be published without inspection.

For sanitized documentation images:

```powershell
.\scripts\screenshots.ps1
# Or pass -DotnetPath as above.
```

The script builds and runs the tests, maps an unused drive letter to a fresh ignored demo directory, and invokes the runner's explicit `--screenshots` mode. It captures the real WPF window with `project-notes.txt`, a masked public demonstration passphrase, the actual operation's busy state and the resulting `.pvlt` selection. The window is resized to 680 × 680 DIP to leave room for the busy status card; images are 1020 × 1020 at 144 DPI. No app labels or screenshot pixels are fabricated. The temporary drive mapping is removed in `finally`; generated synthetic data stays under ignored `dist/`. Environments that forbid `subst` need to allow this temporary mapping to run the capture script.

Inspect every PNG for clipping, private paths and visible passwords before committing it. DPI renders do not replace manual checks across physical monitors. There is no mockup fallback when Windows/WPF is unavailable.

## Future release archives

After a successful build, both publishes and smoke checks:

```powershell
.\scripts\package-release.ps1
```

The script reads the project version, includes the project's MIT license and runtime notices, creates two versioned ZIPs and computes `SHA256SUMS.txt` in `dist/releases/`. It refuses existing outputs and does not create Git tags or upload a release. Review the artifacts before any manual upload. Do not replace historical release assets just for documentation changes.

CI runs the Windows build, standalone tests, both publishes, EXE smoke checks, documentation-link checks and packaging. Permissions remain `contents: read`; it uploads build outputs, test diagnostics and a separate release-package artifact. Existing tags/releases are retained.
