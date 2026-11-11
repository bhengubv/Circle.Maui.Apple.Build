$ErrorActionPreference = 'Stop'

function Invoke-BuildStep([string]$Executable, [string[]]$Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Executable failed with exit code $LASTEXITCODE" }
}

Push-Location $PSScriptRoot
try {
    $out = New-Item -ItemType Directory -Force artifacts
    Invoke-BuildStep dotnet @('pack', 'package/Circle.Maui.Apple.Build.csproj', '-c', 'Release', '-o', $out.FullName)

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    $msbuild = & $vswhere -latest -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    if (!$msbuild) { throw 'Visual Studio MSBuild is required to build the Visual Studio VSIX.' }
    Invoke-BuildStep $msbuild @('visualstudio/Circle.Maui.Apple.VisualStudio.csproj', '/restore', '/p:Configuration=Release', '/m:1', '/nologo')
    Copy-Item visualstudio/bin/Release/net472/Circle.Maui.Apple.VisualStudio.vsix (Join-Path $out.FullName 'Circle.Maui.Apple.VisualStudio.1.0.0.vsix')

    New-Item -ItemType Directory -Force vscode/tools | Out-Null
    Copy-Item tools/xtool-maui-ipa.py vscode/tools
    Copy-Item LICENSE vscode/LICENSE
    Copy-Item README.md vscode/README.md
    Push-Location vscode
    try {
        Invoke-BuildStep node @('--check', 'extension.js')
        Invoke-BuildStep npx @('--yes', '@vscode/vsce@4.1.0', 'package', '--no-dependencies', '-o', (Join-Path $out.FullName 'circle-maui-apple-vscode-1.0.0.vsix'))
    } finally { Pop-Location }

    $assets = @('Circle.Maui.Apple.Build.1.0.0.nupkg', 'Circle.Maui.Apple.VisualStudio.1.0.0.vsix', 'circle-maui-apple-vscode-1.0.0.vsix')
    $assets | ForEach-Object {
        $hash = Get-FileHash -LiteralPath (Join-Path $out.FullName $_) -Algorithm SHA256
        "$($hash.Hash.ToLowerInvariant())  $_"
    } | Set-Content -Encoding ascii (Join-Path $out.FullName 'SHA256SUMS.txt')
} finally { Pop-Location }
