$ErrorActionPreference = 'Stop'
dotnet build (Join-Path $PSScriptRoot 'devLibra.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
$releaseDirectory = Join-Path $PSScriptRoot 'bin/Release'
$releaseFiles = @('devLibra.dll', 'devLibra.json', 'devLibra.deps.json') |
    ForEach-Object { Join-Path $releaseDirectory $_ }
Compress-Archive -LiteralPath $releaseFiles -DestinationPath (Join-Path $releaseDirectory 'latest.zip') -Force
Write-Output (Join-Path $releaseDirectory 'latest.zip')
