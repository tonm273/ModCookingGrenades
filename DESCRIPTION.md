# CookingGrenades

> **Version**: 1.4.2  
> **Compatibility**: SPT 4.1.0 / BepInEx 5.x / .NET Framework 4.7.2  
> **Plugin ID**: `com.Tangh.CookingGrenades`  
> **Fika Co-op**: Supported

---

## Compatibility

- **Fika Co-op** — Fully supported (cooking times are synced across multiplayer clients).
- **Fast Grenade Throw** — Potentially incompatible. If you use this mod together with Fast Grenade Throw, the grenade cooking and throw behavior may conflict.

---

## Overview

CookingGrenades adds a **grenade cooking** mechanic to SPT (Single Player Tarkov). Just like in real combat, you can pull the pin and hold the grenade in hand to time your throw, then release at the exact right moment so the enemy is caught by the blast in mid-air or the instant it lands — with no reaction time.

The mod also includes **trajectory prediction**, a **cooking indicator**, a **grenade wheel**, and a **medicine wheel** to help you control every grenade and medical item in the heat of a fight.

---

## Key Features

### 1. Cooking (Core Mechanic)

- Hold **LMB** (overhand) or **RMB** (underhand) to start cooking a grenade.
- Cooking only begins **after the pin-pull animation completes**, preventing false timing.
- When the cook time approaches the fuse time, the grenade is **auto-thrown** to prevent explosion in hand.
- Swapping weapons, opening doors, picking up items, etc. are **disabled** during cooking to ensure the process is never interrupted.
- The **auto-throw lead time** is configurable (default 0.8s before detonation).

### 2. Realistic Fuse Time (Optional)

- Introduces **random variation** to grenade fuse times — no two grenades are identical, simulating real manufacturing tolerances.
- Variation is generated via a **Gaussian (Box-Muller) distribution**, so most grenades cluster near the average with a few outliers.
- Spread factor is configurable (0.001 ~ 0.6). Default is **disabled**.
- A built-in **fuse time simulator** lets you preview the distribution by sampling up to 100,000 grenades and logging statistics to `BepInEx/LogOutput.log`.

### 3. Trajectory Prediction

While holding a grenade ready to throw, a **trajectory line** and **landing marker** are drawn:

- Two display modes: always-on, or only while aiming.
- Physics model uses an **analytic ballistic solution with linear drag**, factoring in:
  - Grenade mass (read dynamically from the Rigidbody)
  - Player movement speed
  - Strength skill level and hand stamina
  - Overhand/underhand throw multiplier
- The line **collides with the scene**; the marker shows the actual impact point.
- Configurable colors, line width, sampling resolution, step size, and a **throw force multiplier** for calibrating prediction vs. actual landing point.
- Trajectory **recalculates every frame while moving** for smooth tracking, and throttles when stationary to save CPU.

### 4. Cook Indicator

A screen-space blinking icon while cooking, so you can sense the remaining time without staring at the grenade:

- **Time remaining > 2s**: yellow slow blink, accelerating as cooking progresses.
- **Time remaining ≤ 2s**: red fast blink, warning of imminent explosion.
- On release, the icon **rotates 45° counter-clockwise** and fades out.
- Configurable icon size, height, and screen X/Y offset.

### 5. Grenade Wheel

Hold **G** (configurable) to open a radial **grenade selector** at screen center:

- Scans **tactical rigs** and **pockets** for grenades.
- Move mouse to select, release key to equip — no menu navigation needed.
- Auto-releases the mouse cursor for precise selection; re-locks on close.
- Prevents firing, aiming, and weapon switching while open.
- Item names are pulled from the game's **native localization**, supporting all languages automatically.

### 6. Medicine Wheel

Hold **H** (configurable) to open a radial **medicine selector**, reusing the wheel UI:

- Scans **tactical rigs** and **pockets** for medical items. Secure container scanning is **off by default**; backpack scanning is off by default.
- Food and drinks are **included by default** (can be disabled).
- Items are **color-coded by category**: medkits = green, stimulants/painkillers = purple, food/water = orange, other medical items = blue.
- Selecting an item triggers the game's native `Proceed` usage flow.
- Mutually exclusive with the Grenade Wheel — only one can open at a time.

### 7. Audio Improvements

- Replaces the default pin-pull sound with a more appropriate `TripwirePin` sound for M67, V40, M18 smoke, M7290 flash, RDG-2B, and other grenades.
- **Masks** the default fuse hissing sound during cooking to prevent audio overlap.

### 8. Fika Multiplayer Synchronization

When playing with [Fika](https://github.com/project-fika/Fika) in co-op:

- Cooking times are **synchronized across all clients** via Harmony patches on `GrenadePacket` serialization/deserialization.
- Reflection methods are **cached** in a `ConcurrentDictionary` to eliminate repeated lookup overhead.
- Cook time values use `ConditionalWeakTable` for **automatic entry recycling** (no memory leaks).
- Fields are marked `volatile` for cross-thread visibility.
- Each observed controller stores its own cook time, preventing interference between multiple players cooking simultaneously.

### 9. Safety Warning

On first launch, a **safety warning** is displayed at the main menu reminding players of the real-world danger of grenade cooking. Dismissed once, it won't appear again.

---

## Configuration

All settings are adjustable via the **F12 config menu** in-game, organized into 8 categories:

| Category | Key Options |
|----------|-------------|
| Cooking | Auto-throw lead time, cooking notification, pin sound |
| Realistic Fuse Time | Enable toggle, spread factor |
| Fuse Time Tester | Target value, sample count, output toggle |
| Debug | Cooking time GUI, warning confirmation |
| Trajectory | Display mode, colors, sampling, throw force |
| Cook Indicator | Size, height, offsets, animation duration |
| Grenade Wheel | Enable, key, equip behavior |
| Medicine Wheel | Enable, key, scan targets, food/drinks |
