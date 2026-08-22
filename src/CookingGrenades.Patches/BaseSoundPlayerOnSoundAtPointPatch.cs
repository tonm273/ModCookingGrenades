using System;
using System.Reflection;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace CookingGrenades.Patches;

public class BaseSoundPlayerOnSoundAtPointPatch : ModulePatch
{
	public static BaseSoundPlayer HaveToNotRunFuseSound;

	protected override MethodBase GetTargetMethod()
	{
		// SPT 4.1 中是显式接口实现：IEventsConsumer.OnSoundAtPoint(String)
		// 显式接口实现是非公开的，需要通过接口映射找到
		System.Type baseSoundPlayerType = typeof(BaseSoundPlayer);
		System.Type eventsConsumerInterface = null;

		foreach (var iface in baseSoundPlayerType.GetInterfaces())
		{
			if (iface.FullName != null && iface.FullName.IndexOf("IEventsConsumer", StringComparison.Ordinal) >= 0)
			{
				eventsConsumerInterface = iface;
				break;
			}
		}

		if (eventsConsumerInterface != null)
		{
			var interfaceMap = baseSoundPlayerType.GetInterfaceMap(eventsConsumerInterface);
			for (int i = 0; i < interfaceMap.InterfaceMethods.Length; i++)
			{
				var ifaceMethod = interfaceMap.InterfaceMethods[i];
				if (ifaceMethod.Name == "OnSoundAtPoint")
				{
					var targetMethod = interfaceMap.TargetMethods[i];
					Plugin.log.LogInfo($"[SoundPatch] 找到目标: {targetMethod.DeclaringType?.Name}.{targetMethod.Name} (通过 {eventsConsumerInterface.Name})");
					return targetMethod;
				}
			}
		}

		// 回退：直接找 SoundAtPointEventHandler(String)
		var fallback = AccessTools.Method(baseSoundPlayerType, "SoundAtPointEventHandler", new[] { typeof(string) });
		if (fallback != null)
		{
			Plugin.log.LogInfo($"[SoundPatch] 回退使用: SoundAtPointEventHandler(String)");
			return fallback;
		}

		Plugin.log.LogWarning("[SoundPatch] 未找到 OnSoundAtPoint 方法，保险丝静音功能不可用");
		return null;
	}

	[PatchPrefix]
	public static bool PatchPrefix(BaseSoundPlayer __instance, string StringParam)
	{
		if ((UnityEngine.Object)(object)HaveToNotRunFuseSound == (UnityEngine.Object)(object)__instance && StringParam == "SndFuse")
		{
			HaveToNotRunFuseSound = null;
			return false;
		}
		return true;
	}
}
