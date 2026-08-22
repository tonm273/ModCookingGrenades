using System;
using System.Reflection;
using CookingGrenades.Utils;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace CookingGrenades.Patches;

public class PlayerGrenadeHandsControllerHandleAltFireInputPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(EFT.Player.GrenadeHandsController), "HandleAltFireInput", (Type[])null, (Type[])null);
	}

	[PatchPostfix]
	public static void PatchPostfix(EFT.Player.GrenadeHandsController __instance)
	{
		IAnimator animator = __instance.FirearmsAnimator.Animator;
		GrenadeCookingTimer cookingTimer = GrenadeCookingManager.GetCookingTimer();
		Plugin.log.LogInfo($"[HandleAltFireInput] WaitingForHighThrow={__instance.WaitingForHighThrow}, IsCooking={cookingTimer.IsCooking}, AnimPullRingDone={AnimationUtils.IsRemovePullRingCompleted(animator)}");
		if (__instance.WaitingForHighThrow && !cookingTimer.IsCooking && AnimationUtils.IsRemovePullRingCompleted(animator))
		{
			Plugin.log.LogInfo($"[HandleAltFireInput] 开始温雷（高抛）");
			GrenadeCookingHelper.StartCookingWithLeverSound(__instance);
		}
	}
}
