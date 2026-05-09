# Kubernetes 部署说明

## 这份目录是什么

这里保存项目的 Kubernetes 部署配置。初学者可以这样理解：Kubernetes 负责把服务端程序、数据库、缓存、消息队列、对象存储、日志和监控组件部署到服务器集群里。

当前目录先提供基础设施部署，不部署还不存在的业务 API 镜像。后续每个 ASP.NET Core 服务实现后，再新增对应 `Deployment`、`Service`、`Ingress` 和 HPA。

## 目录结构

- `base/`：所有环境共用的基础配置。
- `base/postgres/`：PostgreSQL + PostGIS。
- `base/redis/`：Redis。
- `base/rabbitmq/`：RabbitMQ。
- `base/minio/`：S3 兼容对象存储。
- `base/observability/`：OpenTelemetry Collector、Prometheus、Grafana、Loki、Tempo、Alertmanager。

## 部署前准备

真实环境部署前必须替换 Secret。

仓库里的 Secret 只使用占位值，不能用于生产环境。

建议生产环境使用：

- 云厂商托管 PostgreSQL、Redis、对象存储时，Kubernetes 内只保存连接信息。
- 自建 PostgreSQL、Redis、RabbitMQ、MinIO 时，必须配置持久化存储、备份、监控和恢复演练。
- Secret 建议由云厂商密钥管理、Sealed Secrets 或 External Secrets 管理。

## 本地验证

仅做清单语法和项目门禁：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Assert-ProjectGate.ps1
```

如果本机安装了 `kubectl`，可以手动做客户端 dry-run：

```powershell
kubectl kustomize deploy/k8s/base
```

## 部署命令

```powershell
kubectl apply -k deploy/k8s/base
```

## 重要提醒

- 不要把真实密码、token、证书提交到 Git。
- 不要在 Kubernetes 日志里输出聊天原文、完整经纬度、Wi-Fi 名称、手机号、验证码、token、prompt 或模型输入快照。
- 数据库和对象存储必须做备份。
- 可观测性组件用于排障，只能保存元数据和脱敏日志。
