---
status: draft
owner: product
last_changed: 2026-05-09
permissions:
  - POST_NOTIFICATIONS
api_surface:
  - /notifications
test_status: planned
---

# 通知与提醒

## 目标

在双方授权下，及时通知聊天消息、通话邀请、位置共享状态、设备状态变化、AI 结果和锁屏答题事件。

## 技术方案

国内多商店不依赖 FCM。使用通知网关统一适配厂商推送。

首版：

- OPPO Push。

后续：

- 华为 Push。
- 小米 Push。
- vivo Push。

## 通知类型

- 新消息。
- 通话邀请。
- 对方打开 App。
- 对方开始/停止充电。
- 电量低。
- Wi-Fi 连接变化。
- 位置共享暂停。
- AI 分析完成。
- 锁屏答题邀请。

## 隐私

通知内容默认不展示敏感详情。用户可以在设置中选择是否显示预览。

## 失败与降级

- 厂商 token 失效：重新注册。
- 推送失败：下次 App 打开时同步。
- 用户关闭通知权限：App 内展示提醒，不强迫开启。
