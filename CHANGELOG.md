# CookingGrenades 更新日志

## 版本 1.2.1 — 温雷提示图标 & SPT 4.1.0 适配

### 🆕 新增功能

- **手雷抛物线预测**：拿在手上瞄准（高抛/低抛）时显示轨迹线和落点标记
  - 可在 F12 配置菜单开关（默认开启）
  - 支持自定义轨迹颜色、线宽、采样点数、刷新率
  - 可通过 `ThrowForceMultiplier` 微调投掷距离倍率，解决预测落点偏差
  - 使用正确的物理模型（带线性阻力的解析解），考虑手雷质量、玩家速度、力量技能和手耐力加成

- **温雷提示图标**：温雷时在手雷上方显示闪烁图标，直观提示剩余爆炸时间
  - 图标放置在手雷上方（高度可配置）
  - 闪烁频率随剩余时间加快：正常阶段黄色慢闪，最后 2 秒变红快闪
  - 通过闪烁节奏感知剩余时间，无需紧盯计时器
  - 程序化生成圆形 Sprite，无需外部资源依赖
  - 图标始终面向相机，任何角度可见
  - 可在 F12 配置菜单开关（默认开启），支持自定义高度和缩放

### 🐛 问题修复

- **温雷（Cook）功能适配 SPT 4.1**：
  - 原版本依赖 `Cook(float, bool)` 方法，4.1 中签名改为 `Cook()` 且不再被调用
  - 改为在 `Grenade.Init` 阶段检测温雷状态，通过 `ThrowWeap.GetExplDelay` 动态注入缩短后的引信时间
  - 修复了温雷投掷后人物无法操控、视角锁死的问题（去掉了会破坏输入链的 `SetActive(false)`）
- **保险丝静音功能恢复**：4.1 中 `OnSoundAtPoint` 改为显式接口实现 `IEventsConsumer.OnSoundAtPoint`，通过接口映射正确找到目标方法
- **抛物线起点错误**：LineRenderer 改为 `useWorldSpace = true`，世界坐标直接绘制不再错乱
- **抛物线不可见**：线宽默认加粗 5 倍（起点 7.5cm / 终点 3.75cm），Shader 找不到时自动回退到 `Unlit/Color`
- **卡顿问题**：
  - 去掉了每帧调用的所有反射（`GetValue`/`GetValues` 全部改为直接属性访问）
  - 去掉了高频 `AppDomain.GetAssemblies()` 全量扫描，HitMask 反射结果永久缓存
  - 弹道采样点从 200 个降至 80 个，间距 1m
  - 新增 `RecalcInterval` 帧间隔控制（默认每 2 帧重算 1 次）+ 投掷参数增量变化检测（位置<5cm/力度不变时跳过重算）
  - SphereCast 碰撞检测从逐点检测改为隔点检测（前半段偶数帧点，最后 10 点必检确保落点精度）

### ⚙️ 配置项新增

| 配置项 | 默认值 | 说明 |
|---|---|---|
| `EnableTrajectoryRenderer` | `true` | 是否启用抛物线预测 |
| `TrajectoryColor` | 青色(1,1,0,1) | 轨迹线颜色（RGBA） |
| `TrajectoryHitColor` | 红色(1,0,0,1) | 落点命中时的轨迹颜色 |
| `TrajectoryLineWidth` | `0.015` | 轨迹基础线宽（实际渲染×5） |
| `TrajectorySegmentCount` | `80` | 轨迹采样点数 |
| `ThrowForceMultiplier` | `1.0` | 投掷力度倍率（>1扔得更远） |
| `HitPointSphereRadius` | `0.1` | 落点提示球半径（米） |
| `EnableCookIndicator` | `true` | 是否启用温雷提示图标 |
| `CookIndicatorHeight` | `0.3` | 图标 Y 轴微调基准高度（米），0.3m = 不补偿 |
| `CookIndicatorScale` | `0.15` | 图标缩放大小 |
| `CookIndicatorOffsetX` | `60` | 图标相对屏幕中心的水平偏移（像素，右正左负） |
| `CookIndicatorOffsetY` | `0` | 图标相对屏幕中心的垂直偏移（像素，上正下负） |

### 🔧 兼容性

- 最低支持：SPTarkov 4.1.0 / BepInEx 5.x / .NET Framework 4.7.2
- 所有 Patch 启用失败时会捕获异常并输出日志，不会导致插件整体崩溃
- `GrenadeHandsControllerCookPatch` 保留旧签名兼容逻辑（优先 4.1，回退 3.x）
