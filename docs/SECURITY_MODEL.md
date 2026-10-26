# Security model

This describes the current implementation, not a security certification. Read the [disclaimer](DISCLAIMER.md) and [reporting policy](../SECURITY.md).

## Intended protection

PocketVault encrypts one file's primary contents and base name into a `.pvlt` container. An observer with only the container needs the password to decrypt it, subject to the strength of the password, cryptographic assumptions and implementation correctness. It prevents ordinary content viewing and indexing of the encrypted payload. AES-GCM authenticates the header and payload before ZIP processing or output.

The [format specification](FORMAT.md) documents AES-256-GCM, PBKDF2-HMAC-SHA256 with 600,000 iterations, a random 16-byte salt and random 12-byte nonce. These are implemented through .NET cryptographic APIs. Different salts prevent simple reuse of precomputed password guesses across containers; the KDF makes guesses more expensive. Neither prevents offline guessing of a weak password. There is no account, server, password escrow or recovery key.

## Outside the threat model

- Malware, keyloggers, hostile administrator access or a compromised operating system.
- Memory dumps, crash dumps, swap/page files, hibernation images or physical attacks on an unlocked machine.
- Copies of the source or restored file in backups, synchronization services, snapshots or other applications.
- Secure deletion, anonymity, hiding the existence of encrypted data or authenticating its sender.
- High-risk or regulated uses requiring independent review or certification.

No independent cryptographic audit has been performed. Tests cover specific expected behavior and failure cases; they do not establish implementation security.

## What stays visible

The container header exposes its format, KDF/cipher identifiers, iteration count, salt, nonce and encrypted ZIP length. Its external filename, timestamps, location and filesystem permissions are visible. Compression changes the relationship between original and encrypted size; the encrypted length still leaks size information. There is no padding.

The original filename, extension, contents and ZIP metadata are encrypted. The default suggested container name contains a date and random suffix, not the source name. Users can still disclose information by choosing a revealing output name or sharing the source alongside the container.

Windows and other applications may retain recent-file lists, search indexes, thumbnails, previews, filesystem journals, restore points, old file versions and backup copies from before encryption or after restoration. PocketVault does not clear them. Selecting a file displays its directory in the UI; screenshots and screen sharing can reveal that path.

## Memory and disk

The source, ZIP and encrypted/decrypted buffers are processed in memory. One file is limited to 128 MiB, the ZIP to 129 MiB. Memory use can reach hundreds of MiB and approach 1 GiB during verification/deletion workflows. There is no streaming mode.

Keys and application-owned plaintext buffers are zeroed when released. Password fields are cleared when selection changes or an operation starts. Managed password strings and copies in WPF, .NET or Windows cannot be reliably erased. Plaintext can reach paging, crash or hibernation files.

Saving uses a random `.pvlt-*.tmp` in the destination folder, a disk flush and a rename without overwriting existing files. During **decryption**, this temporary file contains authenticated **plaintext**. Normal failure attempts cleanup; forced termination, power loss or cleanup failure can leave a file behind. No temporary plaintext ZIP is intentionally written. Filesystem crash behavior and rename guarantees vary, particularly on network or removable volumes.

The self-contained EXE also extracts native .NET libraries under the runtime's extraction directory on first launch. These are application libraries, distinct from user-data temporary files.

## Optional source deletion

Originals are kept by default. If deletion is explicitly requested, PocketVault reads back the saved container, decrypts it, checks its name and contents against the source, then requests ordinary Windows deletion through the held source handle. The handle prevents ordinary concurrent write/replacement while checking. A failed write or verification preserves the source; a deletion failure can leave both files.

This is **not secure erasure**, does not use the Recycle Bin and cannot remove disk remnants, SSD wear-leveling copies, indexes or backups. NTFS permissions, alternate streams and other source metadata are not archived separately. Do not delete the only copy of important data simply because one verification succeeded.

## Android-specific boundaries

Android 1.0.0 shares format v1 and the Core implementation with Windows 1.1.1,
but limits source and restored content to 32 MiB. Processing still happens in
memory; low-memory process termination cannot guarantee cleanup or completion.
An injected `OutOfMemoryException` check is distinct from an OS low-memory kill.

Access uses Storage Access Framework `content://` grants, without broad storage
permissions. The app does not persist URIs in preferences or its own saved state;
revoked access requires selecting the document again. The optional exported
launcher accepts only the custom PocketVault MIME type for implicit ACTION_VIEW;
incoming metadata, extension, size and read access are validated. It does not
automatically decrypt or save. Opaque provider IDs and generic MIME types cannot
reliably be associated by filename, so these files use the in-app picker.

Release does not request INTERNET. No analytics, trackers or synchronization
services are added. Automatic app backup is disabled. FLAG_SECURE protects the
app window and recent-app snapshot; the system picker and keyboard are outside
that window. Password fields clear on backgrounding, selection, navigation and
processing. Managed strings and IME copies cannot be reliably erased.

The app does not intentionally write plaintext, passwords or document URIs to
private cache, preferences or logs. A user-selected document provider may be
cloud-backed, index content or share it with other apps. Authenticated plaintext
is written directly to the destination provider. Write failure attempts to delete
only the newly created document; denied deletion or force-stop/power loss may
leave a partial file. There is no provider-independent atomic save or secure
erasure. Original documents are never deleted by Android.

Development-signed APKs are for testing. Distribution requires the owner's
keystore and verification against its expected certificate SHA-256. Keep the
keystore and passwords outside Git and CI; updates with the same application ID
require retaining the signing identity. [Build and signing guide](ANDROID.md).

## Practical use on either platform

Use a long, unique passphrase stored safely outside the container. Keep tested backups and verify that you can restore disposable data before relying on a build. Use an OS and runtime that receive security updates. Protect the destination folder and avoid untrusted shared computers. Treat restored files as plaintext immediately. Do not put passwords, private containers or confidential examples into issues, logs or screenshots.
