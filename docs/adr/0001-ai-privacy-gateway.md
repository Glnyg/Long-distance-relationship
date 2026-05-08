---
status: accepted
date: 2026-05-09
---

# ADR 0001：AI 隐私网关

## 决策

产品 AI 首版使用 `Microsoft.Extensions.AI + IChatClient`。v1 不使用 MAF。

所有涉及聊天、定位轨迹、设备状态的 AI 处理，必须通过 `AiPrivacyGateway`。

## 理由

首版需要的是“输入提示词、输出强类型结果”的简单模式，同时需要严格隐私控制。首版不需要 Agent loop、自主工具、多 agent 协作或长期记忆。

`IChatClient` 可以隔离国内云模型供应商，避免业务代码直接绑定具体模型 SDK。

## 影响

- 编排复杂度较低。
- 审计和测试更容易。
- 后续如需 MAF，必须新增 ADR。
- 后续如需工具调用或 Agent 工作流，必须先更新隐私和授权文档。
