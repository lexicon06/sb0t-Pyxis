# Compila la extension y la deja self-contained (fusiona las dependencias
# SixLabors dentro de extension.dll con ILRepack).
#
# Uso:  powershell -ExecutionPolicy Bypass -File .\build.ps1
#
# Resultado: bin\extension.dll  (DLL unica, lista para el server)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$proj = Join-Path $root "WeatherExtension.csproj"
$bin = Join-Path $root "bin"
$merged = Join-Path $bin "merged"

Write-Host "== 1/3 build ==" -ForegroundColor Cyan
& dotnet build $proj -c Release -o $bin
if ($LASTEXITCODE -ne 0) { throw "dotnet build fallo" }

Write-Host "== 2/3 ubicando ILRepack ==" -ForegroundColor Cyan
$ilrepack = Join-Path $root "tools\ILRepack.exe"
if (-not (Test-Path $ilrepack)) {
    $toolsDir = Join-Path $root "tools"
    New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null
    $nupkg = Join-Path $toolsDir "ilrepack.nupkg"
    $zip = Join-Path $toolsDir "ilrepack.zip"
    Write-Host "   descargando ILRepack 2.0.44 de nuget.org..."
    Invoke-WebRequest -UseBasicParsing `
        "https://api.nuget.org/v3-flatcontainer/ilrepack/2.0.44/ilrepack.2.0.44.nupkg" `
        -OutFile $nupkg
    Copy-Item $nupkg $zip -Force
    Expand-Archive -LiteralPath $zip -DestinationPath (Join-Path $toolsDir "pkg") -Force
    Copy-Item (Join-Path $toolsDir "pkg\tools\ILRepack.exe") $ilrepack -Force
    Remove-Item $nupkg, $zip -Force
    Remove-Item -Recurse -Force (Join-Path $toolsDir "pkg")
}

Write-Host "== 3/3 fusionando (self-contained) ==" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $merged | Out-Null
$inputs = @(
    (Join-Path $bin "extension.dll"),
    (Join-Path $bin "SixLabors.ImageSharp.dll"),
    (Join-Path $bin "SixLabors.ImageSharp.Drawing.dll"),
    (Join-Path $bin "SixLabors.Fonts.dll")
)
foreach ($i in $inputs) { if (-not (Test-Path $i)) { throw "falta $i" } }

& $ilrepack "/out:$(Join-Path $merged 'extension.dll')" @inputs /internalize /lib:$bin
if ($LASTEXITCODE -ne 0) { throw "ILRepack fallo" }

Copy-Item (Join-Path $merged "extension.dll") (Join-Path $bin "extension.dll") -Force

Write-Host ""
Write-Host "LISTO -> $bin\extension.dll (self-contained)" -ForegroundColor Green
Write-Host "Subi extension.dll + weather.json a <server>\Extensions\WeatherExtension\"
