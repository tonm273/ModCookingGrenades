using System;
using System.Linq;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace CookingGrenades.Patches;

/// <summary>
/// SPT 4.1 中手雷扔出时走 Grenade.Init，timeSpent=0 不是引信时间
/// 引信时间由 ThrowWeap.GetExplDelay 决定
/// 此 Patch 在 Grenade.Init 时检测温雷，通过 ThrowWeapGetExplDelayPatch 注入缩短后的引信时间
/// </summary>
public class GrenadeInitPatch : ModulePatch
{
	private static readonly FieldInfo ImpactFuseTimeField = AccessTools.Field(typeof(Grenade), "_timeSpent");

	protected override MethodBase GetTargetMethod()
	{
		MethodInfo[] methods = AccessTools.GetDeclaredMethods(typeof(Grenade))
			.Where(m => m.Name == "Init")
			.ToArray();

		Plugin.log.LogInfo($"[GrenadeInit] 找到 {methods.Length} 个 Init 方法:");
		foreach (var m in methods)
		{
			var ps = m.GetParameters();
			Plugin.log.LogInfo($"  - Init({string.Join(", ", ps.Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
		}

		foreach (var m in methods)
		{
			var ps = m.GetParameters();
			if (ps.Length >= 1 && ps.Any(p => p.ParameterType == typeof(float)))
			{
				Plugin.log.LogInfo($"[GrenadeInit] 选择: Init({string.Join(", ", ps.Select(p => p.ParameterType.Name))})");
				return m;
			}
		}

		if (methods.Length > 0)
		{
			return methods[0];
		}

		Plugin.log.LogError("[GrenadeInit] 未找到 Init 方法");
		return null;
	}

	[PatchPrefix]
	public static void PatchPrefix(Grenade __instance, object[] __args, MethodBase __originalMethod)
	{
		ParameterInfo[] ps = __originalMethod.GetParameters();

		// 找到 ThrowWeap 参数
		ThrowWeap throwWeap = null;
		int floatIdx = -1;
		for (int i = 0; i < ps.Length && i < __args.Length; i++)
		{
			if (ps[i].ParameterType == typeof(ThrowWeap) && __args[i] is ThrowWeap tw)
			{
				throwWeap = tw;
			}
			if (ps[i].ParameterType == typeof(float) && floatIdx < 0)
			{
				floatIdx = i;
			}
		}

		GrenadeCookingTimer cookingTimer = GrenadeCookingManager.GetCookingTimer();

		if (cookingTimer.IsCooking && throwWeap != null)
		{
			// 正在温雷：获取原始引信时间
			float originalDelay = throwWeap.GetExplDelay;
			float cookedTime = cookingTimer.GetCookingTime();
			float newDelay = Math.Max(0.1f, originalDelay - cookedTime);

			Plugin.log.LogInfo($"[GrenadeInit] 温雷注入: 原始引信={originalDelay:F2}s, 已烹饪={cookedTime:F2}s, 新引信={newDelay:F2}s");

			// 通过 ThrowWeapGetExplDelayPatch 的字典注入新的引信时间
			ThrowWeapGetExplDelayPatch.SetExplDelay(throwWeap, newDelay);

			// 广播烹饪事件（供 CGFika 等插件订阅）
			CGEvents.Fire(cookedTime);

			// 只重置计时器，不隐藏控制器（隐藏会导致状态异常→人物锁死）
			EFT.Player.GrenadeHandsController controller = cookingTimer.Controller;
			if (controller != null)
			{
				cookingTimer.Reset(controller);
			}
		}
		else
		{
			float originalTime = floatIdx >= 0 ? (float)__args[floatIdx] : 0f;
			Plugin.log.LogInfo($"[GrenadeInit] 正常初始化 timeSpent={originalTime:F2}s");
		}
	}

	[PatchPostfix]
	public static void PatchPostfix(Grenade __instance, object[] __args, MethodBase __originalMethod)
	{
		ParameterInfo[] ps = __originalMethod.GetParameters();
		for (int i = 0; i < ps.Length && i < __args.Length; i++)
		{
			if (ps[i].ParameterType == typeof(float) && __args[i] is float)
			{
				ImpactFuseTimeField.SetValue(__instance, (float)__args[i]);
				break;
			}
		}
	}
}
