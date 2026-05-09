param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

$sourceExtensions = @('.cs', '.kt', '.java')
$loggingPattern = '(?i)(\bLog(Trace|Debug|Information|Warning|Error|Critical)\s*\(|\blogger\s*\.|Log\s*\.)'
$sensitivePattern = '(?i)(chat\s*text|message\.Text|PrivateChatMessage|Latitude|Longitude|ActiveNetworkName|Wi-?Fi|Wifi|phone|手机号|验证码|VerificationCode|token|access_token|refresh_token|prompt|ChatMessages|LocationPoint|DeviceState|模型输入|聊天原文|完整经纬度)'

$files = Get-ChildItem -LiteralPath $Root -Recurse -File |
    Where-Object {
        $sourceExtensions -contains $_.Extension -and
        $_.FullName -notlike '*\bin\*' -and
        $_.FullName -notlike '*\obj\*' -and
        $_.FullName -notlike '*\.git\*'
    }

$violations = @()

foreach ($file in $files) {
    $lines = Get-Content -LiteralPath $file.FullName
    for ($index = 0; $index -lt $lines.Count; $index++) {
        $line = $lines[$index]
        if ($line -match $loggingPattern -and $line -match $sensitivePattern) {
            $relativePath = [System.IO.Path]::GetRelativePath($Root, $file.FullName)
            $violations += "${relativePath}:$($index + 1): $line"
        }
    }
}

if ($violations.Count -gt 0) {
    throw "Privacy logging gate failed. Sensitive data appears in logging statements:`n$($violations -join "`n")"
}

Write-Host 'Privacy logging gate passed.'
