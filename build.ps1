# bin\TranslatorShortcut.exe 를 빌드한다.
# 사용법: powershell -ExecutionPolicy Bypass -File .\build.ps1
# Windows에 기본으로 들어 있는 .NET Framework 4.x 컴파일러(C# 5)를 쓰므로 별도 SDK가 필요 없다.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $framework 'csc.exe'
if (-not (Test-Path $csc)) { throw ".NET Framework 4.x 컴파일러를 찾지 못했습니다: $csc" }

if (-not (Test-Path 'assets\app.ico')) { & (Join-Path $PSScriptRoot 'tools\make-icon.ps1') }

New-Item -ItemType Directory -Force 'bin' | Out-Null

$references = @(
  'System.dll',
  'System.Drawing.dll',
  'System.Windows.Forms.dll',
  (Join-Path $framework 'WPF\UIAutomationClient.dll'),
  (Join-Path $framework 'WPF\UIAutomationTypes.dll'),
  (Join-Path $framework 'WPF\WindowsBase.dll'),
  'System.Web.Extensions.dll'
) | ForEach-Object { "/reference:$_" }

$sources = Get-ChildItem 'src' -Filter '*.cs' | ForEach-Object { $_.FullName }

& $csc /nologo /codepage:65001 /target:winexe /optimize+ `
  /out:bin\TranslatorShortcut.exe `
  /win32icon:assets\app.ico `
  /win32manifest:assets\app.manifest `
  /resource:assets\app.ico,TranslatorShortcut.app.ico `
  $references $sources
if ($LASTEXITCODE -ne 0) { throw "빌드 실패 (종료 코드 $LASTEXITCODE)" }

Write-Host "빌드 완료: $((Resolve-Path 'bin\TranslatorShortcut.exe').Path)"
