<#
.SYNOPSIS
  Register the Claude Switcher native-messaging host with Chrome.

.DESCRIPTION
  Creates the native-messaging manifest JSON and the HKCU registry entry
  Chrome uses to discover it. After this runs, the Claude Switcher Bridge
  extension can call chrome.runtime.connectNative('com.operative.claudeswitcher').

.PARAMETER ExtensionId
  The 32-character ID Chrome assigned to the unpacked Bridge extension.
  Find it on chrome://extensions after loading the unpacked extension from
  the chrome-extension/ folder of this repo.

.PARAMETER HostExePath
  Absolute path to claude-switcher-host.exe. Defaults to the Debug build
  output. Pass an explicit path if you've published the binaries elsewhere.

.EXAMPLE
  .\Register-NativeHost.ps1 -ExtensionId aaaabbbbccccddddeeee...
#>

[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [ValidatePattern('^[a-p]{32}$')]
  [string]$ExtensionId,

  [string]$HostExePath
)

$ErrorActionPreference = 'Stop'
$HostName = 'com.operative.claudeswitcher'

# 1. Resolve the host exe path.
if (-not $HostExePath) {
  $repoRoot = Split-Path -Parent $PSScriptRoot
  $HostExePath = Join-Path $repoRoot 'src\ClaudeSwitcher.NativeHost\bin\Debug\net9.0-windows\claude-switcher-host.exe'
}
$HostExePath = (Resolve-Path -LiteralPath $HostExePath).Path
if (-not (Test-Path -LiteralPath $HostExePath)) {
  throw "Host exe not found: $HostExePath. Run 'dotnet build' first or pass -HostExePath."
}

# 2. Write the native-messaging manifest.
$manifestDir  = Join-Path $env:LOCALAPPDATA 'ClaudeSwitcher'
$manifestPath = Join-Path $manifestDir "$HostName.json"
New-Item -ItemType Directory -Path $manifestDir -Force | Out-Null

$manifest = [ordered]@{
  name              = $HostName
  description       = 'Claude Switcher desktop-app bridge'
  path              = $HostExePath
  type              = 'stdio'
  allowed_origins   = @("chrome-extension://$ExtensionId/")
}
$json = $manifest | ConvertTo-Json -Depth 4
# Native-messaging manifests must be UTF-8 WITHOUT a BOM.
[System.IO.File]::WriteAllText($manifestPath, $json, (New-Object System.Text.UTF8Encoding $false))

# 3. Point Chrome at it via the per-user registry key.
$regBase = 'HKCU:\Software\Google\Chrome\NativeMessagingHosts'
New-Item -Path $regBase -Force | Out-Null
$regKey = Join-Path $regBase $HostName
New-Item -Path $regKey -Force | Out-Null
Set-ItemProperty -Path $regKey -Name '(default)' -Value $manifestPath

Write-Host "Registered $HostName" -ForegroundColor Green
Write-Host "  manifest: $manifestPath"
Write-Host "  host exe: $HostExePath"
Write-Host "  allowed:  chrome-extension://$ExtensionId/"
Write-Host ""
Write-Host "Reload the Bridge extension on chrome://extensions/ and the popup should report 'Connected to desktop app'."
