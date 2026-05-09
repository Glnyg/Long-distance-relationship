param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [switch]$SkipDotnet
)

$ErrorActionPreference = 'Stop'

# 项目总门禁：本地和 CI 都调用这个脚本，避免“我机器上能跑”和 CI 规则不一致。
function Invoke-GateScript {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScriptName
    )

    $scriptPath = Join-Path $PSScriptRoot $ScriptName
    if (-not (Test-Path -LiteralPath $scriptPath)) {
        throw "Gate script not found: $scriptPath"
    }

    & $scriptPath -Root $Root
}

# 先检查文档和工程规范，再检查代码构建测试。这样失败时更容易定位是哪类问题。
Invoke-GateScript -ScriptName 'Assert-DocsGate.ps1'
Invoke-GateScript -ScriptName 'Assert-PrivacyLoggingGate.ps1'
Invoke-GateScript -ScriptName 'Assert-ObservabilityGate.ps1'
Invoke-GateScript -ScriptName 'Assert-DeploymentGate.ps1'

if (-not $SkipDotnet) {
    # Release + warnaserror 用来尽早发现生产构建才会暴露的问题。
    dotnet build (Join-Path $Root 'AIAPP.slnx') -c Release -warnaserror
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet build failed.'
    }

    # --no-build 表示复用上一步构建产物，保证测试运行的是刚刚通过编译的代码。
    dotnet test (Join-Path $Root 'AIAPP.slnx') -c Release --no-build
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet test failed.'
    }
}

Write-Host 'Project gate passed.'
