---
status: accepted
date: 2026-05-09
---

# ADR 0003：Kubernetes、CI/CD 与基础组件部署

## 背景

项目后续需要服务端、数据库、缓存、消息队列、对象存储、可观测性和预警能力。如果没有部署配置，后续服务实现后无法形成完整运行环境，也无法在 CI/CD 中持续验证基础设施变更。

## 决策

首版新增 Kubernetes 和 CI/CD 基础配置。

决策内容：

- 部署配置放在 `deploy/k8s/`。
- 使用纯 Kubernetes YAML + kustomize。
- 首版不引入 Helm。
- CI 使用 GitHub Actions。
- CD 使用手动触发的 GitHub Actions 工作流。
- 基础组件包含 PostgreSQL + PostGIS、Redis、RabbitMQ、MinIO、OpenTelemetry Collector、Prometheus、Grafana、Loki、Tempo、Alertmanager。
- Secret 只提交示例占位值，真实密钥由环境变量、GitHub Secrets 或集群密钥系统管理。

## 不做什么

- 不自动部署生产环境。
- 不提交真实 Secret。
- 不创建不存在的业务服务镜像部署。
- 不把云厂商专有部署方式写死进项目。

## 后果

好处：

- 新环境可以从统一入口部署基础设施。
- CI 可以持续检查文档、代码和部署配置。
- 后续服务新增部署时有固定结构可参考。

代价：

- 自建 PostgreSQL、Redis、RabbitMQ、MinIO 需要额外运维能力。
- 生产环境可能仍应优先使用云厂商托管数据库、缓存和对象存储。

## 验证

项目总门禁：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Assert-ProjectGate.ps1
```

有 `kubectl` 时可额外验证：

```powershell
kubectl kustomize deploy/k8s/base
```
