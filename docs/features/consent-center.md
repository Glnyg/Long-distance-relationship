---
status: draft
owner: privacy
last_changed: 2026-05-09
permissions: []
api_surface:
  - /consents
test_status: planned
---

# 授权隐私中心

## 目标

授权隐私中心统一管理所有敏感共享开关，让双方知道正在共享什么、为什么共享、如何暂停和撤回。

## 管理范围

- 定位共享。
- 分钟级轨迹。
- 设备状态共享。
- 聊天 AI 分析。
- 轨迹 AI 分析。
- 用机状态 AI 分析。
- 锁屏答题。
- 通知提醒。

## 规则

- 每类敏感数据必须单独授权。
- AI 使用某类数据时，需要额外 AI 授权。
- 任一方撤回授权后，停止新的相关采集和分析。
- 授权变更必须记录版本号、时间和发起人。

## 用户体验

每个开关必须显示：

- 功能名称。
- 需要的数据。
- 数据用途。
- 对方是否可见。
- 保留时间。
- 关闭后影响。

## 测试场景

- 单方未授权时功能不可用。
- 撤回授权后客户端停止上传。
- 服务端拒绝未授权写入。
- 授权版本变更被审计。

## P0 后端接口

- `GET /consents/current`：查询当前用户在当前情侣关系下的授权状态。
- `PUT /consents/{dataType}`：开启某类授权，并生成新的授权版本。
- `DELETE /consents/{dataType}`：撤回某类授权，并生成新的授权版本。

`dataType` 固定为：

- `location_current`
- `location_trail`
- `device_state`
- `chat_ai`
- `location_ai`
- `device_ai`
- `lock_challenge`
- `notifications`

AI 授权和数据授权必须分开。比如用户允许定位共享，不等于允许 AI 使用轨迹；用户允许聊天，不等于允许 AI 分析聊天。任一方撤回授权后，相关敏感写入和 AI 分析必须失败。

## P0 审计与可观测性

授权开启和撤回必须写审计元数据，包括操作名、数据类型、授权版本、结果、错误码、哈希后的用户 ID、哈希后的情侣 ID 和时间。审计、日志、Trace、Metric 都不能记录聊天原文、完整经纬度、Wi-Fi 名称、验证码、token、prompt 或模型输入快照。
