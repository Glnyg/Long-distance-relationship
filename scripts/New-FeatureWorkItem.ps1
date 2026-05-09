param(
    [Parameter(Mandatory = $true)]
    [string]$Name,

    [Parameter(Mandatory = $true)]
    [string]$Title,

    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

if ($Name -notmatch '^[a-z0-9]+(-[a-z0-9]+)*$') {
    throw 'Name must use lowercase kebab-case, for example: ai-result-delete.'
}

$date = Get-Date -Format 'yyyy-MM-dd'
$featuresDir = Join-Path $Root 'docs/features'
$changesDir = Join-Path $Root 'docs/changes'
$checklistsDir = Join-Path $Root 'docs/checklists'

New-Item -ItemType Directory -Force -Path $featuresDir, $changesDir, $checklistsDir | Out-Null

$featurePath = Join-Path $featuresDir "$Name.md"
$changePath = Join-Path $changesDir "$date-$Name.md"
$checklistPath = Join-Path $checklistsDir "$date-$Name-可观测性检查清单.md"

foreach ($path in @($featurePath, $changePath, $checklistPath)) {
    if (Test-Path -LiteralPath $path) {
        throw "File already exists: $path"
    }
}

$featureContent = @"
---
status: draft
owner: product
last_changed: $date
---

# $Title

## 用户怎么用

说明用户在 App 里怎样使用这个功能。

## 系统怎么处理

说明 Android 客户端、服务端、数据库、第三方服务分别做什么。

## 涉及什么数据

列出账号、情侣关系、聊天、定位轨迹、用机状态、AI、推送等数据。

## 权限和授权

说明是否需要双方授权，用户拒绝或撤回后如何降级。

## 失败时怎么办

说明网络失败、权限失败、服务端失败、第三方失败时的处理方式。

## 日志、Trace、Metric、Alert

说明这个功能怎样接入可观测性，必须避免隐私原文进入日志。

## 测试计划

列出成功路径、失败路径、隐私路径、撤回授权路径。
"@

$changeContent = @"
---
date: $date
type: feature
---

# $date $Title

## 变更内容

- 新增或调整：$Title。

## 原因

说明为什么需要这个变更。

## 影响

说明对 Android、服务端、隐私、上架审核、可观测性的影响。
"@

$templatePath = Join-Path $Root 'docs/templates/可观测性检查清单.md'
if (-not (Test-Path -LiteralPath $templatePath)) {
    throw "Missing template: $templatePath"
}

Set-Content -LiteralPath $featurePath -Value $featureContent -Encoding UTF8
Set-Content -LiteralPath $changePath -Value $changeContent -Encoding UTF8
Copy-Item -LiteralPath $templatePath -Destination $checklistPath

Write-Host "Created feature work item:"
Write-Host " - $featurePath"
Write-Host " - $changePath"
Write-Host " - $checklistPath"
