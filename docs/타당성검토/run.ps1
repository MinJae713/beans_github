$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$outDir = Join-Path $scriptDir "out"
$srcFile = Join-Path $scriptDir "src\CookSsafyFeasibility.java"

$javaHomes = @(
    $env:JAVA_HOME,
    "$env:USERPROFILE\.jdks\ms-21.0.9",
    "$env:USERPROFILE\.jdks\ms-17.0.17",
    "$env:USERPROFILE\.jdks\openjdk-25.0.1",
    "C:\Program Files\JetBrains\IntelliJ IDEA 2025.3.1\jbr"
) | Where-Object { $_ -and (Test-Path (Join-Path $_ "bin\javac.exe")) }

if (-not $javaHomes) {
    throw "javac.exe를 찾지 못했습니다. JAVA_HOME 또는 로컬 JDK 경로를 확인하세요."
}

$javaHome = $javaHomes[0]
$javac = Join-Path $javaHome "bin\javac.exe"
$java = Join-Path $javaHome "bin\java.exe"

if (Test-Path $outDir) {
    Remove-Item -Recurse -Force $outDir
}
New-Item -ItemType Directory -Path $outDir | Out-Null

Write-Host "Using JDK: $javaHome"
& $javac -encoding UTF-8 -d $outDir $srcFile
& $java -cp $outDir cookssafy.feasibility.CookSsafyFeasibility
