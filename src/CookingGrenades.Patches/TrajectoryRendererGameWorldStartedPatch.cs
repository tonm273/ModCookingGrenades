using System.Reflection;
using Comfort.Common;
using EFT;
using SPT.Reflection.Patching;
using UnityEngine;

namespace CookingGrenades.Patches;

/// <summary>
/// GameWorld.OnGameStarted 之后挂载 TrajectoryRenderer 组件到 MainPlayer.gameObject
/// 和 VisualAssist 的 GrenadeAssistGameWorldStartedPostfixPatch 保持一致
/// </summary>
public class TrajectoryRendererGameWorldStartedPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(GameWorld).GetMethod(nameof(GameWorld.OnGameStarted));
    }

    [PatchPostfix]
    public static void Postfix(GameWorld __instance)
    {
        var player = __instance.MainPlayer;
        if (player == null)
        {
            Plugin.log.LogWarning("[Trajectory] GameWorld.OnGameStarted: MainPlayer is null");
            return;
        }

        // 挂载到 MainPlayer.gameObject（和 VisualAssist 一致）
        var existing = player.gameObject.GetComponent<TrajectoryRenderer>();
        if (existing == null)
        {
            existing = player.gameObject.AddComponent<TrajectoryRenderer>();
            Plugin.log.LogInfo($"[Trajectory] 已挂载到 MainPlayer ({player.Profile.Nickname})");
        }
        else
        {
            Plugin.log.LogInfo("[Trajectory] 组件已存在，跳过挂载");
        }
    }
}

/// <summary>
/// Player.Dispose 时销毁 TrajectoryRenderer
/// </summary>
public class TrajectoryRendererPlayerDisposePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(Player).GetMethod(nameof(Player.Dispose));
    }

    [PatchPostfix]
    public static void Postfix(Player __instance)
    {
        if (!__instance.IsYourPlayer) return;

        var comp = __instance.gameObject.GetComponent<TrajectoryRenderer>();
        if (comp != null)
        {
            Object.DestroyImmediate(comp);
            Plugin.log.LogInfo("[Trajectory] Player.Dispose: TrajectoryRenderer 已销毁");
        }
    }
}
