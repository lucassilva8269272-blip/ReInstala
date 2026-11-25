$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcDir = Join-Path $root 'src'
$assetsDir = Join-Path $root 'assets'
$outDir = Join-Path $root 'dist'
$appDir = Join-Path $outDir 'app'
$objDir = Join-Path $root 'obj'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    throw 'Compilador do .NET Framework 4.x nao encontrado.'
}

$versionFile = Join-Path $root 'version.txt'
$version = '1.0.0'
if (Test-Path $versionFile) {
    $version = (Get-Content $versionFile -Raw).Trim()
}
if ([string]::IsNullOrWhiteSpace($version)) { $version = '1.0.0' }

$icon = Join-Path $assetsDir 'icon.ico'
if (-not (Test-Path $icon)) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $assetsDir 'make-icon.ps1')
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
New-Item -ItemType Directory -Force -Path $appDir | Out-Null
New-Item -ItemType Directory -Force -Path $objDir | Out-Null

$asmInfo = Join-Path $objDir 'AssemblyInfo.cs'
$info = @"
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("ReInstala")]
[assembly: AssemblyDescription("Back up and restore your Windows programs with Winget.")]
[assembly: AssemblyProduct("ReInstala")]
[assembly: AssemblyCompany("ReInstala contributors")]
[assembly: AssemblyCopyright("Copyright (c) 2026 ReInstala contributors")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("$version.0")]
[assembly: AssemblyFileVersion("$version.0")]
[assembly: AssemblyInformationalVersion("$version")]
"@
Set-Content -Path $asmInfo -Value $info -Encoding UTF8

$exe = Join-Path $appDir 'ReInstala.exe'
$sources = @(Get-ChildItem $srcDir -Filter *.cs | Sort-Object Name)
$sources += Get-Item $asmInfo

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('/nologo')
$lines.Add('/target:winexe')
$lines.Add('/platform:anycpu')
$lines.Add('/optimize+')
$lines.Add('/codepage:65001')
$lines.Add('/langversion:5')
$lines.Add('/win32icon:"' + $icon + '"')
$lines.Add('/win32manifest:"' + (Join-Path $srcDir 'app.manifest') + '"')
$lines.Add('/out:"' + $exe + '"')
$lines.Add('/reference:System.dll')
$lines.Add('/reference:System.Core.dll')
$lines.Add('/reference:System.Drawing.dll')
$lines.Add('/reference:System.Windows.Forms.dll')
$lines.Add('/reference:System.Runtime.Serialization.dll')
$lines.Add('/reference:System.Xml.dll')
foreach ($file in $sources) {
    $lines.Add('"' + $file.FullName + '"')
}

$rsp = Join-Path $env:TEMP 'reinstala-build.rsp'
Set-Content -Path $rsp -Value $lines -Encoding Unicode

& $csc "@$rsp"
$code = $LASTEXITCODE
Remove-Item $rsp -ErrorAction SilentlyContinue

if ($code -ne 0) {
    throw "Falha na compilacao (codigo $code)."
}

$size = [math]::Round((Get-Item $exe).Length / 1KB, 1)
Write-Host ""
Write-Host "Aplicativo compilado: $exe ($size KB, versao $version)" -ForegroundColor Green
