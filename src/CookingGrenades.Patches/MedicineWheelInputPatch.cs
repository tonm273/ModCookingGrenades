using System;
using System.Reflection;
using CookingGrenades.Config;
using EFT;
using EFT.InputSystem;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CookingGrenades.Patches;

/// <summary>
/// 医药轮盘输入补丁：轮盘打开时屏蔽所有游戏输入（防止移动/开枪/切枪干扰选择），
/// 与手雷轮盘输入补丁互不冲突。
/// </summary>
public class MedicineWheelInputPatch : ModulePatch
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
		var wheel = WheelBase<MedicineWheel>.GetOrCreateInstance<MedicineWheel>();

		// 轮盘禁用时放行所有输入，让游戏原生处理
		if (!ConfigManager.EnableMedicineWheel.Value)
			return true;

		// 轮盘打开时屏蔽所有游戏输入
		if (wheel.IsOpen)
		{
			__result = BlockedResult;
			return false;
		}

		return true;
	}
}
