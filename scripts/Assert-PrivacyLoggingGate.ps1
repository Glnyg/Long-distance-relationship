param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

# 只扫描源码文件，避免 build 输出或文档示例造成误报。
$sourceExtensions = @('.cs', '.kt', '.java')

# 日志调用附近如果同时出现敏感字段名，就要求人工处理，防止隐私原文进入日志。
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
        # 这是保守扫描：宁愿让可疑日志人工确认，也不要把聊天、定位、token 等打出去。
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
