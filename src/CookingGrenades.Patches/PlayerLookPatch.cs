using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace CookingGrenades.Patches;

/// <summary>
/// 占位（已弃用）。第一人称视角的解耦改由 WheelBase 的摄像机视角冻结实现：
/// 打开轮盘时缓存世界相机旋转、每帧还原，从而"视角不随鼠标转动"，人物模型则完全不动。
/// 保留这两个空补丁仅为兼容旧注册（不冻结人物、不拦截任何 look）。
/// </summary>
public class PlayerLookPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(Player), nameof(Player.Look));
	}

	// 始终放行：不再冻结人物 look
	[PatchPrefix]
	public static bool Prefix(Player __instance) => true;
}

public class PlayerMouseLookPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(Player), nameof(Player.MouseLook));
	}

	// 始终放行：不再冻结人物 look（Spirit 激活下本方法本身也不生效）
	[PatchPrefix]
	public static bool Prefix(Player __instance) => true;
}