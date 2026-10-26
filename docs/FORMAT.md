# PocketVault container format v1

The implementation is [VaultFormat.cs](../PocketVault.Core/VaultFormat.cs). Format version **1** is distinct from application version **1.1.0**; the WPF migration did not change the format. The [legacy fixture](../PocketVault.Tests/Fixtures/README.md) exercises compatibility with the initial commit.

## Layout

All integer fields are little-endian. Offsets and lengths are bytes.

| Offset | Length | Meaning |
| --- | --- | --- |
| 0 | 8 | Magic: `50 56 4C 54 0D 0A 1A 0A` (`PVLT\r\n\x1a\n`) |
| 8 | 1 | Format version: `1` |
| 9 | 1 | KDF identifier: `1` = PBKDF2-HMAC-SHA256 |
| 10 | 1 | Cipher identifier: `1` = AES-256-GCM |
| 11 | 1 | Reserved: `0` |
| 12 | 4 | Signed Int32 iteration count: `600000` |
| 16 | 16 | Random salt |
| 32 | 12 | Random GCM nonce |
| 44 | 8 | Signed Int64 encrypted ZIP length, `N` |
| 52 | N | Encrypted ZIP bytes |
| 52 + N | 16 | GCM authentication tag |

The exact total length is `52 + N + 16`. Trailing bytes and truncation are rejected. `N` must be between 22 and 135,266,304 bytes (129 MiB), inclusive. Header validation runs before key derivation and allocation of the decrypted ZIP buffer. Unsupported versions, identifiers, reserved values and iteration counts are rejected; the iteration count is not an arbitrary caller-controlled work factor.

## Password and authenticated encryption

The password string is passed to the .NET `Rfc2898DeriveBytes.Pbkdf2` string overload (UTF-8 encoding), without trimming or Unicode normalization. Empty passwords are rejected; whitespace-only passwords are not prohibited by the current implementation.

PBKDF2-HMAC-SHA256 derives **32 bytes** using the header's salt and exactly 600,000 iterations. Encryption generates the salt and nonce separately with `RandomNumberGenerator.Fill`. The entire 52-byte header, including length, salt and nonce, is the additional authenticated data (AAD).

AES-GCM encrypts the complete ZIP byte array and appends a 16-byte tag. Decryption must authenticate successfully before ZIP parsing or writing output. A wrong password and an invalid tag produce the same user-facing error. There is no separate password verifier or key recovery mechanism. Authentication detects modification; it does not prove who created the container.

## ZIP payload

The writer creates one ZIP entry with `CompressionLevel.Optimal` and UTF-8 entry-name encoding through .NET `ZipArchive`. The entry stores the original base name and primary file contents. The complete ZIP, including directory records, is encrypted. ZIP entry metadata, such as its timestamp, is inside the ciphertext; it is not a preservation record of all source filesystem metadata.

The reader requires exactly one entry. It rejects paths, separators, NTFS stream syntax, control characters, Windows device names, trailing dots/spaces and names longer than 255 characters. See `ValidateFileName` for the complete rules. Uncompressed contents are limited to **134,217,728 bytes (128 MiB)** and the decompressed length is checked. Malformed archives are rejected. GCM, rather than a ZIP checksum, supplies the container's cryptographic integrity check.

## Visible information and versioning

The magic, version, algorithms, iteration count, salt, nonce, ciphertext length and tag are public. File size and external filesystem metadata can reveal context; there is no padding or size hiding. See the [security model](SECURITY_MODEL.md).

Changing these parameters requires an explicit reader/writer compatibility decision, normally a new format version. Retain old fixtures and test old-container recovery before changing the format. This document specifies existing v1 behavior; it does not reserve identifiers for unimplemented formats.
