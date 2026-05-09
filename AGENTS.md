# AGENTS.md

## 适用范围

本文件适用于整个仓库。任何 AI 代理、代码生成代理、自动化脚本维护者进入本项目后，都必须先阅读本文件。

## 最重要规则

本项目的事实源是 Obsidian 中文文档，不是代码。

修改功能、接口、权限、隐私、AI、部署、日志、可观测性、预警前，必须先阅读：

- `docs/00-AI-Project-Handbook.md`
- `docs/09-Beginner-Guide.md`
- `docs/engineering-standards/代码生成总规则.md`
- `docs/engineering-standards/服务端工程规范.md`
- `docs/engineering-standards/Android工程规范.md`
- `docs/engineering-standards/AI隐私工程规范.md`
- `docs/engineering-standards/可观测性与预警规范.md`
- `docs/engineering-standards/部署与CICD规范.md`

如果文档和代码冲突，以文档为准。正确做法是先更新文档、ADR、变更记录，再改代码。

## 语言规则

- 和用户交流默认使用中文。
- 文档、注释、功能说明、错误解释默认使用中文。
- 代码名、包名、API 路径、命令、第三方库名、官方术语可以保留英文。
- 英文术语第一次出现时，尽量补中文解释。

## 新手注释规则

代码和配置必须保留必要注释，方便初学者理解。

必须加注释的地方：

- 授权、情侣绑定、AI 隐私校验、数据留存等关键业务规则。
- 隐私边界，例如为什么不能记录 prompt、聊天原文、完整经纬度、Wi-Fi 名称、token。
- 可观测性配置，例如 `correlation_id`、Trace、Metric、Alert。
- 部署配置，例如 Kubernetes、Secret、持久卷、健康检查、资源限制。
- CI/CD 和 PowerShell 门禁脚本中的关键检查逻辑。

注释要解释“为什么这样做”和“误改有什么风险”，不要只重复代码表面含义。不要每一行都机械注释。改代码或配置时必须同步检查注释是否仍然正确。

## 隐私和 AI 硬规则

本项目会处理情侣聊天、定位轨迹、用机状态等敏感数据。

必须遵守：

- AI 分析只能由用户主动触发。
- 双方单独同意是硬性前置条件。
- 每类数据必须单独授权。
- AI 只能通过 `AiPrivacyGateway` 调用。
- AI 使用 `Microsoft.Extensions.AI + IChatClient`。
- 首版不上 MAF，不使用 `Microsoft.Agents.AI`。
- 不做 Agent loop、工具自主调用、多 agent、长期记忆、自动关系画像打分。
- prompt 原文、模型输入快照、聊天原文、精确定位点、Wi-Fi 名称、token、验证码不得进入日志、Trace、Metric、审计或数据库。

## 可观测性规则

服务端必须使用 `AIAPP.Observability` 统一接入：

- `ILogger` 结构化日志。
- OpenTelemetry Trace。
- OpenTelemetry Metric。
- OTLP exporter。
- `correlation_id`。
- 健康检查。

排障字段只能使用元数据和脱敏标识，例如错误码、耗时、数据类型、授权版本、模型 ID、哈希后的用户 ID 和情侣 ID。

## 部署和 CI/CD 规则

部署配置位于 `deploy/k8s/`，CI/CD 配置位于 `.github/workflows/`。

基础组件包括：

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

真实 Secret 不得提交到 Git。仓库里的 `secret.example.yaml` 只能使用 `CHANGE_ME` 占位值。

## 修改代码前检查

开始修改前确认：

- 当前工作区是否已有用户未提交改动。
- 变更是否需要同步设计文档、功能文档、隐私文档、ADR、变更记录。
- 是否涉及敏感数据、授权、AI、日志、Trace、Metric、部署配置。
- 是否需要新增或更新测试。
- 是否需要补充新手注释。

不得擅自回滚用户已有改动。

## 验证命令

提交前必须运行项目总门禁：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Assert-ProjectGate.ps1
```

总门禁包括：

- 文档门禁。
- 隐私日志门禁。
- 可观测性门禁。
- 部署门禁。
- .NET Release 构建。
- .NET 测试。

如果门禁失败，修复问题，不要绕过。

## 提交和推送

只有用户明确要求“提交”“推送”“合回”时，才执行 git commit、push、merge。

提交前必须确认：

- `scripts/Assert-ProjectGate.ps1` 已通过。
- 没有提交真实 Secret。
- 没有提交隐私原文。
- 没有把无关文件混入提交。
