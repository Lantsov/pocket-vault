# Contributing

PocketVault is a small project maintained by one developer. Focused bug fixes, clearer documentation and reproducible reports are useful. For a larger behavior change, start an issue describing the problem and proposed scope.

1. Create a branch and keep the change focused.
2. On Windows x64 with .NET 10 SDK, run `dotnet build PocketVault.slnx -c Release` and `dotnet run --project PocketVault.Tests -c Release`.
3. For publishing or UI changes, follow [BUILD.md](docs/BUILD.md), including the EXE smoke check or real WPF screenshots as appropriate.
4. Explain the user-visible behavior and validation in the pull request.

The test project is a standalone runner; `dotnet test` does not execute its checks. Keep existing tests and synthetic compatibility fixtures.

Changes to cryptography, password handling, container parsing or filesystem writes need a clear technical reason, a threat/compatibility explanation and relevant regression checks. Avoid changing algorithm parameters as a cosmetic update. Format changes must explicitly address reading older containers, versioning and legacy fixture coverage. Do not silently invalidate existing files or weaken authentication, size limits or safe-name validation.

Do not submit passwords, keys, private containers, personal machine paths or confidential unencrypted files. Use neutral synthetic examples and sanitize screenshots. Report suspected vulnerabilities using [SECURITY.md](SECURITY.md), not a public bug report.

Contributions are submitted under the project's [MIT license](LICENSE). Include the origin and compatible license of any third-party code or artwork. No CLA or multi-stage approval process is required.
