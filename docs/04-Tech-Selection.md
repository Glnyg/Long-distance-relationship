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

初学者解释：

- Kotlin 是 Android 官方主推语言，写 Android App 更自然。
- Jetpack Compose 是现代 Android 界面框架，可以用代码直接描述界面。
- ViewModel 用来保存页面状态，避免旋转屏幕或页面重建时数据丢失。
- Room 是手机本地数据库，用于离线缓存。
- DataStore 用于保存轻量设置，例如是否看过权限说明。
- WorkManager 用于系统允许的后台任务，例如网络恢复后补传。

为什么不用传统 XML UI：

- 传统 XML 仍能用，但 Compose 更适合新项目，代码结构更统一，也更利于模块化维护。

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

初学者解释：

- .NET 10 LTS 是长期支持版本，适合新项目长期维护。
- ASP.NET Core Minimal APIs 可以快速写 HTTP 接口，结构比传统控制器更轻。
- PostgreSQL 是主要数据库，可靠、通用、生态成熟。
- PostGIS 是 PostgreSQL 的地理能力扩展，用来处理位置数据。
- Redis 适合放在线状态、验证码频控、短期缓存。
- RabbitMQ + MassTransit 用于服务之间发送事件，避免服务强耦合。
- OpenTelemetry 用于日志、指标和链路追踪，方便排查问题。

为什么不用一个巨大的单体：

- 单体前期简单，但账号、授权、定位、聊天、AI、推送都属于不同边界，后期会互相影响。

为什么也不做过细微服务：

- 过细微服务会增加部署、测试、排查和数据一致性成本。首版只拆核心边界。

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

为什么首版不上 MAF：

- MAF 更适合 Agent loop、工具自主调用、多步骤代理工作流。
- 首版 AI 是用户主动触发的一次分析，不需要代理自己决定调用工具。
- 不上 MAF 可以降低复杂度，减少隐私风险，也更容易测试。

为什么不用业务代码直接调用模型 SDK：

- 直接调用容易绕过授权。
- 直接调用容易把 prompt、聊天原文、轨迹明细写进日志。
- 统一通过 `IChatClient` 和 AI 隐私网关，后续更换国内云模型供应商更容易。

为什么不后台偷偷分析：

- 聊天、轨迹、用机状态都是敏感个人信息。
- 后台分析会增加误判、合规和信任风险。
- 首版只允许用户主动触发，并且结果双方共同可见。

## 音视频

首版使用第三方 RTC 服务，不自建 WebRTC/SFU。

服务端只负责：

- 校验双方绑定关系。
- 签发 RTC token。
- 处理 RTC 回调。
- 记录审计。

为什么不自建 RTC：

- 音视频涉及弱网、回声消除、设备兼容、码率控制、断线重连。
- 自建 WebRTC/SFU 的研发和运维成本很高。
- 首版更应该把精力放在情侣关系、权限和隐私体验上。

## 锁屏

首版使用 Android 设备管理员能力触发系统锁屏，不使用无障碍服务做强制锁屏。

为什么不用无障碍做锁屏：

- 无障碍服务是为残障辅助设计的，不应被用来做普通控制功能。
- 用无障碍强行覆盖屏幕容易被应用商店认为滥用。
- 设备管理员能力边界更清楚：只能触发系统锁屏，不能绕过系统解锁。

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
