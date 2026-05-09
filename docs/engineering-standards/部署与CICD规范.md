---
status: active
owner: platform
last_changed: 2026-05-09
related_adr:
  - ../adr/0003-kubernetes-cicd-platform.md
---

# 部署与CICD规范

## 这份文档是什么

这份文档规定项目怎样部署到 Kubernetes，以及 CI/CD 应该怎样检查和发布。

初学者可以这样理解：

- Kubernetes：负责在服务器集群里运行服务和基础组件。
- CI：代码提交后自动检查，例如构建、测试、文档门禁。
- CD：检查通过后，把配置或镜像发布到运行环境。
- kustomize：Kubernetes 自带的配置组合方式，用来按目录部署一组清单。

## 首版部署内容

首版 Kubernetes 基础设施包含：

- Namespace。
- ServiceAccount。
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

业务服务镜像还没有实现时，不创建假的业务 `Deployment`。后续每个服务完成后再单独新增部署清单。

## 目录规则

部署配置放在：

```text
deploy/k8s/
```

基础环境入口：

```text
deploy/k8s/base/kustomization.yaml
```

不要把真实 Secret 提交到 Git。仓库中的 `secret.example.yaml` 只能放占位值。

## CI 规则

CI 使用 GitHub Actions。

入口文件：

```text
.github/workflows/ci.yml
```

CI 必须运行：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Assert-ProjectGate.ps1
```

这个命令会检查：

- 文档门禁。
- 隐私日志门禁。
- 可观测性门禁。
- 部署门禁。
- .NET Release 构建。
- .NET 测试。

## CD 规则

Kubernetes 部署使用手动触发工作流：

```text
.github/workflows/deploy-k8s.yml
```

原因：

- 首版没有生产发布流水线经验，不做自动生产部署。
- Kubernetes 访问凭据属于高敏感信息，需要人工确认环境。
- 上架前不同环境可能使用不同云厂商资源。

部署工作流需要 GitHub environment secret：

- `KUBE_CONFIG_B64`：base64 后的 kubeconfig。

## 基础组件说明

PostgreSQL + PostGIS：

- 保存账号、情侣关系、授权、聊天元数据、位置轨迹、AI 结果等核心数据。
- 生产环境必须备份。

Redis：

- 保存会话、在线状态、限流、短期缓存。
- 不能作为唯一持久化数据源。

RabbitMQ：

- 负责服务间事件，例如消息同步、推送、AI 分析完成通知。

MinIO：

- 本地或自建环境使用的 S3 兼容对象存储。
- 生产环境也可以替换成云厂商对象存储。

OpenTelemetry Collector：

- 接收服务端 OTLP 日志、Trace、Metric。
- 统一导出到观测组件。

Prometheus / Grafana / Loki / Tempo / Alertmanager：

- Prometheus 保存指标。
- Grafana 展示看板。
- Loki 保存日志。
- Tempo 保存链路追踪。
- Alertmanager 负责预警。

## 安全规则

禁止提交：

- 真实数据库密码。
- Redis 密码。
- RabbitMQ 密码。
- MinIO 密钥。
- Grafana 密码。
- kubeconfig 原文。
- 云厂商 AK/SK。
- 证书私钥。

日志和可观测性数据仍然必须遵守隐私规则，不能包含聊天原文、完整经纬度、Wi-Fi 名称、手机号、验证码、token、prompt 或模型输入快照。
