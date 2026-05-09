param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [switch]$SkipDotnet
)

$ErrorActionPreference = 'Stop'

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

Invoke-GateScript -ScriptName 'Assert-DocsGate.ps1'
Invoke-GateScript -ScriptName 'Assert-PrivacyLoggingGate.ps1'
Invoke-GateScript -ScriptName 'Assert-ObservabilityGate.ps1'
Invoke-GateScript -ScriptName 'Assert-DeploymentGate.ps1'

if (-not $SkipDotnet) {
    dotnet build (Join-Path $Root 'AIAPP.slnx') -c Release -warnaserror
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet build failed.'
    }

    dotnet test (Join-Path $Root 'AIAPP.slnx') -c Release --no-build
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet test failed.'
    }
}

Write-Host 'Project gate passed.'
