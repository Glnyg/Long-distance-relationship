# AIAPP

异地恋 App 项目工作区。

## 目录结构

- `src/`：应用源码。
- `tests/`：自动化测试。
- `docs/`：Obsidian 项目文档，中文为主，是唯一事实源。
- `scripts/`：本地开发和维护脚本。
- `config/`：配置模板和默认配置。
- `data/raw/`：本地原始数据，默认不入库。
- `data/processed/`：本地处理后数据，默认不入库。

## 开始使用

构建和测试当前 .NET AI 隐私网关：

```powershell
dotnet build AIAPP.slnx -c Release -warnaserror
dotnet test AIAPP.slnx -c Release --no-build
powershell -ExecutionPolicy Bypass -File scripts/Assert-DocsGate.ps1
```

修改任何产品行为前，必须先阅读 `docs/00-AI-Project-Handbook.md`。如果你是初学者，先读 `docs/09-Beginner-Guide.md`。项目文档是唯一事实源，代码必须服从文档。

## 工程规范体系

后续 AI/人工代码生成统一遵守 `docs/engineering-standards/`。项目要求完整的全链路日志、可观测性和预警，服务端默认使用 OpenTelemetry，排障通过 `correlation_id` 串起链路。

一键门禁：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Assert-ProjectGate.ps1
```

## 部署

Kubernetes 部署配置在 `deploy/k8s/`，CI/CD 配置在 `.github/workflows/`。

当前基础设施包含 PostgreSQL + PostGIS、Redis、RabbitMQ、MinIO、OpenTelemetry Collector、Prometheus、Grafana、Loki、Tempo、Alertmanager。

本地只检查清单和门禁：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Assert-DeploymentGate.ps1
```

有 `kubectl` 时可额外验证：

```powershell
kubectl kustomize deploy/k8s/base
```
