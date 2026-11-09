param([string]$OutputDirectory = "$PSScriptRoot\..\dist")
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if(!(Test-Path -LiteralPath $compiler)){throw '.NET Framework 4.8 的 C# 编译器不可用。'}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$output = Join-Path (Resolve-Path $OutputDirectory) 'PowerWallet-Setup-1.0.0.exe'
$arguments = @('/nologo','/target:winexe','/platform:x64','/optimize+','/utf8output','/codepage:65001',"/out:$output","/win32manifest:$PSScriptRoot\app.manifest","/win32icon:$PSScriptRoot\assets\PowerWallet.ico",'/r:System.dll','/r:System.Core.dll','/r:System.Xaml.dll',"/r:$framework\WPF\WindowsBase.dll","/r:$framework\WPF\PresentationCore.dll","/r:$framework\WPF\PresentationFramework.dll")
foreach($asset in @('adb.exe','AdbWinApi.dll','AdbWinUsbApi.dll','PowerWallet-1.0.2.apk','NOTICE.txt','source.properties')) {
    $assetPath=Join-Path "$PSScriptRoot\assets" $asset
    if(!(Test-Path -LiteralPath $assetPath)){throw "缺少打包资源：$asset"}
    $arguments += "/resource:$assetPath,PowerWallet.Assets.$asset"
}
$arguments += @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object FullName)
& $compiler @arguments
if($LASTEXITCODE -ne 0){throw 'EXE 编译失败。'}
Write-Output "Built: $output"
