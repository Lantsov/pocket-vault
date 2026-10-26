# Security policy

## Versions

The latest published release is **1.1.0**. Reports should identify whether they affect that release or the current source. No long-term support schedule or backport commitment has been established. Older source snapshots and unofficial builds have no stated support commitment.

## Reporting a vulnerability

Please do **not** immediately disclose suspected cryptographic weaknesses, plaintext exposure or recovery bypasses in public Issues. Details can put other users' data at risk before a fix is available.

The intended private channel is [GitHub Private Vulnerability Reporting](https://github.com/Lantsov/pocket-vault/security/advisories/new), through **Security → Advisories → Report a vulnerability**, once it is enabled. A `SECURITY.md` file by itself does not enable this feature.

During preparation while the repository is private, this channel could not be confirmed available (the API returned 404). The owner must enable Private vulnerability reporting in the repository's security settings when making the repository public, then verify the reporting form from an account without repository administration permissions. See [GitHub's configuration instructions](https://docs.github.com/en/code-security/how-tos/report-and-fix-vulnerabilities/configure-vulnerability-reporting/configure-for-a-repository).

If the form is unavailable, open an issue **only requesting a private reporting channel**, without vulnerability details, passwords, keys, confidential files or private containers. Wait for the maintainer to establish a private channel before sharing details. No reporting email address or guaranteed response time is specified.

Privately include the version/commit, OS and runtime, expected and observed behavior, impact and minimal reproduction using disposable synthetic files. Coordinate disclosure with the maintainer. Never send real sensitive data as a proof of concept.
