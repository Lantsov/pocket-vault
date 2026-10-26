param(
    [string]$DotnetRoot = '',
    [string]$BundleExtractBaseDirectory = '',
    [string]$PublishRoot = ''
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $PublishRoot) { $PublishRoot = Join-Path $projectRoot 'dist' }
if (-not ('PocketVaultPublishSmoke' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class PocketVaultPublishSmoke {
    private delegate bool Callback(IntPtr window, IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Callback callback, IntPtr state);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
    public static IntPtr Find(uint processId) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((window, state) => {
            GetWindowThreadProcessId(window, out uint id);
            if (id != processId) return true;
            var text = new StringBuilder(128);
            GetWindowText(window, text, text.Capacity);
            if (text.ToString() == "PocketVault") { found = window; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
'@
}
$variants = @('self-contained')
if ($DotnetRoot) { $variants += 'framework-dependent' }
foreach ($variant in $variants) {
    $exe = Join-Path $PublishRoot "$variant\PocketVault.exe"
    $start = [Diagnostics.ProcessStartInfo]::new($exe)
    $start.UseShellExecute = $false
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.Environment.Remove('DOTNET_ROOT') | Out-Null
    $start.Environment.Remove('DOTNET_ROOT_X64') | Out-Null
    $start.Environment['PATH'] = Join-Path $env:SystemRoot 'System32'
    if ($BundleExtractBaseDirectory) { $start.Environment['DOTNET_BUNDLE_EXTRACT_BASE_DIR'] = $BundleExtractBaseDirectory }
    if ($variant -eq 'framework-dependent') { $start.Environment['DOTNET_ROOT'] = $DotnetRoot }
    $process = [Diagnostics.Process]::Start($start)
    try {
        if (-not $process.WaitForInputIdle(20000)) { throw "Окно не готово: $variant" }
        $window = [IntPtr]::Zero
        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        while (-not $process.HasExited -and $window -eq [IntPtr]::Zero -and [DateTime]::UtcNow -lt $deadline) {
            $window = [PocketVaultPublishSmoke]::Find($process.Id)
            if ($window -eq [IntPtr]::Zero) { Start-Sleep -Milliseconds 100 }
        }
        if ($process.HasExited -or $window -eq [IntPtr]::Zero) {
            throw "Окно PocketVault не найдено: $variant"
        }
        Write-Output "[OK] $variant : окно запущено, заголовок PocketVault"
        [PocketVaultPublishSmoke]::PostMessage($window, 0x10, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
        if (-not $process.WaitForExit(10000)) { throw "Программа не завершилась: $variant" }
        if ($process.ExitCode -ne 0) { throw "Ненулевой код завершения: $variant" }
    }
    finally {
        if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
        $process.Dispose()
    }
}
