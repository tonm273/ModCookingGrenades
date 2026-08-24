using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using CookingGrenades.Config;
using CookingGrenades.Utils;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace CookingGrenades.Patches;

/// <summary>
/// In SPT 4.1.0, ThrowWeapItemClass was deobfuscated to EFT.InventoryLogic.ThrowWeap.
/// The GetExplDelay property getter still exists.
/// </summary>
public class ThrowWeapGetExplDelayPatch : ModulePatch
{
	// ConditionalWeakTable：弱键，条目随 ThrowWeap 被 GC 时自动回收。
	// 相比原先 Dictionary<WeakReference>, 消除了"失效条目永不清理"的无界增长，
	// 也省去了每次查询 new WeakReference 产生的分配。
	// 注意 ConditionalWeakTable 的 TValue 也必须是引用类型，因此用 DelayRef 包装 float。
	// 独立锁对象：ResetExplDelay 会替换 _explDelay 表引用，若直接 lock 字段引用，
	// 替换后其他线程会锁到旧表（相互不互斥）→ 竞态。所有读写统一锁该对象。
	private static readonly object _explDelayLock = new object();
	private static ConditionalWeakTable<ThrowWeap, DelayRef> _explDelay = new ConditionalWeakTable<ThrowWeap, DelayRef>();

	private sealed class DelayRef
	{
		public float Value;
	}

	private static HashSet<string> uiTypes = new HashSet<string>
	{
		typeof(InfoWindow).FullName,
		typeof(ItemUiContext).FullName,
		typeof(ItemSpecificationPanel).FullName,
		typeof(CompactCharacteristicPanel).FullName
	};

	// 背包/物品栏 UI 判断的短时缓存：避免对每颗新手雷反复构造 StackTrace
	private static float _lastUiCheckTime = -1f;
	private static bool _lastWasUi;
	private static readonly object _uiLock = new object();

	/// <summary>
	/// 清除已缓存的引信值（配置变更时调用）。ConditionalWeakTable 无 Clear，直接换新表。
	/// </summary>
	public static void ResetExplDelay()
	{
		lock (_explDelayLock)
		{
			_explDelay = new ConditionalWeakTable<ThrowWeap, DelayRef>();
		}
	}

	/// <summary>
	/// 设置手雷的引信时间（温雷功能调用）
	/// </summary>
	public static void SetExplDelay(ThrowWeap throwWeap, float delay)
	{
		lock (_explDelayLock)
		{
			_explDelay.Remove(throwWeap);
			_explDelay.Add(throwWeap, new DelayRef { Value = delay });
		}
	}

	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.PropertyGetter(typeof(ThrowWeap), "GetExplDelay");
	}

	[PatchPostfix]
	public static void PatchPostfix(ThrowWeap __instance, ref float __result)
	{
		// 先检查是否有温雷注入的值（最高优先级）
		lock (_explDelayLock)
		{
			if (_explDelay.TryGetValue(__instance, out var delayRef))
			{
				__result = delayRef.Value;
				return;
			}
		}

		if (!ConfigManager.RealisticFuseTimeEnable.Value)
		{
			return;
		}

		// 背包/物品栏 UI 读取时显示原始引信，不做随机化（短时缓存避免高频构造 StackTrace）
		if (ConfigManager.ShowDefaultFuseTimeInInventoryUI.Value && IsInUiContext())
		{
			return;
		}

		float num = MathUtils.GenerateNormalRandomBoxMuller(__result, __result * ConfigManager.FuseTimeSpreadFactor.Value);
		lock (_explDelayLock)
		{
			_explDelay.Remove(__instance);
			_explDelay.Add(__instance, new DelayRef { Value = num });
		}
		__result = num;
	}

	/// <summary>
	/// 短时缓存"是否在背包/物品栏 UI 上下文"的判定，避免每次 getter 调用都构造 StackTrace。
	/// 0.5 秒内复用上次结果，把栈回溯成本降到接近 0。
	/// </summary>
	private static bool IsInUiContext()
	{
		float now = Time.realtimeSinceStartup;
		if (now - _lastUiCheckTime < 0.5f)
		{
			return _lastWasUi;
		}
		lock (_uiLock)
		{
			if (now - _lastUiCheckTime < 0.5f)
			{
				return _lastWasUi;
			}
			_lastWasUi = ComputeIsInUiContext();
			_lastUiCheckTime = now;
			return _lastWasUi;
		}
	}

	private static bool ComputeIsInUiContext()
	{
		StackTrace stackTrace = new StackTrace(2, fNeedFileInfo: false);
		for (int i = 0; i < stackTrace.FrameCount; i++)
		{
			var declaringType = stackTrace.GetFrame(i).GetMethod()?.DeclaringType;
			if (declaringType != null && uiTypes.Contains(declaringType.FullName))
			{
				return true;
			}
		}
		return false;
	}
}