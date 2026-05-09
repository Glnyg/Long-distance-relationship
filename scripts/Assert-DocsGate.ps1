param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

# 文档是唯一事实源，所以这里列出项目必须存在的核心文档。
$requiredFiles = @(
    'AGENTS.md',
    'docs/00-AI-Project-Handbook.md',
    'docs/01-Product-Vision.md',
    'docs/02-Architecture.md',
    'docs/03-Permissions-And-Privacy.md',
    'docs/04-Tech-Selection.md',
    'docs/05-Feature-Map.md',
    'docs/06-Android-Client-Design.md',
    'docs/07-Server-Design.md',
    'docs/08-Launch-And-Compliance.md',
    'docs/09-Beginner-Guide.md',
    'docs/engineering-standards/代码生成总规则.md',
    'docs/engineering-standards/服务端工程规范.md',
    'docs/engineering-standards/Android工程规范.md',
    'docs/engineering-standards/AI隐私工程规范.md',
    'docs/engineering-standards/可观测性与预警规范.md',
    'docs/engineering-standards/部署与CICD规范.md',
    'docs/features/account-and-binding.md',
    'docs/features/consent-center.md',
    'docs/features/ai-privacy-analysis.md',
    'docs/features/location-tracking.md',
    'docs/features/device-state-sharing.md',
    'docs/features/chat-and-rtc.md',
    'docs/features/lock-challenge.md',
    'docs/features/notifications.md',
    'docs/adr/0001-ai-privacy-gateway.md',
    'docs/adr/0002-engineering-standards-observability.md',
    'docs/adr/0003-kubernetes-cicd-platform.md',
    'docs/changes/2026-05-09-ai-privacy-gateway.md',
    'docs/changes/2026-05-09-beginner-guide.md',
    'docs/changes/2026-05-09-engineering-standards-observability.md',
    'docs/changes/2026-05-09-kubernetes-cicd-platform.md',
    'docs/templates/功能文档模板.md',
    'docs/templates/AI代码生成任务模板.md',
    'docs/templates/接口文档模板.md',
    'docs/templates/可观测性检查清单.md'
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

# 这些词代表当前项目的关键决策。缺少时通常说明文档没有同步更新。
$requiredPhrases = @(
    '中文为主',
    '文档是唯一事实源',
    'Android 原生 Kotlin',
    '.NET 10',
    '初学者导读',
    '8 条业务主线',
    '模块关系',
    '服务关系',
    '第三方服务',
    '工程规范体系',
    '全链路日志',
    '可观测性',
    '预警',
    'Kubernetes',
    'CI/CD',
    'PostgreSQL',
    'Redis',
    'RabbitMQ',
    'MinIO',
    'OpenTelemetry',
    'correlation_id',
    'Prometheus',
    'Grafana',
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
    # 首版明确不上 MAF，因此任何 Microsoft.Agents.AI 包引用都直接失败。
    if ($content -match 'Microsoft\.Agents\.AI') {
        throw "MAF package reference is not allowed in v1: $($projectFile.FullName)"
    }
}

Write-Host 'Documentation gate passed.'
