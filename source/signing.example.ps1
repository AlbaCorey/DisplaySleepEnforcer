# Display Sleep Enforcer - force display sleep, including while locked.
# Copyright (C) 2026 ferdef150
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
#  Optional code-signing configuration for Display Sleep Enforcer.
#
#  To enable signing: copy this file to  source\signing.ps1  (same folder,
#  without ".example") and fill in the values below. Do NOT commit the real
#  signing.ps1 if it contains a PFX password.
#
#  build.ps1 dot-sources signing.ps1 when it exists and uses these variables.
#  Anything left unset is ignored. Expects signtool.exe (Windows SDK).
# ---------------------------------------------------------------------------

# Path to signtool.exe. Leave empty to auto-detect from the Windows SDK.
$SigntoolPath = ''

# Option A - certificate in the Windows certificate store (recommended).
#   Thumbprint of a code-signing cert in the CurrentUser\My or LocalMachine\My
#   store (find it with:  Get-ChildItem Cert:\CurrentUser\My | Format-List Subject,Thumbprint)
$CertThumbprint = ''

# Option B - certificate in a PFX file. If both are set, the PFX is used.
$PfxPath     = ''
$PfxPassword = ''

# RFC 3161 timestamp server so signatures outlive the certificate's expiry.
# Common choices: http://timestamp.digicert.com  /  http://timestamp.sectigo.com
$TimestampUrl = 'http://timestamp.digicert.com'
