$ErrorActionPreference = 'Stop'
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $taskCompiler)) { throw 'The 64-bit .NET Framework C# compiler is not installed.' }
$taskOutput = Join-Path $PSScriptRoot 'Auto Loop.exe'
& $taskCompiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$taskOutput" ("/win32icon:" + (Join-Path $PSScriptRoot 'AutoLoop.ico')) ("/resource:" + (Join-Path $PSScriptRoot 'Logo.png') + ',AutoLoop.Logo') /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Xml.dll /r:System.Core.dll (Join-Path $PSScriptRoot 'AutoLoop.cs') (Join-Path $PSScriptRoot 'Theme.cs')
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output "Built $taskOutput"

