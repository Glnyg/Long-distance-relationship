---
status: draft
owner: product
last_changed: 2026-05-09
permissions:
  - PACKAGE_USAGE_STATS
  - READ_PHONE_STATE
api_surface:
  - /device-states
test_status: planned
---

# 设备状态共享

## 目标

双方授权后，可以看到对方的基础设备状态，用于异地陪伴和减少误会。

## 首版状态

- 电量。
- 是否充电。
- 是否连接 Wi-Fi。
- Wi-Fi 名称，只有系统允许时展示。
- App 打开状态。
- 用机时长摘要。
- 通话中/通话结束状态。

## 禁止采集

- 通话号码。
- 联系人。
- 录音。
- 通话内容。
- 其他 App 的具体隐私内容。

## Android 实现

- 电量和充电状态使用系统广播或电池 API。
- 用机状态需要用户开启 Usage Access。
- Wi-Fi 名称受系统权限和定位开关影响，取不到时只能显示连接状态。
- 通话首版只做状态，不采集号码和内容。

## 失败与降级

- 未开启 Usage Access：展示“未开启用机共享”。
- 无法读取 Wi-Fi 名称：展示“已连接 Wi-Fi”。
- 通话权限拒绝：不展示通话状态。

## 审核注意

权限说明必须强调“状态共享”，不能暗示会读取通话内容或联系人。
