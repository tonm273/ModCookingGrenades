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
		// 投掷/温雷动作启动时（早于原生 SetNewTopPriorityGrenade 重置偏好），
		// 提前记录当前手持手雷模板，使 SetNewTopPriorityGrenadePatch 能锁定"正在投的这类"，
		// 而不是陈旧的轮盘选择（修复连投下一类时被自动换成其他雷的问题）。
		var item = __instance.Item;
		if (item != null && !string.IsNullOrEmpty(item.StringTemplateId))
		{
			GrenadeWheel.PreferredTemplateId = item.StringTemplateId;
		}

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
