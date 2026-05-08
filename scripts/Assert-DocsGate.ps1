param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

$requiredFiles = @(
    'docs/00-AI-Project-Handbook.md',
    'docs/01-Product-Vision.md',
    'docs/02-Architecture.md',
    'docs/03-Permissions-And-Privacy.md',
    'docs/04-Tech-Selection.md',
    'docs/05-Feature-Map.md',
    'docs/06-Android-Client-Design.md',
    'docs/07-Server-Design.md',
    'docs/08-Launch-And-Compliance.md',
    'docs/features/account-and-binding.md',
    'docs/features/consent-center.md',
    'docs/features/ai-privacy-analysis.md',
    'docs/features/location-tracking.md',
    'docs/features/device-state-sharing.md',
    'docs/features/chat-and-rtc.md',
    'docs/features/lock-challenge.md',
    'docs/features/notifications.md',
    'docs/adr/0001-ai-privacy-gateway.md',
    'docs/changes/2026-05-09-ai-privacy-gateway.md',
    'docs/templates/功能文档模板.md'
)

$missing = @()
foreach ($file in $requiredFiles) {
    $path = Join-Path $Root $file
    if (-not (Test-Path -LiteralPath $path)) {
        $missing += $file
    }
}

if ($missing.Count -gt 0) {
    throw "Missing required documentation files: $($missing -join ', ')"
}

$combinedDocs = $requiredFiles |
    ForEach-Object { Get-Content -LiteralPath (Join-Path $Root $_) -Raw } |
    Out-String

$requiredPhrases = @(
    '中文为主',
    '文档是唯一事实源',
    'Android 原生 Kotlin',
    '.NET 10',
    'Microsoft.Extensions.AI',
    'IChatClient',
    '不上 MAF',
    '双方单独同意',
    '输入不落库',
    '30 天',
    'OPPO',
    '设备管理员',
    '后台定位'
)

$missingPhrases = @()
foreach ($phrase in $requiredPhrases) {
    if ($combinedDocs -notlike "*$phrase*") {
        $missingPhrases += $phrase
    }
}

if ($missingPhrases.Count -gt 0) {
    throw "Documentation gate failed. Missing required phrases: $($missingPhrases -join ', ')"
}

$projectFiles = Get-ChildItem -LiteralPath $Root -Recurse -Filter '*.csproj' |
    Where-Object { $_.FullName -notlike '*\bin\*' -and $_.FullName -notlike '*\obj\*' }

foreach ($projectFile in $projectFiles) {
    $content = Get-Content -LiteralPath $projectFile.FullName -Raw
    if ($content -match 'Microsoft\.Agents\.AI') {
        throw "MAF package reference is not allowed in v1: $($projectFile.FullName)"
    }
}

Write-Host 'Documentation gate passed.'
