# Display Sleep Enforcer - force display sleep, including while locked.
# Copyright (C) 2026 AlbaCorey
#
# This program is free software: you can redistribute it and/or modify it under
# the terms of the GNU General Public License as published by the Free Software
# Foundation, either version 3 of the License, or (at your option) any later
# version.
#
# This program is distributed in the hope that it will be useful, but WITHOUT
# ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
# FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License along with
# this program. If not, see <https://www.gnu.org/licenses/>.
# ---------------------------------------------------------------------------
#  Builds Display Sleep Enforcer (DSE):
#    1. generate the application icon      (make-icon.ps1 -> source\DSE.ico)
#    2. compile the WinForms application   (csc  -> compiled\DisplaySleepEnforcer.exe)
#    3. build the installer                (ISCC -> compiled\DSE-Setup.exe)
#
#  Optional Authenticode signing:
#    Create source\signing.ps1 (copy signing.example.ps1 and fill it in). When
#    present and valid, this script defines an Inno "dse_sign" Sign Tool and
#    passes /DDSE_SIGN so the app exe ([Files] signonce), Setup.exe and the
#    uninstaller are all signed in the single ISCC run below.
#
#  Usage:  powershell -ExecutionPolicy Bypass -File source\build.ps1 [-NoSign]
# ---------------------------------------------------------------------------
param(
    [switch]$NoSign
)

$ErrorActionPreference = 'Stop'

$sourceDir   = $PSScriptRoot
$projectDir  = Split-Path -Parent $sourceDir
$compiledDir = Join-Path $projectDir 'compiled'

$exePath = Join-Path $compiledDir 'DisplaySleepEnforcer.exe'
$icoPath = Join-Path $sourceDir 'DSE.ico'
$csPath  = Join-Path $sourceDir 'DisplaySleepEnforcer.cs'
$issPath = Join-Path $sourceDir 'DSE.iss'

$csc  = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$iscc = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'

if (-not (Test-Path -LiteralPath $compiledDir)) {
    New-Item -ItemType Directory -Path $compiledDir | Out-Null
}

# --- Optional code-signing configuration -----------------------------------
# State consumed below. Defaults to an unsigned build.
$isccArgs = @($issPath)

function Find-Signtool {
    $cmd = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $roots = @(
        (Join-Path $env:ProgramFiles 'Windows Kits'),
        (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits')
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
    foreach ($root in $roots) {
        $hit = Get-ChildItem -LiteralPath $root -Filter 'signtool.exe' -Recurse -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($hit) { return $hit.FullName }
    }
    return $null
}

$signingConfigPath = Join-Path $sourceDir 'signing.ps1'
if ($NoSign) {
    Write-Host 'Signing disabled (-NoSign).'
}
elseif (-not (Test-Path -LiteralPath $signingConfigPath)) {
    Write-Host 'Signing not configured (source\signing.ps1 not found) - building unsigned.'
}
else {
    . $signingConfigPath   # defines $SigntoolPath, $CertThumbprint / $PfxPath, $TimestampUrl

    $signtool = if ($SigntoolPath) { $SigntoolPath } else { Find-Signtool }
    if (-not $signtool -or -not (Test-Path -LiteralPath $signtool)) {
        Write-Warning 'Signing requested but signtool.exe was not found - building unsigned.'
    }
    elseif (-not $CertThumbprint -and -not $PfxPath) {
        Write-Warning 'Signing requested but neither CertThumbprint nor PfxPath is set - building unsigned.'
    }
    else {
        # Build the Sign Tool command. $q (quote) and $f (file) are Inno
        # placeholders expanded by ISCC; keep them in single-quoted fragments.
        $toolCmd = '$q' + $signtool + '$q sign /fd sha256'
        if ($PfxPath) {
            $toolCmd += ' /f $q' + $PfxPath + '$q'
            if ($PfxPassword) { $toolCmd += ' /p $q' + $PfxPassword + '$q' }
        }
        else {
            $toolCmd += ' /sha1 ' + $CertThumbprint
        }
        if ($TimestampUrl) { $toolCmd += ' /tr ' + $TimestampUrl + ' /td sha256' }
        $toolCmd += ' /d $qDisplay Sleep Enforcer$q $f'

        $isccArgs = @('/DDSE_SIGN', "/Sdse_sign=$toolCmd", $issPath)
        Write-Host ("Signing enabled using: {0}" -f $signtool)
    }
}

Write-Host '[1/3] Generating icon...'
& (Join-Path $sourceDir 'make-icon.ps1')

Write-Host '[2/3] Compiling application...'
& $csc /nologo /target:winexe /optimize+ "/out:$exePath" "/win32icon:$icoPath" `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll $csPath
if ($LASTEXITCODE -ne 0) { throw "Application compile failed (exit $LASTEXITCODE)." }

Write-Host '[3/3] Building installer...'
& $iscc @isccArgs
if ($LASTEXITCODE -ne 0) { throw "Installer build failed (exit $LASTEXITCODE)." }

Write-Host 'Build complete.'
