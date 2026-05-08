---
status: active
owner: product
last_changed: 2026-05-09
permissions:
  - chat_ai_consent
  - location_ai_consent
  - device_state_ai_consent
api_surface:
  - AiPrivacyGateway.AnalyzeAsync
  - AiPrivacyGateway.DeleteExpiredResultsAsync
test_status: covered
---

# AI 隐私分析

## 目标

帮助已绑定情侣在双方单独同意后，主动发起一次共同 AI 分析。AI 可以使用最近聊天、定位轨迹和设备状态，但必须遵守最小必要、输入不落库、结果双方共同可见。

## 用户流程

1. 双方分别开启 AI 分析授权。
2. 双方分别选择允许 AI 使用的数据类型。
3. 一方进入 AI 分析页面，选择数据类型和时间范围。
4. App 明确提示“结果将双方共同可见”。
5. 用户主动开始分析。
6. 分析完成后，结果进入情侣共同 AI 记录。

## 规则

- 默认范围：最近 7 天。
- 最大范围：30 天。
- 结果双方共同可见。
- 结果 30 天后过期。
- 双方均可删除共享结果。
- 任一方撤回授权后，禁止新的 AI 分析。
- 首版不上 MAF。

## 失败行为

- 缺少授权：加载隐私数据前失败。
- 时间范围无效：加载隐私数据前失败。
- 模型不可用：返回可解释失败。
- 模型输出格式错误：返回可解释失败，不保存结果。
- 模型输出不安全：返回可解释失败，不保存结果。

## 实现

已在 `src/AIAPP.AiPrivacy` 实现核心规则。

网关使用 `Microsoft.Extensions.AI.IChatClient`，通过 `AiPrivacyGatewayOptions` 配置 `Temperature`、`MaxOutputTokens`、`ModelId`、超时和重试。

代码不得引用 MAF 或 `Microsoft.Agents.AI`。首版不上 MAF。
