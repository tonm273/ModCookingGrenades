using System;
using System.Reflection;
using CookingGrenades.Utils;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace CookingGrenades.Patches;

public class PlayerGrenadeHandsControllerHandleFireInputPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(EFT.Player.GrenadeHandsController), "HandleFireInput", (Type[])null, (Type[])null);
	}

	[PatchPostfix]
	public static void PatchPostfix(EFT.Player.GrenadeHandsController __instance)
	{
		IAnimator animator = __instance.FirearmsAnimator.Animator;
		GrenadeCookingTimer cookingTimer = GrenadeCookingManager.GetCookingTimer();
		Plugin.log.LogInfo($"[HandleFireInput] WaitingForLowThrow={__instance.WaitingForLowThrow}, IsCooking={cookingTimer.IsCooking}, AnimPullRingDone={AnimationUtils.IsRemovePullRingCompleted(animator)}");
		if (__instance.WaitingForLowThrow && !cookingTimer.IsCooking && AnimationUtils.IsRemovePullRingCompleted(animator))
		{
			Plugin.log.LogInfo($"[HandleFireInput] 开始温雷（低抛）");
			GrenadeCookingHelper.StartCookingWithLeverSound(__instance);
		}
	}
}
