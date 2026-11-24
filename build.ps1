# Builds Gravity with the C# compiler that ships with Windows (.NET Framework 4.8).
# No SDK or Visual Studio needed.
#
#   .\build.ps1          build bin\Gravity.exe
#   .\build.ps1 -Test    build, then compile and run the offline self-tests
#   .\build.ps1 -Package build, then write a portable zip to dist\

param([switch]$Test, [switch]$Package)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "C# compiler not found at $csc (.NET Framework 4.8 is required)." }

$bin = Join-Path $root 'bin'
New-Item -ItemType Directory -Force $bin | Out-Null

$icon = Join-Path $root 'src\Gravity.ico'
foreach ($asset in 'Gravity.ico', 'GravityPdf.ico', 'Lockup.png') {
    if (-not (Test-Path (Join-Path $root "src\$asset"))) { throw "src\$asset is missing. Regenerate brand assets: python tools\make_brand.py" }
}
$resources = @("/resource:$root\src\Gravity.ico,Gravity.ico", "/resource:$root\src\Lockup.png,Lockup.png")

$sources = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
$refs = @('/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll')
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
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
    if ($code -ne 0) { throw "Self-tests failed ($code)." }
}

if ($Package) {
    $ver = (Get-Item "$bin\Gravity.exe").VersionInfo.FileVersion -replace '\.0$', ''
    $dist = Join-Path $root 'dist'
    $stage = Join-Path $dist "Gravity-$ver"
    if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
    New-Item -ItemType Directory -Force $stage | Out-Null
    Copy-Item "$bin\Gravity.exe", "$bin\pdfium.dll", "$bin\GravityPdf.ico" $stage
    Copy-Item $lic (Join-Path $stage 'licenses') -Recurse
    Copy-Item (Join-Path $root 'README.md') $stage
    $zip = Join-Path $dist "Gravity-$ver-win-x64.zip"
    if (Test-Path $zip) { Remove-Item -Force $zip }
    Compress-Archive -Path "$stage\*" -DestinationPath $zip
    Remove-Item -Recurse -Force $stage
    Write-Host "Packaged $zip"
}
