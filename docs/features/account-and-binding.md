---
status: draft
owner: product
last_changed: 2026-05-09
permissions: []
api_surface:
  - /auth
  - /couples
test_status: planned
---

# 账号与情侣绑定

## 目标

用户可以通过手机号和微信注册登录，并且只能绑定一个情侣账号。绑定后所有聊天、位置、设备状态、AI 分析都只在这对情侣之间发生。

## 用户流程

1. 用户用手机号验证码登录。
2. 用户可以绑定微信作为第三方登录方式。
3. 一方生成绑定邀请码或二维码。
4. 另一方确认绑定。
5. 双方进入授权隐私中心，分别开启需要共享的功能。
6. 任一方可以发起解绑。

## 数据

- 用户 ID。
- 手机号哈希或加密值。
- 微信 openid/unionid。
- 情侣关系 ID。
- 绑定状态。
- 绑定时间、解绑时间。

## 规则

- 一个账号同一时间只能绑定一个情侣账号。
- 解绑后停止新的共享数据写入。
- 解绑后历史数据按各功能文档的删除和留存规则处理。
- 必须提供账号注销入口。

## 失败与降级

- 验证码失败：提示重新获取。
- 微信登录失败：允许手机号登录。
- 绑定码过期：重新生成。
- 对方已有绑定：拒绝绑定。

## 审核注意

上架资料必须说明账号服务和注销机制。

## P0 后端接口

账号接口：

- `POST /auth/sms/send-code`：发送验证码，只返回下一次可发送时间，不返回验证码。
- `POST /auth/sms/login`：手机号验证码登录，返回 access token、refresh token 和用户摘要。
- `POST /auth/wechat/login`：通过微信适配接口登录，P0 使用测试替身，不提交真实 app secret。
- `POST /auth/token/refresh`：使用 refresh token 换取新 token。
- `POST /auth/logout`：撤销当前 refresh token。
- `GET /auth/me`：查询当前登录用户。

情侣绑定接口：

- `POST /couples/invitations`：生成长随机邀请码和二维码 payload。
- `POST /couples/invitations/{code}/accept`：接受邀请，成功后邀请码立即失效。
- `GET /couples/current`：查询当前情侣关系。
- `POST /couples/current/unbind`：解绑，解绑后停止新的共享数据写入。

## P0 隐私与审计

手机号必须同时保存规范化哈希和加密值：哈希用于唯一索引，加密值只用于必要账号展示和恢复流程。日志、Trace、Metric 和审计都不能记录手机号原文、验证码、access token、refresh token 或邀请码原文。

登录、刷新、登出、生成邀请码、接受邀请和解绑都是敏感操作，必须写入审计元数据。审计只保存操作名、结果、错误码、哈希后的用户 ID、哈希后的情侣 ID 和时间。
