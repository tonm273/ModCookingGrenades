using System;
using System.Linq;
using System.Reflection;
using CookingGrenades.Config;
using EFT;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using SPT.Reflection.Utils;
using TMPro;

namespace CookingGrenades.Patches;

public class MenuScreenPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		// 4.1 中 MainMenuController 类名已混淆/不存在；ShowScreen 有多个重载，
		// 模糊查找会命中泛型基类 ScreenController<TController,TScreen>（open generic 方法无法被 Harmony patch，
		// 报 IL Compile Error / Specified method is not supported）。
		// 改为：排除泛型类型定义 + 精确匹配主菜单控制器独有的签名 ShowScreen(EMenuType, bool)。
		Type mainMenuControllerType = PatchConstants.EftTypes.FirstOrDefault(t =>
			!t.IsGenericTypeDefinition &&
			t.GetMethods(BindingFlags.Instance | BindingFlags.Public).Any(m =>
				m.Name == "ShowScreen"
				&& m.GetParameters().Length == 2
				&& m.GetParameters()[0].ParameterType == typeof(EMenuType)
				&& m.GetParameters()[1].ParameterType == typeof(bool)));
		if (mainMenuControllerType == null)
		{
			Plugin.log.LogError("Failed to find MainMenuController type with ShowScreen(EMenuType, bool)");
			return null;
		}
		return AccessTools.Method(mainMenuControllerType, "ShowScreen", new[] { typeof(EMenuType), typeof(bool) }, null);
	}

	[PatchPostfix]
	public static void PatchPostfix(object __instance)
	{
		if (!ConfigManager.UserWarningConfirmed.Value)
		{
			string title;
			string text;
			if (IsChineseLanguage())
			{
				title = "温雷警告";
				text = "[警告]\n温雷（拔销后在手中延时持握）在现实中极度危险！\n引信延时并非绝对精确，手雷可能早于预设时间起爆。\n拔出拉环、保险销后，切勿松开保险杆——否则手雷可能在手中提前爆炸，造成重伤甚至死亡。\n你是否已阅读并理解此警告，并愿意自行承担相关责任？";
			}
			else
			{
				title = "Cooking Grenades Warning";
				text = "[WARNING]\nCooking a grenade is extremely dangerous in real life. Time-delay setting may vary and fuzes may function before prescribed times. DO NOT \"COOK OFF\" the safety lever after pull ring with safety pin extraction. This action can lead to premature detonation of the grenade leading to severe injury, death.\nDo you acknowledge this warning and accept responsibility?";
			}

			ItemUiContext.Instance.ShowMessageWindow(text, (Action)delegate
			{
				ConfigManager.UserWarningConfirmed.Value = true;
			}, (Action)delegate
			{
				ConfigManager.UserWarningConfirmed.Value = false;
			}, title, 0f, false, (TextAlignmentOptions)514);
		}
	}

	/// <summary>
	/// 判断当前游戏语言是否为中文（LocalizationManager.Instance.Culture 来自游戏语言设置，
	/// 如 ch/en/ru...，切换游戏语言重启后自动跟随）。
	/// </summary>
	private static bool IsChineseLanguage()
	{
		try
		{
			var culture = LocalizationManager.Instance?.Culture;
			return !string.IsNullOrEmpty(culture)
			       && culture.StartsWith("ch", StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}
}