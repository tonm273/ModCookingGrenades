using System;
using System.Reflection;
using Comfort.Common;
using CookingGrenades.Config;
using CookingGrenades;
using EFT;
using EFT.InputSystem;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CookingGrenades.Patches;

public class GrenadeWheelInputPatch : ModulePatch
{
	// 缓存 ETranslateResult 类型与"已处理"常量，避免每次输入都 TypeByName + Enum.ToObject
	private static readonly Type TranslateResultType = AccessTools.TypeByName("ETranslateResult");
	private static readonly object BlockedResult = Enum.ToObject(TranslateResultType, 2);

	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(EftGamePlayerOwner), "TranslateCommand");
	}

	[PatchPrefix]
	public static bool PatchPrefix(EftGamePlayerOwner __instance, ECommand command, ref object __result)
	{
		var wheel = GrenadeWheel.Instance;
		if (wheel == null)
		{
			var wheelObj = new GameObject("GrenadeWheel");
			Object.DontDestroyOnLoad(wheelObj);
			wheel = wheelObj.AddComponent<GrenadeWheel>();
		}

		// 轮盘禁用时放行所有输入，让游戏原生处理
		if (!ConfigManager.EnableGrenadeWheel.Value)
			return true;

		// 轮盘打开时屏蔽所有游戏输入（防止移动/开枪/切枪等干扰轮盘操作）
		if (wheel.IsOpen)
		{
			__result = BlockedResult;
			return false;
		}

		// 阻止游戏原生的 G 键选雷功能（避免与 Unity Input 检测冲突）。
		// 仅当轮盘触发键就是 G 时拦截，避免用户改键（如改成其他键）后原生 G 键也一并失效。
		if ((int)command == 60 && ConfigManager.GrenadeWheelKey.Value == KeyCode.G)
		{
			__result = BlockedResult;
			return false;
		}

		return true;
	}
}