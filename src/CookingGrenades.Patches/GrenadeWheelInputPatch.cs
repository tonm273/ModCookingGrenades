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
		// 单例若被销毁（异常）则自动重建（WheelBase 统一入口，避免重复创建逻辑）
		var wheel = WheelBase<GrenadeWheel>.GetOrCreateInstance<GrenadeWheel>();

		// 轮盘禁用时放行所有输入，让游戏原生处理
		if (!ConfigManager.EnableGrenadeWheel.Value)
			return true;

		// 轮盘打开时屏蔽所有游戏输入（防止移动/开枪/切枪等干扰轮盘操作）
		if (wheel.IsOpen)
		{
			__result = BlockedResult;
			return false;
		}

		// 注意：此处不应拦截 ThrowGrenade(13) / PressThrowGrenade(14) / ReloadWeapon(15) 等命令，
		// 否则会破坏"点按 G 直接投雷"与"换弹"功能。屏蔽原生手雷选择栏统一交给
		// GrenadeSelectorPatch（拦截 GrenadeSelector.ShowGrenades 阻止 UI 显示），投雷/换弹命令保持放行。
		return true;
	}
}