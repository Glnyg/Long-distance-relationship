---
date: 2026-05-09
type: platform
---

# 2026-05-09 Kubernetes、CI/CD 与基础组件部署

## 变更内容

- 新增 Kubernetes 基础设施目录 `deploy/k8s/`。
- 新增 PostgreSQL + PostGIS、Redis、RabbitMQ、MinIO 部署清单。
- 新增 OpenTelemetry Collector、Prometheus、Grafana、Loki、Tempo、Alertmanager 部署清单。
- 新增 GitHub Actions CI 工作流。
- 新增 GitHub Actions Kubernetes 手动部署工作流。
- 新增部署与 CI/CD 工程规范、ADR 和部署门禁。

## 原因

项目需要从早期就具备可部署、可检查、可观测、可预警的基础设施框架，避免服务端实现完成后再补运维体系。

## 影响

- 后续新增服务必须补 Kubernetes 部署清单。
- 后续新增基础组件必须同步部署文档、工程规范、ADR、变更记录和门禁脚本。
- CI 将通过项目总门禁持续验证部署配置存在性和安全约束。
