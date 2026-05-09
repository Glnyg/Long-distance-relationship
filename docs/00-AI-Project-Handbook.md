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
- [[engineering-standards/代码生成总规则]]
- [[engineering-standards/服务端工程规范]]
- [[engineering-standards/Android工程规范]]
- [[engineering-standards/AI隐私工程规范]]
- [[engineering-standards/可观测性与预警规范]]
- [[engineering-standards/部署与CICD规范]]
- [[features/ai-privacy-analysis]]
- [[adr/0001-ai-privacy-gateway]]
- [[adr/0002-engineering-standards-observability]]
- [[adr/0003-kubernetes-cicd-platform]]

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
8. [[engineering-standards/代码生成总规则]]：写代码前理解工程规范体系。

## 工程规范体系

本项目把后续代码生成、文档同步、质量门禁、全链路日志、可观测性和预警统一纳入工程规范体系。

工程规范体系不是单个脚本，也不是外部插件。它由中文规范文档、模板、门禁脚本和共享类库组成。

后续 AI 或工程师生成代码前必须确认：

- 是否已阅读对应功能文档。
- 是否已更新设计文档、功能文档、隐私文档、ADR 和变更记录。
- 是否需要接入结构化日志、OpenTelemetry Trace、OpenTelemetry Metric 和 Alert 预警。
- 是否需要新增 Kubernetes 部署配置、CI/CD 配置或基础组件配置。
- 是否已为关键代码、脚本和配置补充新手能看懂的注释。
- 是否确认日志、Trace、Metric 不包含聊天原文、完整经纬度、Wi-Fi 名称、手机号、验证码、token、prompt 或模型输入快照。

## 新手注释注意事项

本项目默认面向初学者维护。后续代码和配置必须保留必要注释，帮助新手理解“为什么这样写”。

必须加注释的地方：

- 关键业务规则，例如授权、情侣绑定、AI 隐私校验、数据留存。
- 隐私边界，例如为什么不能记录 prompt、聊天原文、完整经纬度、Wi-Fi 名称、token。
- 可观测性配置，例如 `correlation_id`、Trace、Metric、Alert 的用途。
- 部署配置，例如 Kubernetes、Secret、持久卷、健康检查、资源限制。
- CI/CD 脚本，例如每个门禁检查什么、失败后应该怎么处理。

注释要求：

- 注释优先使用中文。
- 注释解释意图和风险，不重复代码表面含义。
- 不要给每一行都加机械注释，避免噪音影响维护。
- 修改代码逻辑时，必须同步检查相关注释是否仍然正确。

## 部署规则

项目部署配置位于 `deploy/k8s/`，CI/CD 配置位于 `.github/workflows/`。

首版基础设施包含：

- Kubernetes。
- PostgreSQL + PostGIS。
- Redis。
- RabbitMQ。
- MinIO。
- OpenTelemetry Collector。
- Prometheus。
- Grafana。
- Loki。
- Tempo。
- Alertmanager。

真实 Secret 不允许提交到 Git。仓库内只能保存 `CHANGE_ME` 占位示例。

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

项目总门禁命令：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Assert-ProjectGate.ps1
```
