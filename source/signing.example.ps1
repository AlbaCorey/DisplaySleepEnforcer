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
