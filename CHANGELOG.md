# Changelog

Notable changes based on the repository history and published releases. Dates use the release date where a release exists.

## v1.1.1 — Maintenance release

Prepared for release; not yet tagged or published.

- Added required redistribution license notices for packaged .NET runtime components.
- Improved release packaging and third-party attribution, including the framework-dependent executable host.
- No changes to encryption, file format or application functionality. Containers remain format v1 and compatible with v1.1.0.

## Public-release documentation preparation

### Added

- English and Russian project pages, genuine WPF preview images and a social preview asset.
- Security model, format/build/usage guides, bilingual disclaimer, MIT license and third-party notices.
- Contribution guidance, issue forms, pull request checklist and a public-release preparation report.
- Repeatable documentation screenshots and packaging/checksum tooling for future releases.

### Changed

- Documentation organization and publication hygiene; no cryptographic, API or container-format changes.

## [1.1.0] - 2026-10-26

### Changed

- Replaced the WinForms interface with a dark WPF/XAML window while retaining v1 container compatibility.
- Added background operations, password visibility/clearing, drag-and-drop handling and inline status/error feedback.
- Added DPI/work-area layout handling and keyboard accessibility.

### Added

- WPF interaction/layout tests, multiple-DPI render captures and a legacy v1 container fixture.
- Windows manifest and GUI-aware publish smoke checks.

The release contains self-contained and framework-dependent Windows x64 ZIPs and `SHA256SUMS.txt`.

## Initial source - 2026-10-26

Commit [`661c57c`](https://github.com/Lantsov/pocket-vault/commit/661c57c65ada7af8b13ddd4e17c0aadb75c4d372) introduced the WinForms application, AES-256-GCM/PBKDF2 v1 format, in-memory ZIP, verified optional deletion, Explorer association, tests, icons and Windows build workflow. This snapshot has no separate release or version tag in the available history.

[1.1.0]: https://github.com/Lantsov/pocket-vault/releases/tag/v1.1.0
