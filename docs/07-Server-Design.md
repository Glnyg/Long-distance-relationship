---
status: active
owner: backend
last_changed: 2026-05-09
---

# C# 服务端设计

## 总体技术

服务端使用 C#、.NET 10、ASP.NET Core Minimal APIs。首版采用受控微服务，每个服务边界清晰，但避免过细拆分。

## 服务列表

`IdentityService`：

- 手机号验证码登录。
- 微信登录绑定。
- token 签发和刷新。
- 账号注销。

`CoupleService`：

- 生成绑定邀请码。
- 接受绑定邀请。
- 解绑。
- 查询当前情侣关系。

`ConsentService`：

- 管理聊天、定位、设备状态、AI 的双方授权。
- 记录授权版本。
- 处理撤回授权。

`LocationService`：

- 接收位置点。
- 轨迹查询。
- 轨迹清理。
- 30 天保留策略。

`DeviceStateService`：

- 接收电量、充电、Wi-Fi、App 打开、用机、通话状态。
- 查询当前状态和历史摘要。

`MessagingService`：

- 情侣文本消息。
- 图片和语音消息元数据。
- 已读、撤回、同步。

`RtcGatewayService`：

- 校验情侣关系。
- 签发 RTC token。
- 处理 RTC 回调。

`AiPrivacyService`：

- 承载 `AiPrivacyGateway`。
- 校验授权。
- 调用国内云模型。
- 保存 AI 结果和审计元数据。

`NotificationGatewayService`：

- 厂商推送适配。
- OPPO 推送首发。
- 后续扩展华为、小米、vivo。

`AuditService`：

- 保存敏感操作审计。
- 支持后台查询和风控排查。

`Observability`：

- 不是业务服务，而是服务端共享基础能力。
- 统一结构化日志、OpenTelemetry Trace、OpenTelemetry Metric、`correlation_id` 和健康检查。
- 帮助排查登录、绑定、定位、聊天、AI、推送等全链路问题。

`PlatformDeployment`：

- 不是业务服务，而是部署基础设施。
- 通过 Kubernetes 管理 PostgreSQL、Redis、RabbitMQ、MinIO 和可观测性组件。
- 通过 GitHub Actions 执行 CI/CD。

## 服务关系解释

初学者可以这样理解：

- 账号服务回答“你是谁”。
- 情侣服务回答“你和谁绑定”。
- 授权服务回答“你们同意共享什么”。
- 定位服务回答“位置和轨迹在哪里”。
- 设备状态服务回答“手机当前是什么状态”。
- 消息服务回答“你们聊了什么消息”。
- RTC 网关回答“你们能不能发起通话”。
- AI 隐私服务回答“这次 AI 能不能分析，以及能分析哪些数据”。
- 通知网关回答“消息怎么推到手机”。
- 审计服务回答“敏感操作发生过什么”。

关键规则：

- 账号服务不保存聊天。
- 情侣服务不保存位置。
- 授权服务不做 AI 分析。
- AI 服务不绕过授权服务。
- 通知服务不读取聊天原文，只拿必要通知摘要。

## 典型请求流程

### 登录

1. 客户端提交手机号验证码。
2. 账号服务验证验证码。
3. 账号服务签发访问 token。
4. 客户端保存 token，后续请求带上 token。

### 上传位置

1. 客户端确认本地位置共享开关开启。
2. 客户端上传位置点。
3. API 网关校验 token。
4. 定位服务向授权服务确认双方授权。
5. 定位服务保存位置点。
6. 事件总线通知状态更新。

### 发起 AI 分析

1. 客户端请求 `/ai`。
2. AI 服务校验情侣关系。
3. AI 服务校验双方 AI 授权和数据类型授权。
4. AI 隐私网关拉取最小必要数据。
5. AI 隐私网关调用 `IChatClient`。
6. AI 服务保存双方共同可见结果。
7. 通知服务推送“AI 分析完成”。

## 数据库

首版推荐：

- PostgreSQL：核心业务数据。
- PostGIS：地理位置查询。
- TimescaleDB：轨迹时间序列，可按实际部署决定是否开启。
- Redis：会话、在线状态、限流、短期缓存。
- S3 兼容对象存储：图片、语音消息、头像等对象。

## 部署配置

服务端部署配置位于 `deploy/k8s/`。

首版包含：

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

后续每个 ASP.NET Core 服务实现后，必须补充：

- `Deployment`。
- `Service`。
- 健康检查。
- 资源请求和限制。
- 环境变量和 Secret 引用。
- OpenTelemetry OTLP endpoint。

部署变更必须通过 `scripts/Assert-DeploymentGate.ps1`。

## API 风格

移动端 API 使用 HTTPS JSON。

推荐路径：

- `/auth`
- `/couples`
- `/consents`
- `/locations`
- `/device-states`
- `/messages`
- `/rtc`
- `/ai`
- `/notifications`

实时能力：

- `ChatHub`
- `PresenceHub`
- `CoupleStateHub`

## 安全要求

- 所有接口默认鉴权。
- 所有情侣数据查询必须校验绑定关系。
- 所有敏感数据写入必须校验授权。
- 所有模型调用必须通过 AI 隐私网关。
- 不在日志中打印聊天原文、定位明细、Wi-Fi 名称、token、验证码。

## 全链路日志、可观测性和预警

服务端所有 ASP.NET Core 服务必须接入 `AIAPP.Observability`。

每个服务启动时必须配置：

```csharp
builder.Services.AddAiAppObservability(options =>
{
    options.ServiceName = "AIAPP.ExampleService";
});
```

每个服务必须启用：

```csharp
app.UseAiAppCorrelationId();
app.MapHealthChecks("/healthz");
```

每个关键操作必须能通过 `correlation_id` 串起来。一次 AI 分析至少要覆盖：

- 授权校验。
- 最小必要数据拉取。
- 模型调用。
- 结果保存。
- 审计记录。

日志和 Trace 允许记录：

- 服务名。
- 操作名。
- 错误码。
- 耗时。
- 数据类型。
- 授权版本。
- 模型 ID。
- 哈希后的用户 ID 和情侣 ID。

日志和 Trace 禁止记录：

- 聊天原文。
- 精确定位点。
- Wi-Fi 名称。
- 用机明细。
- 手机号。
- 验证码。
- token。
- prompt。
- 模型输入快照。

预警首版重点关注：

- API 5xx 错误率。
- API p95 延迟。
- AI 模型超时率。
- AI 审计写入失败。
- 位置上传成功率下降。
- 设备状态上传成功率下降。
- 推送失败率升高。
- 数据库或消息队列异常。

## 接口设计原则

- 移动端接口用 JSON，方便 Android 调用。
- 接口返回错误时使用稳定错误码，便于客户端显示中文提示。
- 写操作必须可审计。
- 敏感接口必须校验情侣关系和授权。
- 内部服务之间可以用事件传递状态变化，避免一个服务直接强依赖多个服务。

## 测试要求

- 单元测试覆盖核心业务规则。
- 集成测试使用 Testcontainers。
- 授权相关测试必须覆盖双方授权、单方撤回、解绑后访问失败。
- AI 测试必须覆盖输入不落库、审计不含隐私原文、模型失败降级。
- 可观测性测试必须覆盖敏感日志门禁、OpenTelemetry 接入门禁和 `correlation_id` 传递。

## P0 后端基础接口

P0 先落地三个服务，接口路径保持稳定：

- `IdentityService`：`POST /auth/sms/send-code`、`POST /auth/sms/login`、`POST /auth/wechat/login`、`POST /auth/token/refresh`、`POST /auth/logout`、`GET /auth/me`。
- `CoupleService`：`POST /couples/invitations`、`POST /couples/invitations/{code}/accept`、`GET /couples/current`、`POST /couples/current/unbind`。
- `ConsentService`：`GET /consents/current`、`PUT /consents/{dataType}`、`DELETE /consents/{dataType}`。

错误响应统一使用 `ProblemDetails`，扩展字段为 `error_code`、中文 `message` 和 `correlation_id`。路由层必须保持薄，只负责参数绑定、鉴权、调用 MediatR 命令或查询、返回结果。领域规则放在聚合根或领域服务里，应用层用命令和查询串联仓储、审计和时间。

P0 不接真实短信、微信或云厂商 Secret。短信和微信只定义适配接口，开发和测试使用替身实现。所有真实 Secret 只能由部署环境注入，仓库示例只能使用 `CHANGE_ME`。
