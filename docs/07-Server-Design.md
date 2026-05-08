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

## 数据库

首版推荐：

- PostgreSQL：核心业务数据。
- PostGIS：地理位置查询。
- TimescaleDB：轨迹时间序列，可按实际部署决定是否开启。
- Redis：会话、在线状态、限流、短期缓存。
- S3 兼容对象存储：图片、语音消息、头像等对象。

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

## 测试要求

- 单元测试覆盖核心业务规则。
- 集成测试使用 Testcontainers。
- 授权相关测试必须覆盖双方授权、单方撤回、解绑后访问失败。
- AI 测试必须覆盖输入不落库、审计不含隐私原文、模型失败降级。
