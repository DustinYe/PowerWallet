param(
    [string]$Sdk = "$env:LOCALAPPDATA\Android\Sdk",
    [string]$Jdk = "$env:USERPROFILE\.jdks\jbr-21.0.11",
    [string]$BuildDirectory = "$env:TEMP\PowerWallet-build",
    [string]$ApkPath = "$PSScriptRoot\..\PowerWallet-1.0.4.apk"
)
$ErrorActionPreference = 'Stop'
$env:JAVA_HOME = $Jdk
$bt = Join-Path $Sdk 'build-tools\36.0.0'
$androidJar = Join-Path $Sdk 'platforms\android-36\android.jar'
foreach ($p in @($Jdk,$bt,$androidJar)) { if (!(Test-Path -LiteralPath $p)) { throw "Missing build dependency: $p" } }
New-Item -ItemType Directory -Force -Path $BuildDirectory,"$BuildDirectory\classes","$BuildDirectory\dex","$BuildDirectory\tests" | Out-Null
function CheckExit { if($LASTEXITCODE -ne 0) { throw "Build tool failed with exit code $LASTEXITCODE" } }
$sources = @(Get-ChildItem -LiteralPath "$PSScriptRoot\src" -Recurse -Filter '*.java' | ForEach-Object FullName)
& "$Jdk\bin\javac.exe" -encoding UTF-8 -source 8 -target 8 -cp $androidJar -d "$BuildDirectory\classes" @sources
CheckExit
& "$Jdk\bin\javac.exe" -encoding UTF-8 -cp "$BuildDirectory\classes" -d "$BuildDirectory\tests" "$PSScriptRoot\test\PressDetectorTest.java"
CheckExit
& "$Jdk\bin\java.exe" -cp "$BuildDirectory\classes;$BuildDirectory\tests" PressDetectorTest
CheckExit
& "$Jdk\bin\jar.exe" cf "$BuildDirectory\classes.jar" -C "$BuildDirectory\classes" .
CheckExit
& "$bt\d8.bat" --lib $androidJar --min-api 28 --output "$BuildDirectory\dex" "$BuildDirectory\classes.jar"
CheckExit
& "$bt\aapt2.exe" compile --dir "$PSScriptRoot\res" -o "$BuildDirectory\res.zip"
CheckExit
& "$bt\aapt2.exe" link -I $androidJar --manifest "$PSScriptRoot\AndroidManifest.xml" -o "$BuildDirectory\unsigned.apk" "$BuildDirectory\res.zip"
CheckExit
& "$Jdk\bin\jar.exe" uf "$BuildDirectory\unsigned.apk" -C "$BuildDirectory\dex" classes.dex
CheckExit
& "$bt\zipalign.exe" -f 4 "$BuildDirectory\unsigned.apk" "$BuildDirectory\aligned.apk"
CheckExit
$keyPath = "$BuildDirectory\powerwallet-personal.jks"
if (!(Test-Path -LiteralPath $keyPath)) {
    & "$Jdk\bin\keytool.exe" -genkeypair -keystore $keyPath -alias powerwallet -keyalg RSA -keysize 3072 -validity 10000 -storepass powerwallet-local -keypass powerwallet-local -dname 'CN=PowerWallet Personal Build'
    CheckExit
}
& "$bt\apksigner.bat" sign --ks $keyPath --ks-key-alias powerwallet --ks-pass pass:powerwallet-local --key-pass pass:powerwallet-local --out $ApkPath "$BuildDirectory\aligned.apk"
CheckExit
& "$bt\apksigner.bat" verify --verbose $ApkPath
CheckExit
Write-Output "Built: $ApkPath"
