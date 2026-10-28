# Android Intent probe (test only)

A separate native Java APK sends real temporary read grants for a public encrypted
fixture. It does not ship inside PocketVault and needs no storage/network permission.
Its provider is not exported. Numeric URI IDs test filename-independent document
handling; the exported sender is for use on a test device only.

Build with `scripts/build-android-intent-probe.ps1`, specifying Android SDK 36,
JDK 21 and the existing standard Android **debug** keystore. The conventional public
`android` development password is used only for this test package, never for a release key.
Install the returned probe APK, then use:

```powershell
adb shell am force-stop com.lantsov.pocketvault
adb shell am start -W -n com.lantsov.pocketvault.intentprobe/.IntentProbe --es id 17
# PocketVault must select cold.PVLT without asking for a password or creating output.
adb shell am start -W -n com.lantsov.pocketvault.intentprobe/.IntentProbe --es id 18
# Existing PocketVault activity must select warm.pvlt and clear its password.
adb shell am start -W -n com.lantsov.pocketvault.intentprobe/.IntentProbe --es id 19
# A provider claiming the custom MIME with wrong.bin must be rejected.
adb shell am start -W -n com.lantsov.pocketvault.intentprobe/.IntentProbe --es id 17
adb shell am start -W -n com.lantsov.pocketvault.intentprobe/.IntentProbe --ez revoke true
# Return to PocketVault, enter the public fixture password, attempt decryption:
# "Public fixture password 1.1.0". Revoked read access must fail without an output picker.
```

The shell cannot grant another provider's private document URI, which is why the
probe is needed. Remove the probe with `adb uninstall com.lantsov.pocketvault.intentprobe`
after testing. Do not use personal documents or install it as a production component.
