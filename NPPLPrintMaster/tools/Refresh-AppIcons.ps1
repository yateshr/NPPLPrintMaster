param([string[]]$Paths)
$ErrorActionPreference = 'Stop'
if (-not $Paths) {
    $Paths = @((Join-Path $PSScriptRoot '../bin/Release/NPPLPrintMaster.exe'))
}
if (-not ('NPPLShellIcons' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class NPPLShellIcons {
    [DllImport("shell32.dll")]
    public static extern void SHChangeNotify(int change, uint flags, IntPtr item1, IntPtr item2);
}
'@
}
foreach ($item in $Paths) {
    $resolved = (Resolve-Path -LiteralPath $item).Path
    $nativePath = [Runtime.InteropServices.Marshal]::StringToHGlobalUni($resolved)
    try {
        # SHCNE_UPDATEITEM, SHCNF_PATHW | SHCNF_FLUSH.
        [NPPLShellIcons]::SHChangeNotify(0x2000, 0x1005, $nativePath, [IntPtr]::Zero)
    } finally { [Runtime.InteropServices.Marshal]::FreeHGlobal($nativePath) }
}
# Ask Explorer to invalidate its icon associations. No cache files are deleted,
# Explorer is not restarted, and no file association is changed.
[NPPLShellIcons]::SHChangeNotify(0x08000000, 0x1000, [IntPtr]::Zero, [IntPtr]::Zero)
Write-Output 'Windows shell icon refresh requested.'
