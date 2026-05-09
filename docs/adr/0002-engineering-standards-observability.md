---
status: accepted
date: 2026-05-09
---

# ADR 0002：工程规范体系与全链路可观测性

## 背景

本项目会处理情侣聊天、定位轨迹、用机状态、AI 分析等敏感数据。后续会有 AI 和人工共同参与代码生成，如果缺少统一规范，容易出现文档不同步、日志泄露隐私、排障困难、预警缺失等问题。

## 决策

建立工程规范体系，作为后续代码生成和质量门禁的基础。

决策内容：

- 正式名称使用“工程规范体系”，不使用泛称 `tools`。
- 文档位于 `docs/engineering-standards/`。
- 模板位于 `docs/templates/`。
- 门禁脚本位于 `scripts/`。
- 服务端统一使用 OpenTelemetry。
- 默认可观测性出口使用 OTLP。
- 默认本地和云端汇聚使用 OpenTelemetry Collector。
- 默认可观测性组合为 Prometheus、Grafana、Loki、Tempo、Alertmanager。
- 服务端共享入口为 `AIAPP.Observability`。

## 不做什么

首版不绑定商业 APM。

首版不把工程规范体系做成独立 MCP 或插件。

首版不允许为了排障记录隐私原文。

## 后果

好处：

- 后续 AI 生成代码有统一约束。
- 文档、代码、测试、可观测性保持同步。
- 排查问题时可以通过 `correlation_id` 和 Trace 串起链路。
- 隐私日志风险可以被脚本提前拦截。

代价：

- 新功能需要额外补充文档和可观测性说明。
- 新服务必须接入统一可观测性基础设施。

## 验证

本地总门禁：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Assert-ProjectGate.ps1
```
