$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
 dotnet run --project tests/Tests.csproj -c Release
 if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
 dotnet publish src/Legacy.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o release
 if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
} finally { Pop-Location }
