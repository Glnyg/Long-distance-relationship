---
status: active
owner: android
last_changed: 2026-05-09
related_adr:
  - ../adr/0002-engineering-standards-observability.md
---

# Android工程规范

## 这份文档是什么

这份文档规定 Android Kotlin 客户端代码应该怎样生成和维护。

初学者可以这样理解：Android 客户端就是用户手机上的 App，负责界面、权限申请、位置采集、聊天展示、通话入口、设备状态展示和通知展示。

## 技术固定选择

Android 首版使用：

- Kotlin。
- Jetpack Compose。
- Material 3。
- ViewModel。
- Kotlin Coroutines + Flow。
- Hilt。
- Room。
- DataStore。
- WorkManager。
- Retrofit。
- Android 12+。

模块规划继续使用：

- `app`：应用入口。
- `feature:*`：具体功能模块。
- `core:*`：通用能力。

新增可观测性能力时，预留 `core:observability`。

## 权限规则

权限必须由用户主动授权，不能绕过系统限制。

涉及以下能力时，界面必须说明用途：

- 定位。
- 后台定位。
- 通知。
- 用机访问。
- 设备管理员。
- 麦克风。
- 摄像头。
- 通话状态。
- Wi-Fi 状态。

用户拒绝权限时，App 必须降级：

- 拒绝定位：不显示当前位置和轨迹。
- 拒绝后台定位：只在 App 可见时更新。
- 拒绝通知：只做 App 内提醒。
- 拒绝用机访问：不展示用机状态。
- 拒绝设备管理员：不能触发系统锁屏。
- 拒绝麦克风或摄像头：不能进入对应通话模式。

## 日志规则

Android release 版本不得向 `Logcat` 写入敏感数据。

禁止记录：

- 聊天原文。
- 完整经纬度。
- Wi-Fi 名称。
- 手机号。
- 验证码。
- token。
- prompt 原文。
- 模型输入快照。

调试日志只能用于开发环境，并且也要先脱敏。

## 客户端可观测性

Android 客户端后续通过 `core:observability` 统一处理：

- 本地结构化事件。
- 崩溃摘要。
- 网络错误摘要。
- 权限状态变化。
- 后台任务执行结果。
- 用户主动触发的关键操作。

客户端上报时只上传排障需要的元数据，不上传隐私原文。

推荐字段：

- `operation`：操作名。
- `correlation_id`：关联 ID。
- `result`：成功或失败。
- `error_code`：错误码。
- `duration_ms`：耗时。
- `permission_state`：权限状态。
- `network_type`：网络类型，不包含 Wi-Fi 名称。

## 关联 ID 规则

用户触发关键操作时，客户端应生成或传递 `correlation_id`。

例如：

- 发送消息。
- 上传位置。
- 上传设备状态。
- 发起 AI 分析。
- 发起通话。
- 删除 AI 结果。

服务端收到请求后继续使用同一个 `correlation_id`，这样排查问题时能把客户端、服务端、模型调用、推送通知串起来。

## UI 规则

敏感权限界面必须说明：

- 采集什么。
- 为什么采集。
- 谁能看到。
- 保留多久。
- 如何关闭。

不要用恐吓式文案，不要诱导用户授权。

## 测试规则

Android 测试必须覆盖：

- 权限同意。
- 权限拒绝。
- 权限被系统收回。
- 后台定位受限。
- 省电模式影响后台任务。
- 通知关闭。
- 设备管理员关闭。
- 网络断开后恢复同步。
