# Builds Gravity with the C# compiler that ships with Windows (.NET Framework 4.8).
# No SDK or Visual Studio needed.
#
#   .\build.ps1          build bin\Gravity.exe
#   .\build.ps1 -Test    build, then compile and run the offline self-tests
#   .\build.ps1 -Package build, then write a portable zip to dist\

param([switch]$Test, [switch]$Package)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'Use bash build-macos.sh on macOS. This script builds the Windows app.'
}

function Remove-BuildDirectory([string]$Path, [string]$Parent) {
    $targetPath = [IO.Path]::GetFullPath($Path)
    $parentPath = [IO.Path]::GetFullPath($Parent).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $targetPath.StartsWith($parentPath, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing to remove a directory outside the expected build location.'
    }
    if (Test-Path -LiteralPath $targetPath) { Remove-Item -LiteralPath $targetPath -Recurse -Force }
}
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "C# compiler not found at $csc (.NET Framework 4.8 is required)." }

$bin = Join-Path $root 'bin'
New-Item -ItemType Directory -Force $bin | Out-Null

$icon = Join-Path $root 'src\Gravity.ico'
foreach ($asset in 'Gravity.ico', 'GravityPdf.ico') {
    if (-not (Test-Path (Join-Path $root "src\$asset"))) { throw "src\$asset is missing. Regenerate icons: python tools\make_brand.py" }
}
if (-not (Test-Path (Join-Path $root 'src\Lockup.png'))) { throw 'src\Lockup.png is missing. Restore the finished logo artwork from the repository.' }
$resources = @("/resource:$root\src\Gravity.ico,Gravity.ico", "/resource:$root\src\Lockup.png,Lockup.png", "/resource:$root\assets\g-mark.png,GMark.png")
Get-ChildItem (Join-Path $root 'third_party\lucide\icons') -Filter *.svg | ForEach-Object {
    $resources += "/resource:$($_.FullName),Lucide.$($_.Name)"
}

$sources = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
$refs = @('/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Xml.dll', '/r:Accessibility.dll')
$common = @('/nologo', '/platform:x64', '/optimize+', '/langversion:5', '/warn:4', '/nowarn:1591') + $refs

Write-Host 'Compiling Gravity.exe...'
& $csc @common /target:winexe "/out:$bin\Gravity.exe" "/win32manifest:$root\src\app.manifest" "/win32icon:$icon" /main:Gravity.Program @resources @sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

Copy-Item (Join-Path $root 'third_party\pdfium\pdfium.dll') $bin -Force
Copy-Item (Join-Path $root 'src\GravityPdf.ico') $bin -Force
$lic = Join-Path $bin 'licenses'
New-Item -ItemType Directory -Force $lic | Out-Null
Copy-Item (Join-Path $root 'third_party\pdfium\LICENSE') (Join-Path $lic 'pdfium-binaries.txt') -Force
Copy-Item (Join-Path $root 'third_party\pdfium\licenses\*') $lic -Force
Copy-Item (Join-Path $root 'LICENSE') (Join-Path $lic 'Gravity.txt') -Force
Copy-Item (Join-Path $root 'third_party\lucide\LICENSE') (Join-Path $lic 'Lucide.txt') -Force
Copy-Item (Join-Path $root 'THIRD_PARTY_NOTICES.md') $lic -Force

$size = [Math]::Round(((Get-Item "$bin\Gravity.exe").Length / 1KB), 0)
Write-Host "Built $bin\Gravity.exe ($size KB) + pdfium.dll + GravityPdf.ico"

if ($Test) {
    Write-Host 'Compiling self-tests...'
    $testSources = $sources + (Get-ChildItem (Join-Path $root 'tests') -Filter *.cs | ForEach-Object { $_.FullName })
    & $csc @common /target:exe "/out:$bin\GravityTests.exe" /main:Gravity.Tests.SelfTest @resources @testSources
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    $work = Join-Path ([System.IO.Path]::GetTempPath()) ('gravity-tests-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    & "$bin\GravityTests.exe" $work
    $code = $LASTEXITCODE
    Remove-BuildDirectory $work ([IO.Path]::GetTempPath())
    if ($code -ne 0) { throw "Self-tests failed ($code)." }
}

if ($Package) {
    $ver = (Get-Item "$bin\Gravity.exe").VersionInfo.FileVersion -replace '\.0$', ''
    $dist = Join-Path $root 'dist'
    $stage = Join-Path $dist "Gravity-$ver"
    Remove-BuildDirectory $stage $dist
    New-Item -ItemType Directory -Force $stage | Out-Null
    Copy-Item "$bin\Gravity.exe", "$bin\pdfium.dll", "$bin\GravityPdf.ico" $stage
    Copy-Item $lic (Join-Path $stage 'licenses') -Recurse
    Copy-Item (Join-Path $root 'README.md') $stage
    $zip = Join-Path $dist "Gravity-$ver-win-x64.zip"
    if (Test-Path $zip) { Remove-Item -Force $zip }
    Compress-Archive -Path "$stage\*" -DestinationPath $zip
    Remove-BuildDirectory $stage $dist
    Write-Host "Packaged $zip"
}
