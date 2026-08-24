using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CookingGrenades.Config;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace CookingGrenades.Patches;

/// <summary>
/// 屏蔽游戏原生"按住 G 弹出的手雷选择栏（GrenadeSelector）"。
/// 原生选择栏与模组轮盘叠加，故在模组启用时直拦截其唯一显示入口 ShowGrenades，
/// 返回已完成的空任务使上方 await 立即结束且不显示 UI。
/// 点按 G 直接投雷与换弹不经过 ShowGrenades，因此完全不受影响（不会再出现投雷/换弹被误拦截的副作用）。
/// 注意：原生列表的显示由 ShowGrenades 内部 ShowGameObject()+创建视图触发，滚轮(ScrollNext/Previous)
/// 只在列表内切换选中项，屏蔽滚轮无法阻止列表弹出——必须拦 ShowGrenades。
/// </summary>
public class GrenadeSelectorPatch : ModulePatch
{
	// 用反射按类型名匹配全局定位，规避命名空间限定失效（此前 TypeByName("EFT.GrenadeSelector") 返回 null 导致 TargetMethod is null）
	private static readonly Type GrenadeSelectorType = AccessTools.AllTypes()
		.FirstOrDefault(t => t.Name == "GrenadeSelector");

	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(GrenadeSelectorType, "ShowGrenades");
	}

	[PatchPrefix]
	public static bool PatchPrefix(ref object __result)
	{
		// 仅当手雷轮盘启用时屏蔽原生选择栏；禁用则交给游戏原生处理
		if (ConfigManager.EnableGrenadeWheel.Value)
		{
			__result = Task.FromResult<ThrowWeap>(null);
			return false;
		}
		return true;
	}
}