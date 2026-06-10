$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$rimWorldRoot = if ($env:RIMWORLD_ROOT) { $env:RIMWORLD_ROOT } else { "C:\GOG Games\RimWorld" }
$managed = Join-Path $rimWorldRoot "RimWorldWin64_Data\Managed"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$out = Join-Path $root "Assemblies\RimTouch.dll"

$harmonyCandidates = @(
  $env:HARMONY_DLL,
  (Join-Path $rimWorldRoot "Mods\1279012058\1.2\Assemblies\0Harmony.dll"),
  (Join-Path $rimWorldRoot "Mods\2009463077\Current\Assemblies\0Harmony.dll"),
  (Join-Path $rimWorldRoot "Mods\2009463077\Assemblies\0Harmony.dll")
) | Where-Object { $_ }

$harmony = $harmonyCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $harmony) {
  throw "Harmony DLL not found. Set HARMONY_DLL or install Harmony under RimWorld Mods."
}

if (-not (Test-Path -LiteralPath $managed)) {
  throw "RimWorld managed directory not found: $managed. Set RIMWORLD_ROOT if needed."
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $out) | Out-Null

& $csc /nologo /target:library /optimize+ /out:$out `
  /reference:"$managed\netstandard.dll" `
  /reference:"$managed\Assembly-CSharp.dll" `
  /reference:"$managed\UnityEngine.dll" `
  /reference:"$managed\UnityEngine.CoreModule.dll" `
  /reference:"$managed\UnityEngine.IMGUIModule.dll" `
  /reference:"$managed\UnityEngine.InputLegacyModule.dll" `
  /reference:"$managed\UnityEngine.TextRenderingModule.dll" `
  /reference:"$harmony" `
  (Join-Path $PSScriptRoot "*.cs")

if ($LASTEXITCODE -ne 0) {
  exit $LASTEXITCODE
}

Write-Host "Built $out"
