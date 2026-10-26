# Using PocketVault

[English README](../README.md) · [Русская версия](README.ru.md)

Extract a release ZIP before starting `PocketVault.exe`. Use a Windows version supported by .NET 10. The compact build requires .NET Desktop Runtime 10 **x64**; the base .NET Runtime alone does not include WPF. The UI is currently in Russian.

## Encrypt and restore

Choose a file with the dashed drop area, drag one file into the window, or pass its path on the command line. A `.pvlt` extension selects decryption; other extensions select encryption. A source file already named `.pvlt` must be renamed before encrypting it through this UI.

Enter a long, unique password and repeat it for encryption. Passwords are case-sensitive; leading/trailing spaces and Unicode characters are preserved. The eye button reveals only the primary password; confirmation stays masked. Selecting or clearing a file and starting an operation clear password fields.

**Зашифровать** saves a new container. **Расшифровать** asks for an output folder and restores the name from the encrypted archive. Existing files are never overwritten: choose another folder or resolve the name conflict and retry. Cancelling a destination dialog does not start an operation. Keep tested backups and the password separately.

`Tab` moves focus and `Enter` invokes the available action. While processing, inputs/settings are disabled and a moving indicator is shown; it does not represent a percentage. The window can be minimized. Closing is blocked until the operation finishes; cancelling a running operation is not implemented.

The original stays on disk by default. Enabling **Удалить исходник после проверки контейнера** requests a separate confirmation, then read-back verification before ordinary deletion. This is irreversible through the Recycle Bin and is not secure erasure. See the [security model](SECURITY_MODEL.md).

## Explorer integration

Settings → **Зарегистрировать** explicitly registers `.pvlt` for the current user under `HKEY_CURRENT_USER\Software\Classes`. It does not require administrator rights or happen automatically on launch. Double-clicking a registered container opens decryption mode. Command-line use also works:

```powershell
.\PocketVault.exe "D:\Demo\archive.pvlt"
```

Registration extracts the document icon to `%LOCALAPPDATA%\PocketVault\pocketvault-file.ico`. It remembers a previous extension association and refuses to silently take over another registered PocketVault copy. Unregister through the same copy's settings **before** moving the EXE, then register from its new location. Keep all files in a framework-dependent distribution together.

Unregistration removes PocketVault's entries and icon, restores its saved default when still applicable and leaves unrelated settings alone. Windows may retain the user's chosen default; use **Open with** if needed. The protected Windows `UserChoice` registry setting is not modified. Unregistration does not erase user files.

## Errors and limits

A wrong password and damaged ciphertext cannot be distinguished reliably from the authentication failure. Verify the selected file and password; restore from backup if needed. One ordinary file up to 128 MiB is supported. Folders, multiple selection and preservation of all filesystem metadata are not supported. See [BUILD.md](BUILD.md) for unsigned-build and runtime checks.
