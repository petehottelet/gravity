# Installs Gravity for the current user (no admin needed) and registers it as a PDF handler.
#
#   .\tools\install.ps1              install or update from bin\, then open Default apps
#   .\tools\install.ps1 -NoSettings  same, without opening Settings
#   .\tools\install.ps1 -Uninstall   remove the installed copy, shortcut, and registration
#
# Windows does not let programs silently take over a file type, so the last step is yours:
# in the Settings page this script opens, choose Gravity for .pdf (or click "Set default").

param([switch]$Uninstall, [switch]$NoSettings)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$bin = Join-Path $root 'bin'
$dest = Join-Path $env:LOCALAPPDATA 'Programs\Gravity'
$exe = Join-Path $dest 'Gravity.exe'
$progId = 'Gravity.PDF'
$classes = 'HKCU:\Software\Classes'
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Gravity.lnk'

try {
    Add-Type -Namespace GravitySetup -Name Shell -MemberDefinition '[DllImport("shell32.dll")] public static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);'
} catch { }

function Ensure-Key([string]$path) {
    if (-not (Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
}

function Test-InstalledRunning {
    @(Get-Process Gravity -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }).Count -gt 0
}

function Remove-Registration {
    foreach ($k in "$classes\$progId", "$classes\Gravity.Image", "$classes\Applications\Gravity.exe", 'HKCU:\Software\Gravity') {
        if (Test-Path $k) { Remove-Item -Path $k -Recurse -Force }
    }
    foreach ($extension in '.pdf', '.png', '.jpg', '.jpeg', '.bmp') {
        $handler = if ($extension -eq '.pdf') { $progId } else { 'Gravity.Image' }
        if (Test-Path "$classes\$extension\OpenWithProgids") {
            Remove-ItemProperty -Path "$classes\$extension\OpenWithProgids" -Name $handler -ErrorAction SilentlyContinue
        }
    }
    Remove-ItemProperty -Path 'HKCU:\Software\RegisteredApplications' -Name 'Gravity' -ErrorAction SilentlyContinue
}

function Notify-Shell {
    try { [GravitySetup.Shell]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero) } catch { }
}

if ($Uninstall) {
    if (Test-InstalledRunning) { throw 'Close Gravity first, then run the uninstall again.' }
    Remove-Registration
    if (Test-Path $shortcut) { Remove-Item -Force $shortcut }
    if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
    Notify-Shell
    Write-Host 'Gravity was uninstalled. Windows will ask which app to use for PDFs next time.'
    return
}

if (-not (Test-Path (Join-Path $bin 'GravityPdf.ico'))) { throw 'Build first: .\build.ps1' }
if (Test-InstalledRunning) { throw 'Close the installed copy of Gravity before updating it.' }

# 1. Copy the app to a stable per-user location (independent of the build folder).
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item (Join-Path $bin 'Gravity.exe'), (Join-Path $bin 'pdfium.dll'), (Join-Path $bin 'GravityPdf.ico') $dest -Force
if (Test-Path (Join-Path $bin 'licenses')) { Copy-Item (Join-Path $bin 'licenses') $dest -Recurse -Force }

$command = '"' + $exe + '" "%1"'
$icon = '"' + $exe + '",0'
$docIcon = Join-Path $dest 'GravityPdf.ico'   # what PDF files show in Explorer

# 2. A ProgID describing how Gravity opens PDFs.
Ensure-Key "$classes\$progId\shell\open\command"
Ensure-Key "$classes\$progId\DefaultIcon"
Set-Item -Path "$classes\$progId" -Value 'PDF Document'
Set-ItemProperty -Path "$classes\$progId" -Name 'FriendlyTypeName' -Value 'PDF Document'
Set-Item -Path "$classes\$progId\DefaultIcon" -Value $docIcon
Set-Item -Path "$classes\$progId\shell\open\command" -Value $command

# 3. Offer Gravity in "Open with" for .pdf.
Ensure-Key "$classes\.pdf\OpenWithProgids"
New-ItemProperty -Path "$classes\.pdf\OpenWithProgids" -Name $progId -PropertyType String -Value '' -Force | Out-Null
Ensure-Key "$classes\Applications\Gravity.exe\shell\open\command"
Ensure-Key "$classes\Applications\Gravity.exe\SupportedTypes"
Set-ItemProperty -Path "$classes\Applications\Gravity.exe" -Name 'FriendlyAppName' -Value 'Gravity'
Set-Item -Path "$classes\Applications\Gravity.exe\shell\open\command" -Value $command
New-ItemProperty -Path "$classes\Applications\Gravity.exe\SupportedTypes" -Name '.pdf' -PropertyType String -Value '' -Force | Out-Null

# 4. List Gravity under Settings > Apps > Default apps.
Ensure-Key 'HKCU:\Software\Gravity\Capabilities\FileAssociations'
Set-ItemProperty -Path 'HKCU:\Software\Gravity\Capabilities' -Name 'ApplicationName' -Value 'Gravity'
Set-ItemProperty -Path 'HKCU:\Software\Gravity\Capabilities' -Name 'ApplicationDescription' -Value 'Fast PDF viewer and page organizer'
Set-ItemProperty -Path 'HKCU:\Software\Gravity\Capabilities' -Name 'ApplicationIcon' -Value $icon
Set-ItemProperty -Path 'HKCU:\Software\Gravity\Capabilities\FileAssociations' -Name '.pdf' -Value $progId
Ensure-Key 'HKCU:\Software\RegisteredApplications'
Set-ItemProperty -Path 'HKCU:\Software\RegisteredApplications' -Name 'Gravity' -Value 'Software\Gravity\Capabilities'

# 5. Start menu shortcut.
$ws = New-Object -ComObject WScript.Shell
$lnk = $ws.CreateShortcut($shortcut)
$lnk.TargetPath = $exe
$lnk.WorkingDirectory = $dest
$lnk.Description = 'Fast PDF viewer and page organizer'
$lnk.Save()

# Reapply the user's saved color after replacing the executable and registration.
$iconRefresh = Start-Process -FilePath $exe -ArgumentList '--register-file-types' -WindowStyle Hidden -Wait -PassThru
if ($iconRefresh.ExitCode -ne 0) {
    Write-Warning 'Gravity was installed, but its file types and saved icon color could not be applied. Choose File > File Associations or File > Icon Color in the app to retry.'
}

Notify-Shell
Write-Host "Installed to $dest and registered for .pdf files."

if (-not $NoSettings) {
    Write-Host 'Opening Default apps: choose Gravity for .pdf (or click "Set default").'
    Start-Process 'ms-settings:defaultapps?registeredAppUser=Gravity'
}
