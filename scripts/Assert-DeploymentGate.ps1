param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

# 部署门禁只检查“部署骨架是否完整”，不会连接生产环境，也不会读取真实 Secret。
$requiredFiles = @(
    'deploy/k8s/README.md',
    'deploy/k8s/base/kustomization.yaml',
    'deploy/k8s/base/namespace.yaml',
    'deploy/k8s/base/service-account.yaml',
    'deploy/k8s/base/postgres/kustomization.yaml',
    'deploy/k8s/base/postgres/secret.example.yaml',
    'deploy/k8s/base/postgres/service.yaml',
    'deploy/k8s/base/postgres/statefulset.yaml',
    'deploy/k8s/base/redis/kustomization.yaml',
    'deploy/k8s/base/redis/secret.example.yaml',
    'deploy/k8s/base/redis/service.yaml',
    'deploy/k8s/base/redis/statefulset.yaml',
    'deploy/k8s/base/rabbitmq/kustomization.yaml',
    'deploy/k8s/base/rabbitmq/secret.example.yaml',
    'deploy/k8s/base/rabbitmq/service.yaml',
    'deploy/k8s/base/rabbitmq/statefulset.yaml',
    'deploy/k8s/base/minio/kustomization.yaml',
    'deploy/k8s/base/minio/secret.example.yaml',
    'deploy/k8s/base/minio/service.yaml',
    'deploy/k8s/base/minio/statefulset.yaml',
    'deploy/k8s/base/observability/kustomization.yaml',
    'deploy/k8s/base/observability/otel-collector.yaml',
    'deploy/k8s/base/observability/prometheus.yaml',
    'deploy/k8s/base/observability/alertmanager.yaml',
    'deploy/k8s/base/observability/grafana.yaml',
    'deploy/k8s/base/observability/loki.yaml',
    'deploy/k8s/base/observability/tempo.yaml',
    '.github/workflows/ci.yml',
    '.github/workflows/deploy-k8s.yml',
    'docs/engineering-standards/部署与CICD规范.md',
    'docs/adr/0003-kubernetes-cicd-platform.md',
    'docs/changes/2026-05-09-kubernetes-cicd-platform.md'
)

$missingFiles = @()
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $Root $file))) {
        $missingFiles += $file
    }
}

if ($missingFiles.Count -gt 0) {
    throw "Deployment gate failed. Missing required files: $($missingFiles -join ', ')"
}

$kustomization = Get-Content -LiteralPath (Join-Path $Root 'deploy/k8s/base/kustomization.yaml') -Raw

# base 入口必须包含项目首版依赖的基础组件。
$requiredKustomizeEntries = @(
    'postgres/',
    'redis/',
    'rabbitmq/',
    'minio/',
    'observability/'
)

$missingKustomizeEntries = @()
foreach ($entry in $requiredKustomizeEntries) {
    if ($kustomization -notlike "*$entry*") {
        $missingKustomizeEntries += $entry
    }
}

if ($missingKustomizeEntries.Count -gt 0) {
    throw "Deployment gate failed. Missing kustomize entries: $($missingKustomizeEntries -join ', ')"
}

$combinedDeployContent = Get-ChildItem -LiteralPath (Join-Path $Root 'deploy/k8s') -Recurse -File |
    Where-Object { $_.Extension -in @('.yaml', '.yml', '.md') } |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw } |
    Out-String

$requiredPhrases = @(
    'PostgreSQL',
    'PostGIS',
    'Redis',
    'RabbitMQ',
    'MinIO',
    'OpenTelemetry Collector',
    'Prometheus',
    'Grafana',
    'Loki',
    'Tempo',
    'Alertmanager',
    'CHANGE_ME'
)

$missingPhrases = @()
foreach ($phrase in $requiredPhrases) {
    if ($combinedDeployContent -notlike "*$phrase*") {
        $missingPhrases += $phrase
    }
}

if ($missingPhrases.Count -gt 0) {
    throw "Deployment gate failed. Missing required deployment phrases: $($missingPhrases -join ', ')"
}

$secretFiles = Get-ChildItem -LiteralPath (Join-Path $Root 'deploy/k8s') -Recurse -File |
    Where-Object { $_.Name -like '*secret*.yaml' -or $_.Name -like '*secret*.yml' }

foreach ($secretFile in $secretFiles) {
    $content = Get-Content -LiteralPath $secretFile.FullName -Raw
    # 仓库里只能放示例 Secret，真实密码必须由环境或密钥系统注入。
    if ($content -notlike '*CHANGE_ME*') {
        throw "Deployment gate failed. Secret example must use CHANGE_ME placeholder: $($secretFile.FullName)"
    }
}

$workflowCi = Get-Content -LiteralPath (Join-Path $Root '.github/workflows/ci.yml') -Raw
if ($workflowCi -notlike '*Assert-ProjectGate.ps1*') {
    throw 'Deployment gate failed. CI workflow must run Assert-ProjectGate.ps1.'
}

$workflowDeploy = Get-Content -LiteralPath (Join-Path $Root '.github/workflows/deploy-k8s.yml') -Raw
if ($workflowDeploy -notlike '*kubectl apply -k*' -or $workflowDeploy -notlike '*KUBE_CONFIG_B64*') {
    throw 'Deployment gate failed. Deploy workflow must use kubectl apply -k and KUBE_CONFIG_B64.'
}

$kubectl = Get-Command kubectl -ErrorAction SilentlyContinue
if ($kubectl) {
    # kubectl kustomize 是离线渲染，不需要本机连接 Kubernetes 集群。
    kubectl kustomize (Join-Path $Root 'deploy/k8s/base') | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Deployment gate failed. kubectl kustomize failed.'
    }
}

Write-Host 'Deployment gate passed.'
