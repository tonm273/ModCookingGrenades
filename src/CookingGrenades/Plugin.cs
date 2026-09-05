using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Logging;
using CookingGrenades.Config;
using CookingGrenades.Patches;
using CookingGrenades.Utils;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace CookingGrenades;

[BepInPlugin("com.Tangh.CookingGrenades", "CookingGrenades", "1.4.2")]
[BepInDependency("com.SPT.core", "3.11.0")]
public class Plugin : BaseUnityPlugin
{
	internal static ManualLogSource log;

	// ── Fika 联机同步字段 ──────────────────────────────────────────
	/// <summary>发送端：最近一次投掷的烹饪时长，随 GrenadePacket.Serialize 尾随写入（volatile 保证跨线程可见）</summary>
	internal static volatile float NextCookTime;
	private static Harmony _fikaHarmony;

	private void Awake()
	{
		log = this.Logger;
		ConfigManager.Init(((BaseUnityPlugin)this).Config);
		ConfigEventHandler.Init();
		FuseTimeTester.Init();

		// 预烤轮盘程序化纹理（圆环/分隔线 + 常见扇区数的扇形/弧段），
		// 把生成开销从"每次打开轮盘"转移到"一次开机"，消除打开卡顿
		try
		{
			WheelTextureCache.WarmUp(12);
		}
		catch (Exception e)
		{
			log.LogWarning($"[WheelTextureCache] 预热失败（将按需生成）: {e.Message}");
		}

		// 手雷轮盘：跨场景持久化单例
		var wheelObj = new GameObject("GrenadeWheel");
		DontDestroyOnLoad(wheelObj);
		wheelObj.AddComponent<GrenadeWheel>();

		// 医药轮盘：跨场景持久化单例（复用轮盘 UI 效果）
		var medWheelObj = new GameObject("MedicineWheel");
		DontDestroyOnLoad(medWheelObj);
		medWheelObj.AddComponent<MedicineWheel>();

		// 每个 Patch 独立 try-catch，一个失败不影响其他
		TryEnablePatch<GrenadeInitPatch>();
		TryEnablePatch<PlayerGrenadeHandsControllerHandleFireInputPatch>();
		TryEnablePatch<PlayerGrenadeHandsControllerHandleAltFireInputPatch>();
		TryEnablePatch<BaseSoundPlayerOnSoundAtPointPatch>();
		TryEnablePatch<EftGamePlayerOwnerTranslateCommandPatch>();
		TryEnablePatch<ThrowWeapGetExplDelayPatch>();
		TryEnablePatch<GrenadeWheelInputPatch>();
		TryEnablePatch<GrenadeSelectorPatch>();
		TryEnablePatch<SetNewTopPriorityGrenadePatch>();
		TryEnablePatch<MedicineWheelInputPatch>();
		TryEnablePatch<PlayerLookPatch>();
		TryEnablePatch<PlayerMouseLookPatch>();
		// 抛物线预测：在 GameWorld.OnGameStarted 后挂载到 MainPlayer.gameObject（和 VisualAssist 一致）
		TryEnablePatch<TrajectoryRendererGameWorldStartedPatch>();
		TryEnablePatch<TrajectoryRendererPlayerDisposePatch>();

		if (!ConfigManager.UserWarningConfirmed.Value)
		{
			TryEnablePatch<MenuScreenPatch>();
		}

		// Fika 联机同步（反射定位，无 Fika 时安全跳过）
		TryInitFikaPatches();
	}

	private static void TryEnablePatch<T>() where T : ModulePatch, new()
	{
		try
		{
			((ModulePatch)new T()).Enable();
			log.LogInfo($"[Patch] {typeof(T).Name} 已启用");
		}
		catch (Exception e)
		{
			log.LogError($"[Patch] {typeof(T).Name} 启用失败: {e.Message}");
		}
	}

	// ── Fika 联机同步补丁 ──────────────────────────────────────────

	private void TryInitFikaPatches()
	{
		try
		{
			_fikaHarmony = new Harmony("com.Tangh.CookingGrenades.fika");
			bool fikaFound = false;
			fikaFound |= PatchClientGrenadeThrow("Fika.Core.Main.ClientClasses.HandsControllers.FikaClientGrenadeController");
			fikaFound |= PatchClientGrenadeThrow("Fika.Core.Main.ClientClasses.HandsControllers.FikaClientQuickGrenadeController");
			fikaFound |= PatchGrenadePacket();
			fikaFound |= PatchObservedSpawn("Fika.Core.Main.ObservedClasses.HandsControllers.ObservedGrenadeController");
		fikaFound |= PatchObservedSpawn("Fika.Core.Main.ObservedClasses.HandsControllers.ObservedQuickGrenadeController");

			log.LogInfo(fikaFound
				? "[Fika] 联机同步补丁已启用（检测到 Fika）"
				: "[Fika] 未检测到 Fika，联机同步已跳过");
		}
		catch (Exception e)
		{
			log.LogError($"[Fika] 初始化失败: {e.Message}");
		}
	}

	private bool PatchClientGrenadeThrow(string typeName)
	{
		var type = AccessTools.TypeByName(typeName);
		if (type == null) return false;
		var method = AccessTools.Method(type, "ThrowGrenade");
		if (method == null) return false;

		_fikaHarmony.Patch(method,
			prefix: new HarmonyMethod(typeof(FikaPatches_ClientGrenadeThrow), nameof(FikaPatches_ClientGrenadeThrow.Prefix)));
		log.LogInfo($"[Fika] 已 patch {typeName}.ThrowGrenade");
		return true;
	}

	private bool PatchGrenadePacket()
	{
		var type = AccessTools.TypeByName("Fika.Core.Networking.Packets.FirearmController.SubPackets.GrenadePacket");
		if (type == null) return false;

		var serialize = AccessTools.Method(type, "Serialize");
		var deserialize = AccessTools.Method(type, "Deserialize");
		var execute = AccessTools.Method(type, "Execute");

		if (serialize != null)
			_fikaHarmony.Patch(serialize,
				postfix: new HarmonyMethod(typeof(FikaPatches_GrenadePacketSerialize), nameof(FikaPatches_GrenadePacketSerialize.Postfix)));
		if (deserialize != null)
			_fikaHarmony.Patch(deserialize,
				postfix: new HarmonyMethod(typeof(FikaPatches_GrenadePacketDeserialize), nameof(FikaPatches_GrenadePacketDeserialize.Postfix)));
		if (execute != null)
		{
			_fikaHarmony.Patch(execute,
				prefix: new HarmonyMethod(typeof(FikaPatches_GrenadePacketExecute), nameof(FikaPatches_GrenadePacketExecute.Prefix)),
				postfix: new HarmonyMethod(typeof(FikaPatches_GrenadePacketExecute), nameof(FikaPatches_GrenadePacketExecute.Postfix)));
		}

		log.LogInfo("[Fika] 已 patch GrenadePacket Serialize/Deserialize/Execute");
		return true;
	}

	private bool PatchObservedSpawn(string typeName)
	{
		var type = AccessTools.TypeByName(typeName);
		if (type == null) return false;
		var method = AccessTools.Method(type, "SpawnGrenade");
		if (method == null) return false;

		_fikaHarmony.Patch(method,
			prefix: new HarmonyMethod(typeof(FikaPatches_ObservedGrenadeSpawn), nameof(FikaPatches_ObservedGrenadeSpawn.Prefix)));
		log.LogInfo($"[Fika] 已 patch {typeName}.SpawnGrenade");
		return true;
	}
}

// ── Fika 补丁内部类 ──────────────────────────────────────────────

/// <summary>GrenadePacket 编解码读写器的反射方法缓存（avoid per-call GetMethod 开销）</summary>
internal static class GrenadePacketCodecCache
{
	// 读写器类型固定（Fika 同一种二进制流类型），按类型缓存一次性查找结果
	private static readonly ConcurrentDictionary<Type, MethodInfo> PutMethods = new ConcurrentDictionary<Type, MethodInfo>();
	private static readonly ConcurrentDictionary<Type, MethodInfo> GetFloatMethods = new ConcurrentDictionary<Type, MethodInfo>();

	public static MethodInfo GetPut(Type writerType)
	{
		return PutMethods.GetOrAdd(writerType, t =>
			t.GetMethod("Put", new[] { typeof(float) }));
	}

	public static MethodInfo GetGetFloat(Type readerType)
	{
		return GetFloatMethods.GetOrAdd(readerType, t =>
			t.GetMethod("GetFloat"));
	}
}

/// <summary>按包实例暂存的烹饪时长，配弱引用防泄漏 + 定时清理</summary>
internal static class PacketCookTimeStore
{
	// 用 ConditionalWeakTable 以对象为 key、值存入包装类：键弱引用，不随包迁移产生强引用
	private sealed class CookTimeRef { public float Value; }

	private static readonly ConditionalWeakTable<object, CookTimeRef> Store = new ConditionalWeakTable<object, CookTimeRef>();

	public static void Set(object packet, float cookTime)
	{
		if (Store.TryGetValue(packet, out var existing))
		{
			existing.Value = cookTime;
			return;
		}
		Store.Add(packet, new CookTimeRef { Value = cookTime });
	}

	public static bool TryGet(object packet, out float cookTime)
	{
		if (Store.TryGetValue(packet, out var refObj))
		{
			cookTime = refObj.Value;
			return true;
		}
		cookTime = 0f;
		return false;
	}

	public static void Remove(object packet)
	{
		Store.Remove(packet);
	}
}

/// <summary>房主/自己投掷时捕获烹饪时长（ThrowGrenade 的 timeSinceSafetyLevelRemoved）</summary>
internal static class FikaPatches_ClientGrenadeThrow
{
	public static void Prefix(float timeSinceSafetyLevelRemoved)
		{
			// 权威烹饪时长：Fika 的 timeSinceSafetyLevelRemoved 在本模组走 Grenade.Init 注入引信时
			// 通常为 0/不可靠（headless 端多处实测 cook=0）。优先取模组自身烹饪计时器的已烹饪时间。
			float modCook = GrenadeCookingManager.GetCookingTimer().IsCooking
				? Mathf.Max(0f, GrenadeCookingManager.GetCookingTimer().GetCookingTime())
				: 0f;
			float cook = Mathf.Max(modCook, timeSinceSafetyLevelRemoved);
			if (cook > 0f)
			{
				Plugin.NextCookTime = cook;
				Plugin.log.LogInfo($"[CG-Fika][Throw] 本次投掷 cook={cook:F2}s (mod={modCook:F2}, fika={timeSinceSafetyLevelRemoved:F2})");
			}
		}
}

/// <summary>序列化时在包尾写入烹饪时长（无条件写入以保持字节流一致，0 表示未烹饪），随后清零</summary>
internal static class FikaPatches_GrenadePacketSerialize
{
	public static void Postfix(object writer)
	{
		if (writer == null) return;

		var put = GrenadePacketCodecCache.GetPut(writer.GetType());
		if (put == null) return;

		put.Invoke(writer, new object[] { Plugin.NextCookTime });
		Plugin.NextCookTime = 0f;
	}
}

/// <summary>反序列化时按包实例暂存烹饪时长</summary>
internal static class FikaPatches_GrenadePacketDeserialize
{
	public static void Postfix(object __instance, object reader)
	{
		if (__instance == null || reader == null) return;

		var getFloat = GrenadePacketCodecCache.GetGetFloat(reader.GetType());
		if (getFloat == null) return;

		var cookTime = (float)getFloat.Invoke(reader, null);
		PacketCookTimeStore.Set(__instance, cookTime);
	}
}

/// <summary>按 Observed controller 实例暂存注入烹饪时长（controller 弱引用防泄漏，按实例隔离多人并发）</summary>
internal static class ObservedControllerCookStore
{
	private sealed class ControllerRef { public float Value; }

	// 同一把锁保护读写，确保一个 controller 的绑定与读取原子
	private static readonly object StoreLock = new object();
	private static readonly ConditionalWeakTable<object, ControllerRef> ControllerCookTimes =
		new ConditionalWeakTable<object, ControllerRef>();

	public static void Set(object controller, float cookTime)
	{
		if (controller == null) return;
		lock (StoreLock)
		{
			if (ControllerCookTimes.TryGetValue(controller, out var existing))
			{
				existing.Value = cookTime;
				return;
			}
			ControllerCookTimes.Add(controller, new ControllerRef { Value = cookTime });
		}
	}

	public static float Take(object controller)
	{
		if (controller == null) return 0f;
		lock (StoreLock)
		{
			float value = 0f;
			if (ControllerCookTimes.TryGetValue(controller, out var refObj))
			{
				value = refObj.Value;
				ControllerCookTimes.Remove(controller);   // 取走即清，避免陈旧值串台
			}
			return value;
		}
	}

	public static void Remove(object controller)
	{
		if (controller == null) return;
		lock (StoreLock)
		{
			ControllerCookTimes.Remove(controller);
		}
	}
}

/// <summary>执行包时把烹饪时长绑定到对应的 Observed controller 实例，供 SpawnGrenade 注入</summary>
internal static class FikaPatches_GrenadePacketExecute
{
	// Execute 与 SpawnGrenade 是同步串行调用，但为不依赖"主线程串行"的隐含假设，
	// 将 cook 暂存到目标 controller 实例上，ObservedSpawn 通过 __instance 精确取回，避免全局单值串台。

	// 反射缓存：从 player 上取 HandsController 属性
	private static readonly ConcurrentDictionary<Type, PropertyInfo> HandsControllerProps = new ConcurrentDictionary<Type, PropertyInfo>();

	public static void Prefix(object __instance, object player)
		{
			if (__instance == null || player == null)
			{
				Plugin.log.LogWarning("[CG-Fika][Execute] __instance 或 player 为 null，跳过 cook 绑定");
				return;
			}
			if (!PacketCookTimeStore.TryGet(__instance, out var cookTime))
			{
				Plugin.log.LogDebug("[CG-Fika][Execute] 包内未检出 cookTime（可能未烹饪）");
				return;
			}

			// 通过 player.HandsController 拿到本次 spawning 的目标 controller
			var handsController = GetHandsController(player);
			if (handsController == null)
			{
				Plugin.log.LogWarning($"[CG-Fika][Execute] 检出 cookTime={cookTime:F2}，但 player.HandsController 为 null，无法绑定（headless 无本地玩家的同步断点之一）");
				return;
			}

			Plugin.log.LogInfo($"[CG-Fika][Execute] 绑定 cook={cookTime:F2}s 到 HandsController={handsController.GetType().Name}");
			ObservedControllerCookStore.Set(handsController, cookTime);
		}

	private static object GetHandsController(object player)
	{
		var t = player.GetType();
		if (HandsControllerProps.TryGetValue(t, out var cached)) return cached?.GetValue(player);

		var prop = t.GetProperty("HandsController", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
		HandsControllerProps.TryAdd(t, prop);
		return prop?.GetValue(player);
	}

	public static void Postfix(object __instance, object player)
	{
		// 清理本次包实例暂存
		if (__instance != null)
		{
			PacketCookTimeStore.Remove(__instance);
		}
		// 清理 controller 绑定（弱引用即便不清也不泄漏，主动清理避免陈旧值）
		if (player != null)
		{
			var handsController = GetHandsController(player);
			if (handsController != null) ObservedControllerCookStore.Remove(handsController);
		}
	}
}

/// <summary>队友端 Observed 控制器生成手雷时注入烹饪时长（从 controller 实例精确取回，天然隔离多人并发）</summary>
internal static class FikaPatches_ObservedGrenadeSpawn
{
	public static void Prefix(object __instance, ref float timeSinceSafetyLevelRemoved)
		{
			// __instance 是当前 spawn 手雷的 target controller；从它取回绑定 cook
			if (__instance == null) return;

			float oldVal = timeSinceSafetyLevelRemoved;
			float cook = ObservedControllerCookStore.Take(__instance);
			if (cook > 0f)
			{
				timeSinceSafetyLevelRemoved = cook;
				Plugin.log.LogInfo($"[CG-Fika][SpawnGrenade] 注入 cook: {oldVal:F2} -> {cook:F2}s (controller={__instance.GetType().Name})");
			}
			else
			{
				Plugin.log.LogWarning($"[CG-Fika][SpawnGrenade] 未取到 cook（controller={__instance.GetType().Name}），手雷将用默认引信 {oldVal:F2}s");
			}
		}
}