---
status: active
owner: project
last_changed: 2026-05-09
related_adr:
  - adr/0001-ai-privacy-gateway.md
---

# AI必读项目手册

## 最高规则

本项目以 Obsidian 文档为唯一事实源。设计文档、功能文档、隐私文档和 ADR 与代码冲突时，以文档为准；工程实现必须修正代码或先提交文档变更。

任何 AI 或工程师在修改功能前必须先阅读：

- [[01-Product-Vision]]
- [[02-Architecture]]
- [[03-Permissions-And-Privacy]]
- [[04-Tech-Selection]]
- [[05-Feature-Map]]
- [[06-Android-Client-Design]]
- [[07-Server-Design]]
- [[08-Launch-And-Compliance]]
- [[09-Beginner-Guide]]
- [[features/ai-privacy-analysis]]
- [[adr/0001-ai-privacy-gateway]]

## 语言规则

本项目主要面向中文使用者和中文维护者。和用户沟通、项目说明、设计文档、功能文档、变更记录必须优先使用中文。

允许保留英文的地方：

- 代码标识符
- 包名
- API 路径
- 第三方库名
- 命令行命令
- 官方术语原名

如果文档里必须出现英文术语，应尽量加中文解释。

## 初学者阅读顺序

如果你刚接触这个项目，按下面顺序读：

1. [[09-Beginner-Guide]]：先理解项目、目录、技术栈和常见词。
2. [[01-Product-Vision]]：理解这个 App 要解决什么问题、不做什么。
3. [[05-Feature-Map]]：理解首版有哪些功能，功能之间有什么依赖。
4. [[03-Permissions-And-Privacy]]：理解为什么权限和隐私是本项目的核心。
5. [[02-Architecture]]：理解客户端、服务端、数据库、AI、第三方服务怎样协作。
6. [[04-Tech-Selection]]：理解为什么选这些技术，为什么暂时不用某些技术。
7. 具体功能文档：改哪个功能就读 `docs/features/` 下对应文件。

## AI 技术边界

首版 AI 是产品能力，会在用户主动触发时处理情侣聊天、定位轨迹和用机状态等敏感个人信息。

首版技术栈固定为：

- `Microsoft.Extensions.AI`
- `IChatClient`
- 国内云模型 provider 适配器

明确决策：首版不上 MAF。

首版禁止：

- MAF / `Microsoft.Agents.AI`
- Agent loop
- 工具自主调用
- 多 agent
- 长期记忆
- 自动关系画像打分
- 未经双方同意的单方分析

## 隐私硬约束

- AI 分析必须由用户主动触发。
- 分析双方数据前，双方单独同意是硬性前置条件。
- 每类数据必须单独授权：聊天、定位轨迹、用机状态。
- 单次分析默认最近 7 天，最长 30 天。
- 输入不落库。输入上下文只在请求内存中使用，不保存 prompt 原文，不保存模型输入快照。
- AI 输出结果双方共同可见，保留 30 天，双方均可删除。
- 审计日志只保存元数据，不保存聊天原文、定位点明细、Wi-Fi 名称、用机明细。

## 变更门禁

任何涉及 AI 数据范围、授权规则、留存时间、模型供应商、提示词策略、输出可见性的变更，必须同步更新：

- [[02-Architecture]]
- [[03-Permissions-And-Privacy]]
- [[features/ai-privacy-analysis]]
- `docs/adr/`
- `docs/changes/`

任何涉及账号、绑定、定位、聊天、音视频、设备状态、锁屏、推送、上架权限的变更，必须同步更新：

- [[04-Tech-Selection]]
- [[05-Feature-Map]]
- 对应的 `docs/features/` 功能文档
- 对应的 ADR 和变更记录

本地检查命令：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Assert-DocsGate.ps1
```
