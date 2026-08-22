using System;
using System.Reflection;
using EFT;
using EFT.InputSystem;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace CookingGrenades.Patches;

public class EftGamePlayerOwnerTranslateCommandPatch : ModulePatch
{
	// 缓存 ETranslateResult 类型与"已处理"常量，避免每次输入都 TypeByName + Enum.ToObject
	private static readonly Type TranslateResultType = AccessTools.TypeByName("ETranslateResult");
	private static readonly object BlockedResult = Enum.ToObject(TranslateResultType, 2);

	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(EftGamePlayerOwner), "TranslateCommand", (Type[])null, (Type[])null);
	}

	[PatchPrefix]
	public static bool PatchPrefix(EftGamePlayerOwner __instance, ECommand command, ref object __result)
	{
		if (GrenadeCookingManager.Timer.IsCooking)
		{
			switch ((int)command - 36)
			{
			case 0:
			case 1:
			case 4:
			case 5:
			case 6:
			case 7:
			case 10:
			case 11:
			case 12:
			case 13:
			case 14:
			case 15:
			case 16:
			case 19:
			case 24:
				__result = BlockedResult;
				return false;
			}
		}
		return true;
	}
}