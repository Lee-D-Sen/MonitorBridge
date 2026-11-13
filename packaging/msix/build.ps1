<#
  Builds an MSIX from dist\MonitorBridge\MonitorBridge.exe.
  -Dev   : placeholder identity + self-signed dev certificate (local test only; the Store re-signs the real package).
  Store  : pass the identity values from Partner Center (-IdentityName, -Publisher, -PublisherDisplay) and omit -Dev;
           the resulting unsigned .msix is what you upload to Partner Center.
#>
param(
  [string]$Version = "0.3.0.0",
  [string]$IdentityName = "MonitorBridge.Dev",
  [string]$Publisher = "CN=MonitorBridgeDev",
  [string]$PublisherDisplay = "Lee-D-Sen",
  [string]$DisplayName = "MonitorBridge $([char]0x2013) Cursor Fix",
  [string]$ToolsDir = "",                       # folder containing makeappx.exe / signtool.exe
  [string]$OutDir = "$PSScriptRoot\out",
  [switch]$Dev
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot\..\.."
$exe = Join-Path $root "dist\MonitorBridge\MonitorBridge.exe"
if (-not (Test-Path $exe)) { throw "Publish first: $exe not found" }
if (-not $ToolsDir) { $ToolsDir = Split-Path (Get-Command makeappx.exe -ErrorAction Stop).Source }
$stage = Join-Path $OutDir "stage"
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory "$stage\Assets" -Force | Out-Null
Copy-Item $exe $stage
Copy-Item "$PSScriptRoot\Assets\*.png" "$stage\Assets"
$m = (Get-Content "$PSScriptRoot\AppxManifest.xml" -Raw).Replace('@IDENTITY_NAME@', $IdentityName).Replace('@PUBLISHER@', $Publisher).Replace('@VERSION@', $Version).Replace('@PUBLISHER_DISPLAY@', $PublisherDisplay).Replace('@DISPLAY_NAME@', $DisplayName)
[IO.File]::WriteAllText("$stage\AppxManifest.xml", $m, (New-Object Text.UTF8Encoding($false)))
$msix = Join-Path $OutDir "MonitorBridge_$Version.msix"
Remove-Item $msix -ErrorAction SilentlyContinue
& "$ToolsDir\makeappx.exe" pack /d $stage /p $msix /o
if ($LASTEXITCODE) { throw "makeappx failed" }
if ($Dev) {
  $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object Subject -eq $Publisher | Select-Object -First 1
  if (-not $cert) {
    $cert = New-SelfSignedCertificate -Type Custom -Subject $Publisher -KeyUsage DigitalSignature -FriendlyName "MonitorBridge dev (local test only)" `
      -CertStoreLocation Cert:\CurrentUser\My -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
  }
  & "$ToolsDir\signtool.exe" sign /fd SHA256 /sha1 $cert.Thumbprint $msix
  if ($LASTEXITCODE) { throw "signtool failed" }
  Export-Certificate -Cert $cert -FilePath (Join-Path $OutDir "MonitorBridgeDev.cer") | Out-Null
}
Write-Host "Built: $msix"
