---
status: active
owner: ai
last_changed: 2026-05-09
related_adr:
  - ../adr/0001-ai-privacy-gateway.md
  - ../adr/0002-engineering-standards-observability.md
---

# AI隐私工程规范

## 这份文档是什么

这份文档规定 AI 相关代码应该怎样生成和维护。

初学者可以这样理解：AI 可以帮助用户分析聊天、轨迹和用机状态，但这些数据非常敏感，所以 AI 不能自由读取数据，必须经过统一的隐私网关。

## 固定技术路线

首版 AI 使用：

- `Microsoft.Extensions.AI`。
- `IChatClient`。
- 国内云模型 provider 适配器。
- `AiPrivacyGateway`。

首版不上 MAF。

首版禁止：

- Agent loop。
- 工具自主调用。
- 多 agent。
- 长期记忆。
- 自动关系画像打分。
- 未经双方同意的单方分析。
- 后台持续监控。

## 唯一入口规则

所有涉及情侣聊天、定位轨迹、用机状态的 AI 分析，都必须经过 `AiPrivacyGateway`。

业务服务不得直接调用：

- 模型 SDK。
- 模型 HTTP API。
- 任何绕过授权的数据读取接口。

如果业务代码需要 AI 分析，只能调用受控入口：

```csharp
AiPrivacyGateway.AnalyzeAsync(...)
```

## 授权规则

每次 AI 分析必须校验：

- 双方绑定有效。
- 请求人属于这段情侣关系。
- 双方均开启 AI 分析授权。
- 双方均授权本次请求的数据类型。
- 时间范围合法。

数据类型必须单独授权：

- 聊天。
- 定位轨迹。
- 用机状态。

默认分析最近 7 天，最长 30 天。

## 输入不落库规则

AI 输入上下文只允许存在于一次请求内存中。

禁止保存：

- prompt 原文。
- 模型输入快照。
- 聊天原文输入包。
- 精确定位点输入包。
- 用机明细输入包。

允许保存：

- AI 输出结果。
- 结果过期时间。
- 双方可见用户 ID。
- 审计元数据。
- token 用量。
- 模型 ID。

AI 输出结果保留 30 天，双方均可删除。

## Trace 与日志规则

AI 分析必须记录可排查的链路，但不能暴露隐私。

Trace 允许记录：

- `data_type`。
- `time_range_days`。
- `consent_version`。
- `model_id`。
- `result`。
- `error_code`。
- `duration_ms`。
- 哈希后的 `user_id_hash`、`couple_id_hash`。

Trace 禁止记录：

- prompt。
- 聊天原文。
- 轨迹明细。
- 完整经纬度。
- Wi-Fi 名称。
- 用机明细。

## 失败降级规则

AI 失败时必须返回可解释状态。

必须覆盖：

- 绑定无效。
- 请求人不属于情侣关系。
- 授权缺失。
- 时间范围非法。
- 模型不可用。
- 模型返回格式错误。
- 模型输出不安全。

不能把模型异常原文直接展示给用户。

## 测试规则

AI 测试必须覆盖：

- 任一方未授权时分析失败。
- 未授权的数据类型不能进入 prompt。
- prompt 不落库。
- AI 结果 30 天过期清理。
- 审计日志不包含隐私原文。
- Trace 不包含隐私原文。
- 模型超时、格式错误、不安全输出时返回可解释失败。
