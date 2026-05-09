param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

# 可观测性不是可选项，缺少规范文档或共享类库时直接失败。
$requiredDocs = @(
    'docs/engineering-standards/代码生成总规则.md',
    'docs/engineering-standards/服务端工程规范.md',
    'docs/engineering-standards/Android工程规范.md',
    'docs/engineering-standards/AI隐私工程规范.md',
    'docs/engineering-standards/可观测性与预警规范.md',
    'docs/templates/可观测性检查清单.md',
    'docs/adr/0002-engineering-standards-observability.md'
)

$missingDocs = @()
foreach ($doc in $requiredDocs) {
    if (-not (Test-Path -LiteralPath (Join-Path $Root $doc))) {
        $missingDocs += $doc
    }
}

if ($missingDocs.Count -gt 0) {
    throw "Observability gate failed. Missing documentation files: $($missingDocs -join ', ')"
}

$observabilityProject = Join-Path $Root 'src/AIAPP.Observability/AIAPP.Observability.csproj'
if (-not (Test-Path -LiteralPath $observabilityProject)) {
    throw "Observability gate failed. Missing project: $observabilityProject"
}

$observabilityProjectContent = Get-Content -LiteralPath $observabilityProject -Raw

# 这些包分别负责 OpenTelemetry 托管集成、HTTP 自动埋点和 OTLP 导出。
$requiredPackages = @(
    'OpenTelemetry.Extensions.Hosting',
    'OpenTelemetry.Instrumentation.AspNetCore',
    'OpenTelemetry.Instrumentation.Http',
    'OpenTelemetry.Exporter.OpenTelemetryProtocol'
)

$missingPackages = @()
foreach ($package in $requiredPackages) {
    if ($observabilityProjectContent -notlike "*$package*") {
        $missingPackages += $package
    }
}

if ($missingPackages.Count -gt 0) {
    throw "Observability gate failed. Missing package references: $($missingPackages -join ', ')"
}

$requiredSourceFiles = @(
    'src/AIAPP.Observability/AiAppObservabilityExtensions.cs',
    'src/AIAPP.Observability/AiAppTelemetryNames.cs',
    'src/AIAPP.Observability/TelemetrySanitizer.cs',
    'src/AIAPP.Observability/CorrelationIdMiddleware.cs'
)

$missingSourceFiles = @()
foreach ($sourceFile in $requiredSourceFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $Root $sourceFile))) {
        $missingSourceFiles += $sourceFile
    }
}

if ($missingSourceFiles.Count -gt 0) {
    throw "Observability gate failed. Missing source files: $($missingSourceFiles -join ', ')"
}

$projectFiles = Get-ChildItem -LiteralPath (Join-Path $Root 'src') -Recurse -Filter '*.csproj' |
    Where-Object { $_.FullName -notlike '*\bin\*' -and $_.FullName -notlike '*\obj\*' }

$webProjects = @()
foreach ($projectFile in $projectFiles) {
    $content = Get-Content -LiteralPath $projectFile.FullName -Raw
    if ($content -like '*Microsoft.NET.Sdk.Web*') {
        $webProjects += $projectFile
    }
}

foreach ($webProject in $webProjects) {
    $projectDir = Split-Path -Parent $webProject.FullName
    $programFile = Join-Path $projectDir 'Program.cs'
    if (-not (Test-Path -LiteralPath $programFile)) {
        throw "Observability gate failed. Web project has no Program.cs: $($webProject.FullName)"
    }

    $programContent = Get-Content -LiteralPath $programFile -Raw
    $missingCalls = @()
    # 每个 Web 服务都必须接入统一观测入口、关联 ID 和健康检查。
    if ($programContent -notlike '*AddAiAppObservability*') {
        $missingCalls += 'AddAiAppObservability'
    }

    if ($programContent -notlike '*UseAiAppCorrelationId*') {
        $missingCalls += 'UseAiAppCorrelationId'
    }

    if ($programContent -notlike '*MapHealthChecks*') {
        $missingCalls += 'MapHealthChecks'
    }

    if ($missingCalls.Count -gt 0) {
        throw "Observability gate failed. $($webProject.Name) missing calls: $($missingCalls -join ', ')"
    }
}

Write-Host 'Observability gate passed.'
