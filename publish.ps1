# Builds QRCodeLogo into a single self-contained .exe (no .NET install required on the target PC).
# Output: .\publish\QRCodeLogo.exe  (just the one file)
# The app creates the "Logo" and "QR" folders next to the .exe the first time it runs.

$ErrorActionPreference = "Stop"
$proj = Join-Path $PSScriptRoot "QRCodeLogo\QRCodeLogo.csproj"
$out  = Join-Path $PSScriptRoot "publish"

dotnet publish $proj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $out

Write-Host ""
Write-Host "Done. Single file at: $(Join-Path $out 'QRCodeLogo.exe')"
