$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcDir = Join-Path $root 'src'
$installerDir = Join-Path $root 'installer'
$assetsDir = Join-Path $root 'assets'
$outDir = Join-Path $root 'dist'
$objDir = Join-Path $root 'obj'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    throw 'Compilador do .NET Framework 4.x nao encontrado.'
}

$version = '1.0.0'
if (Test-Path (Join-Path $root 'version.txt')) {
    $version = (Get-Content (Join-Path $root 'version.txt') -Raw).Trim()
}
if ([string]::IsNullOrWhiteSpace($version)) { $version = '1.0.0' }

$appExe = Join-Path $outDir 'ReInstala.exe'
if (-not (Test-Path $appExe)) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'build.ps1')
}

$icon = Join-Path $assetsDir 'icon.ico'
$logo = Join-Path $assetsDir 'icon-256.png'
if (-not (Test-Path $icon) -or -not (Test-Path $logo)) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $assetsDir 'make-icon.ps1')
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
New-Item -ItemType Directory -Force -Path $objDir | Out-Null

$asmInfo = Join-Path $objDir 'SetupAssemblyInfo.cs'
$info = @"
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("ReInstala Setup")]
[assembly: AssemblyDescription("Instalador do ReInstala")]
[assembly: AssemblyProduct("ReInstala")]
[assembly: AssemblyCompany("ReInstala contributors")]
[assembly: AssemblyCopyright("Copyright (c) 2026 ReInstala contributors")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("$version.0")]
[assembly: AssemblyFileVersion("$version.0")]
[assembly: AssemblyInformationalVersion("$version")]
"@
Set-Content -Path $asmInfo -Value $info -Encoding UTF8

$setupExe = Join-Path $outDir 'ReInstala-Setup.exe'

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('/nologo')
$lines.Add('/target:winexe')
$lines.Add('/platform:anycpu')
$lines.Add('/optimize+')
$lines.Add('/codepage:65001')
$lines.Add('/langversion:5')
$lines.Add('/win32icon:"' + $icon + '"')
$lines.Add('/win32manifest:"' + (Join-Path $srcDir 'app.manifest') + '"')
$lines.Add('/out:"' + $setupExe + '"')
$lines.Add('/resource:"' + $appExe + '",ReInstala.exe')
$lines.Add('/resource:"' + $logo + '",icon.png')
$lines.Add('/reference:System.dll')
$lines.Add('/reference:System.Core.dll')
$lines.Add('/reference:System.Drawing.dll')
$lines.Add('/reference:System.Windows.Forms.dll')
$lines.Add('"' + (Join-Path $installerDir 'Setup.cs') + '"')
$lines.Add('"' + (Join-Path $srcDir 'Theme.cs') + '"')
$lines.Add('"' + (Join-Path $srcDir 'Controls.cs') + '"')
$lines.Add('"' + $asmInfo + '"')

$rsp = Join-Path $env:TEMP 'reinstala-setup-build.rsp'
Set-Content -Path $rsp -Value $lines -Encoding Unicode

& $csc "@$rsp"
$code = $LASTEXITCODE
Remove-Item $rsp -ErrorAction SilentlyContinue

if ($code -ne 0) {
    throw "Falha na compilacao do instalador (codigo $code)."
}

$size = [math]::Round((Get-Item $setupExe).Length / 1KB, 1)
Write-Host ""
Write-Host "Instalador gerado: $setupExe ($size KB, versao $version)" -ForegroundColor Green
