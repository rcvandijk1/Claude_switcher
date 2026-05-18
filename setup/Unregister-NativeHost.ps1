<#
.SYNOPSIS
  Remove the Claude Switcher native-messaging host registration.
#>

$ErrorActionPreference = 'Stop'
$HostName = 'com.operative.claudeswitcher'

$regKey = "HKCU:\Software\Google\Chrome\NativeMessagingHosts\$HostName"
if (Test-Path $regKey) {
  Remove-Item $regKey -Force
  Write-Host "Removed registry key: $regKey" -ForegroundColor Yellow
} else {
  Write-Host "No registry key found at $regKey."
}

$manifestPath = Join-Path $env:LOCALAPPDATA "ClaudeSwitcher\$HostName.json"
if (Test-Path $manifestPath) {
  Remove-Item $manifestPath -Force
  Write-Host "Removed manifest: $manifestPath" -ForegroundColor Yellow
} else {
  Write-Host "No manifest at $manifestPath."
}
