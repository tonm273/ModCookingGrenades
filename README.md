# CookingGrenades

> **Version**: 1.4.1  
> **Compatibility**: SPT 4.1.0 / BepInEx 5.x / .NET Framework 4.7.2  
> **BepInEx Plugin ID**: `com.Tangh.CookingGrenades`

---

## Overview

CookingGrenades adds a **grenade cooking** mechanic to SPT. Just like in real combat, you can pull the pin, hold the grenade in hand to time your throw instead of tossing it immediately, then release at exactly the right moment so the enemy is caught by the blast in mid-air or the moment it lands — with no reaction time.

It also provides a **grenade trajectory prediction** and a **cooking indicator** to help you precisely control every grenade in the heat of a fight.

---

## Features

### 1. Cooking (Core Mechanic)

- **How it works**: While holding a grenade, hold **LMB** (overhand throw) or **RMB** (underhand throw) to start cooking.
- **Pin-pull detection**: Cooking only starts after the pin-pull animation completes, avoiding false timing.
- **Auto throw**: When the cook time approaches the fuse time, the grenade is force-thrown to prevent it from exploding in your hand.
- **Action lock**: Swapping weapons, opening doors, picking up items, etc. are disabled while cooking so the process is never interrupted.

### 2. Realistic Fuse Time

Adds **random variation** to grenade fuse times — no two grenades are identical, simulating real manufacturing tolerances:

- **Toggle**: F12 config menu.
- **Fuse Time Spread Factor**: range `0.001` ~ `0.6`
  - `0.001` ≈ fixed fuse time (no variation)
  - `0.6` ≈ large variation
- **Inventory UI**: choose whether to show the default fuse time or the randomized value in the item tooltip.

> Fuse time is generated via a normal (Gaussian) distribution (Box-Muller), so most grenades cluster near the average with a few outliers — closer to real-world behavior.

### 3. Fuse Time Tester (Simulator)

A built-in tool to help you tune the fuse time variation:

- **Simulation Target Value**: the baseline cook time in seconds to simulate.
- **Fuse Time Test Count**: samples per simulation (`1` ~ `100,000`).
- **Time Simulation To Output**: set to `true` to run one simulation and print results to `BepInEx/LogOutput.log`.

The log reports min/max/mean/std-dev plus a frequency distribution table so you can fine-tune precisely.

### 4. Trajectory Prediction

While holding a grenade ready to throw, a **trajectory line** and a **landing marker** are drawn:

- **Display modes**:
  - Mode `0`: always shown while holding a grenade.
  - Mode `1`: shown only while aiming an overhand/underhand throw.
- **Physics model**: analytic ballistic solution with linear drag, considering:
  - grenade mass (read dynamically from the Rigidbody)
  - current player movement speed
  - strength skill level and hand stamina
  - overhand/underhand multiplier differences
- **Collision detection**: the line collides with the scene; the landing marker shows the impact point.
- **Configurable**: line/landing colors, width, radius, sample points and step time (affects smoothness and performance), and a **Throw Force Multiplier** for calibrating prediction vs. actual landing point.

### 5. Cook Indicator

A screen blinking indicator while cooking, so you can sense the remaining time without staring at the grenade:

- **Position**: offset from screen center (configurable X/Y offsets).
- **Blink rhythm**:
  - time remaining > 2s: yellow slow blink, accelerating as cooking progresses.
  - time remaining ≤ 2s: red fast blink, warning of imminent explosion.
- **Throw animation**: on release the icon rotates 45° counter-clockwise and fades out.
- **Customizable**: icon size, height, and screen offset.

### 6. Audio Improvements

- **Pin sound replacement**: uses a more fitting `TripwirePin` sound instead of the default fuse sound for M67, V40, M18 smoke, M7290 flash, RDG-2B, etc.
- **Fuse sound suppression**: while cooking, the default fuse hissing is muted to avoid overlapping sounds.

### 7. Safety Warning

On first install, entering the main menu shows a **safety warning** about the real-world danger of cooking grenades. It is shown once; confirming it disables it permanently.

### 8. Grenade Wheel

Hold **G** to open a **grenade wheel** at screen center, showing all grenades in your inventory:

- **Usage**: hold G in combat → wheel pops up at screen center → move the mouse to select → release G to equip.
- **Visuals**: grenades arranged in a circle, color-coded by type, gold highlight on selection.
- **Cursor control**: the cursor is released while the wheel is open for precise selection, and locked again on close.
- **Scanning**: scans the **tactical rig** and **pockets**; the **backpack** is not scanned by default (toggleable in F12).
- **Safety**: the wheel won't trigger while cooking; shooting/aiming/weapon-switching is disabled while the wheel is open.
- **Toggle**: fully disable in the F12 config menu (enabled by default).

### 9. Medicine Wheel

Reuses the wheel UI, holding a key opens a **medicine wheel**, select, release to use:

- **Usage**: hold **H** (rebindable in F12) → wheel pops up → select → release to use.
- **Scanning**:
  - Scans the **tactical rig** and **pockets** by default; the **backpack** is not scanned by default (configurable).
  - The **secure container** (Kappa/Alpha) is scanned by default (configurable). Items in it are usable in raid as far as the mod is concerned, but the in-raid storage is restricted by the game.
  - Food and drinks shown by default (toggleable).
- **Color coding**: medkits=green, meds/stims/painkillers=purple, food & drink=orange, other medical supplies (bandages/surgery kits/etc.)=blue.
- **Usage**: uses the native `Proceed` flow, so both medical items and food/drinks can be used directly.
- **Mutual exclusion**: cannot be open at the same time as the Grenade Wheel; won't trigger while cooking.
- **Input protection**: game input is blocked while the wheel is open to prevent movement/shooting/weapon-switching from interfering.
- **Toggle**: fully disable in the F12 config menu (enabled by default).

---

## Configuration Reference

All settings are adjustable in the F12 config menu, grouped below.

### 0. Cooking Grenades

| Setting | Type | Default | Description |
|--------|------|--------|------|
| `Enable Cooking Notification` | bool | `false` | Show a notification when cooking starts. |
| `Show Default Fuse Time In Inventory UI` | bool | `true` | Show default fuse time (not the randomized value) in the inventory UI. |
| `Use Alternative Pin Sound` | bool | `true` | Use alternative pin sound for specific grenades. |
| `Auto Throw Lead Time` | float | `0.8` | How long before the fuse expires the grenade is force-thrown (0.1 ~ 3 s). |

### 1. Realistic Fuse Time

| Setting | Type | Default | Description |
|--------|------|--------|------|
| `Realistic Fuse Time Enable` | bool | `false` | Enable randomized fuse times. |
| `Fuse Time Spread Factor` | float | `0.085` | Spread (0.001 ~ 0.6); higher = wider variation. |

### 2. Fuse Time Tester

| Setting | Type | Default | Description |
|--------|------|--------|------|
| `Simulation Target Value` | float | `5.0` | Simulation baseline (seconds). |
| `Fuse Time Test Count` | int | `10000` | Samples per simulation. |
| `Time Simulation To Output` | bool | `false` | Set `true` to run one simulation and log the output. |

### 3. Debug

| Setting | Type | Default | Description |
|--------|------|--------|------|
| `Enable Cooking Time GUI` | bool | `false` | Enable debug GUI showing cooking timer info. |
| `User Warning Confirmed` | bool | `false` | Whether the safety warning was confirmed (advanced). |

### 4. Trajectory Prediction

| Setting | Type | Default | Description |
|--------|------|--------|------|
| `Enable Trajectory` | bool | `true` | Enable trajectory prediction. |
| `Display Mode` | int | `0` | 0 = shown while holding, 1 = only when aiming a throw. |
| `Trajectory Color` | Color | `(1,0.3,0.1,0.8)` | Trajectory line color (RGBA). |
| `Landing Point Color` | Color | `(1,0,0,0.9)` | Landing marker color (RGBA). |
| `Landing Point Radius` | float | `0.3` | Landing marker radius (m, 0.05 ~ 2). |
| `Trajectory Points` | int | `60` | Sample points (10 ~ 200; more = smoother but heavier). |
| `Trajectory Step Size` | float | `0.5` | Horizontal distance (m) between sample points (0.1 ~ 2); smaller = denser = smoother. |
| `Trajectory Line Width` | float | `0.015` | Line width (0.005 ~ 0.1; multiplied at render). |
| `Throw Force Multiplier` | float | `1.0` | Force multiplier (0.5 ~ 3) to calibrate predicted vs. actual landing. |
| `Recalc Interval (frames)` | int | `2` | Recalculate trajectory at most every N frames when throw params are unchanged (1 = every frame; higher = cheaper but less responsive). |

### 5. Cook Indicator

| Setting | Type | Default | Description |
|--------|------|--------|------|
| `Enable Cook Indicator` | bool | `true` | Enable the cooking indicator icon. |
| `Indicator Height` | float | `1.0` | Base height above screen center (m, 0.1 ~ 2). |
| `Indicator Scale` | float | `0.15` | Icon scale (0.05 ~ 1). |
| `Offset X (px)` | float | `200` | Horizontal offset from screen center (-500 ~ 500). |
| `Offset Y (px)` | float | `0` | Vertical offset from screen center (-500 ~ 500). |
| `Throw Animation Duration (s)` | float | `2.5` | Rotate/fade-out animation duration on throw (s, 0.1 ~ 5). |

### 6. Grenade Wheel

| Setting | Type | Default | Description |
|--------|------|--------|------|
| `Enable Grenade Wheel` | bool | `true` | Enable the grenade wheel selector (hold key). |
| `Grenade Wheel Key` | KeyCode | `G` | Key to hold to open the grenade wheel. |
| `Equip Immediately On Select` | bool | `true` | Equip the selected grenade immediately on release; if disabled, only sets it as preferred (press the key again to pull it out). |
| `Switch Immediately When Holding` | bool | `true` | If you already hold a grenade, selecting a different one switches to it immediately; if disabled, only sets the preference. |

### 7. Medicine Wheel

| Setting | Type | Default | Description |
|--------|------|--------|------|
| `Enable Medicine Wheel` | bool | `true` | Enable the medicine wheel selector. |
| `Medicine Wheel Key` | KeyCode | `H` | Key to hold to open the medicine wheel. |
| `Scan Secure Container For Medicine` | bool | `false` | Also scan the secure container for medicine. |
| `Scan Backpack For Medicine` | bool | `false` | Scan the backpack for medicine (default: pockets and tactical rig only). |
| `Include Food And Drinks` | bool | `true` | Show food and drinks in the medicine wheel. |

---

## Controls

| Action | Key | Description |
|------|------|------|
| Overhand cook | Hold **LMB** | Start cooking after pin-pull; release for an overhand throw. |
| Underhand cook | Hold **RMB** | Start cooking after pin-pull; release for an underhand roll. |
| Force throw | Auto | Auto-thrown near fuse expiry to prevent in-hand explosion. |
| Open Grenade Wheel | Hold **G** | Show grenade wheel, move mouse to select, release to equip. |
| Open Medicine Wheel | Hold **H** (rebindable) | Show medicine wheel, move mouse to select, release to use. |

> **Note**: swapping weapons, opening doors, and interacting are disabled while cooking to prevent interruption.

---

## Compatibility

- Minimum: SPT 4.1.0 / BepInEx 5.x / .NET Framework 4.7.2.
- Each patch is wrapped in an independent try-catch; a single failing feature will not crash the whole mod.
- Keeps the compatibility logic for the old `Cook(float, bool)` flow.

---

## Multiplayer (Fika)

Cooking time is synchronized across clients when Fika is installed (detected at runtime). The grenade is spawned on remote clients with the correct cook time carried in the `GrenadePacket`, so one player's cooking timing is reflected on other players' screens.

---

## License

All assets and code are provided as-is for use with SPT Single Player Tarkov. Use at your own risk.
