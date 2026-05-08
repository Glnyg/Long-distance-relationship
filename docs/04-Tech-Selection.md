---
status: active
owner: architecture
last_changed: 2026-05-09
---

# 技术选型

## 总体结论

客户端使用 Android 原生 Kotlin。服务端使用 C#/.NET 10。AI 使用 `Microsoft.Extensions.AI + IChatClient`，首版不上 MAF。部署采用云无关的 Docker/Kubernetes 方案。

## Android 客户端

选型：

- 语言：Kotlin。
- UI：Jetpack Compose + Material 3。
- 架构：官方推荐分层架构，UI 层、领域层、数据层分离。
- 状态：ViewModel + Kotlin Coroutines + Flow。
- 导航：Navigation Compose。
- 依赖注入：Hilt。
- 本地数据库：Room。
- 轻量配置：DataStore。
- 后台任务：WorkManager。
- 网络：Retrofit 或 Ktor Client，首版建议 Retrofit。
- WebSocket/实时状态：SignalR Android 客户端或标准 WebSocket 适配器。
- 地图：国内地图供应商适配器，首版优先高德地图或腾讯地图，不把地图 SDK 写死进业务层。
- 位置：`LocationProvider` 抽象，国内设备不能依赖 Google Play 服务作为唯一实现。

Android 最低版本：Android 12+。

理由：

- 现代权限模型更统一。
- 能减少旧机型后台行为差异。
- 更适合 Compose 和现代 Jetpack 组件。

## 服务端

选型：

- 语言：C#。
- 运行时：.NET 10 LTS。
- Web 框架：ASP.NET Core Minimal APIs。
- 实时通信：SignalR。
- 服务间通信：gRPC 或事件总线，首版优先事件总线。
- 数据库：PostgreSQL。
- 空间数据：PostGIS。
- 轨迹时间序列：TimescaleDB 扩展可选。
- 缓存和在线状态：Redis。
- 消息队列：RabbitMQ + MassTransit。
- 对象存储：S3 兼容存储。
- 可观测性：OpenTelemetry。
- 测试：xUnit + Testcontainers。

## 微服务拆分

首版采用受控微服务，不做过细拆分：

- `IdentityService`
- `CoupleService`
- `ConsentService`
- `LocationService`
- `DeviceStateService`
- `MessagingService`
- `RtcGatewayService`
- `AiPrivacyService`
- `NotificationGatewayService`
- `AuditService`

服务间共享规则：

- 用户身份只由账号服务负责。
- 情侣关系只由绑定服务负责。
- 授权状态只由授权隐私服务负责。
- AI 不直接查所有数据库，由 AI 隐私网关通过受控接口拉取最小必要数据。

## AI

首版 AI 决策：

- 使用 `Microsoft.Extensions.AI`。
- 业务代码依赖 `IChatClient`。
- 国内云模型通过 provider 适配器接入。
- 首版不上 MAF。
- 禁止 Agent loop、工具自主调用、多 agent 和长期记忆。

适用场景：

- 用户主动触发的聊天、轨迹、用机状态共同分析。
- 输出强类型 JSON。
- 失败时给出可解释降级。

不适用场景：

- 后台持续监控。
- 实时关系风控。
- 自动画像评分。

## 音视频

首版使用第三方 RTC 服务，不自建 WebRTC/SFU。

服务端只负责：

- 校验双方绑定关系。
- 签发 RTC token。
- 处理 RTC 回调。
- 记录审计。

## 推送

国内多商店路线不依赖 FCM。首版 OPPO 优先，后续扩展华为、小米、vivo。

设计为：

- `NotificationGatewayService`
- `PushProvider` 抽象
- `OppoPushProvider`
- 后续 `HuaweiPushProvider`、`MiPushProvider`、`VivoPushProvider`

## 文档

文档使用 Obsidian Markdown。文档以中文为主，是唯一事实源。

任何功能变更必须同步：

- 设计文档。
- 功能文档。
- 权限隐私文档。
- ADR。
- 变更记录。
