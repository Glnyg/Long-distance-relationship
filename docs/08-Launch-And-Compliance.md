---
status: active
owner: release
last_changed: 2026-05-09
---

# 上架与合规方案

## 首发路线

首发应用商店：OPPO 软件商店。

架构预留国内多商店：

- OPPO
- 华为
- 小米
- vivo

## OPPO 上架注意事项

上架前必须准备：

- APP 备案。
- 软件著作权或必要资质材料。
- 中文隐私政策网页链接。
- 权限用途说明。
- 敏感权限说明。
- 账号注销机制。
- 应用截图和功能描述。

OPPO 平台会解析安装包权限，并可能进行隐私合规检测。权限说明必须和实际功能一致。

## 隐私政策必须写清楚

必须说明：

- 收集哪些个人信息。
- 每类信息用于什么功能。
- 是否属于敏感个人信息。
- 是否用于 AI 分析。
- 是否提供给第三方 RTC、地图、推送、AI 模型服务商。
- 保存多久。
- 如何撤回授权。
- 如何删除数据。
- 如何注销账号。

## 权限审核重点

定位：

- 说明前台定位和后台定位用途。
- 说明分钟级轨迹用于情侣共享。
- 提供关闭共享入口。

用机状态：

- 说明需要 Usage Access 的功能目的。
- 未开启时不得影响基础聊天。

通话状态：

- 首版只说明“通话中/通话结束状态”。
- 不采集号码、联系人、录音、通话内容。

设备管理员：

- 说明只用于用户主动开启的锁屏答题。
- 说明如何关闭设备管理员权限。

AI：

- 说明 AI 会处理聊天、轨迹、用机状态。
- 说明必须双方单独同意。
- 说明输入不落库，结果保留 30 天。
- 说明结果双方共同可见。

## 审核风险控制

禁止：

- 未授权采集。
- 权限申请和功能不匹配。
- 强制给权限才能使用无关功能。
- 隐藏后台定位。
- 使用无障碍或悬浮窗做强控制。
- 隐私政策无法访问或用图片/PDF 替代普通网页。

## 参考资料

- [Android 后台定位说明](https://developer.android.com/develop/sensors-and-location/location/background)
- [Android 后台定位权限申请](https://developer.android.com/develop/sensors-and-location/location/permissions/background)
- [Android 前台服务限制](https://developer.android.com/about/versions/12/foreground-services)
- [Android 应用架构指南](https://developer.android.com/jetpack/guide)
- [.NET 版本支持](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support)
- [OPPO 开放平台协议与隐私要求](https://open.oppomobile.com/wiki/index)
