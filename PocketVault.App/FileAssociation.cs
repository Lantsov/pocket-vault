using System.Runtime.InteropServices;
using Microsoft.Win32;
using PocketVault.Core;

namespace PocketVault.App;

internal static class FileAssociation
{
    private const string Classes = @"Software\Classes";
    private const string ProgId = "PocketVault.EncryptedFile";
    private static string Executable => Environment.ProcessPath!;
    private static string IconPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PocketVault", "pocketvault-file.ico");

    public static void Register()
    {
        using var classes = Registry.CurrentUser.CreateSubKey(Classes);
        using var existing = classes.OpenSubKey(ProgId);
        if (existing?.GetValue("Owner") is string owner && !owner.Equals(Executable, StringComparison.OrdinalIgnoreCase))
            throw new VaultException("Регистрация принадлежит другой копии PocketVault. Отмените её из той копии приложения.");
        Directory.CreateDirectory(Path.GetDirectoryName(IconPath)!);
        using (var source = typeof(FileAssociation).Assembly.GetManifestResourceStream("PocketVault.FileIcon")!)
        using (var target = File.Create(IconPath)) source.CopyTo(target);
        using var ext = classes.CreateSubKey(".pvlt");
        using var type = classes.CreateSubKey(ProgId);
        if (type.GetValue("Owner") is null)
        {
            type.SetValue("HadDefault", ext.GetValue("") is string ? 1 : 0);
            type.SetValue("PreviousDefault", ext.GetValue("") as string ?? "");
        }
        type.SetValue("Owner", Executable);
        type.SetValue("", "PocketVault Encrypted File");
        using (var icon = type.CreateSubKey("DefaultIcon")) icon.SetValue("", $"\"{IconPath}\",0");
        using (var command = type.CreateSubKey(@"shell\open\command")) command.SetValue("", $"\"{Executable}\" \"%1\"");
        ext.SetValue("", ProgId);
        using (var openWith = ext.CreateSubKey("OpenWithProgids")) openWith.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
        Notify();
    }

    public static void Unregister()
    {
        using var classes = Registry.CurrentUser.CreateSubKey(Classes);
        using (var type = classes.OpenSubKey(ProgId))
        {
            if (type is null) return;
            if (type.GetValue("Owner") is not string owner || !owner.Equals(Executable, StringComparison.OrdinalIgnoreCase))
                throw new VaultException("Регистрация принадлежит другой копии PocketVault.");
            using var ext = classes.OpenSubKey(".pvlt", true);
            if (ext?.GetValue("") as string == ProgId)
            {
                if (type.GetValue("HadDefault") is int had && had == 1) ext.SetValue("", type.GetValue("PreviousDefault") as string ?? "");
                else ext.DeleteValue("", false);
            }
            using var openWith = ext?.OpenSubKey("OpenWithProgids", true);
            openWith?.DeleteValue(ProgId, false);
        }
        classes.DeleteSubKeyTree(ProgId, false);
        if (File.Exists(IconPath)) File.Delete(IconPath);
        Notify();
    }

    private static void Notify() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
}
