# IBM Plex Mono

Official, unmodified full static TTF files from [IBM/plex](https://github.com/IBM/plex),
package `@ibm/plex-mono` 2.5.0, pinned commit `71f558992315a12ca1f06a2215d57962a1992162`.
Source directory: `packages/plex-mono/fonts/complete/ttf` (not a web subset).

| File | Weight | SHA-256 |
| --- | --- | --- |
| IBMPlexMono-Medium.ttf | 500 | `98fbd727aae340b236955879dabed4d991aac9e8e90b3b2a67ce4a59221cc97c` |
| IBMPlexMono-Regular.ttf | 400 | `7c6fbddca4b700be918f5f6183d9bd4464fa427fe435f0b480d77fe2bb8c5a43` |
| IBMPlexMono-SemiBold.ttf | 600 | `f04d7c488ddf7d1fa99f2574efc3406ea4cbe17bb1af3a1ab960f84d0c96a172` |

All three files have complete English and Russian alphabets, including Ё/ё, fixed
advance width, and every character used by the RU/EN UI resources. MAUI aliases:
`IBMPlexMonoRegular`, `IBMPlexMonoMedium`, `IBMPlexMonoSemiBold`.
No synthetic bold is used. Platform password masking is unchanged.

License: [SIL Open Font License 1.1](OFL.txt), copied from the same IBM commit;
the complete text is also available offline in Settings. Iosevka files are removed
from the current app resources. The previous font is documented in the historical
[UI report](../../../docs/ANDROID_UI_POLISH.md).
