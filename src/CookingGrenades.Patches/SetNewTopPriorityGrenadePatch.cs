using System;
using System.Linq;
using System.Reflection;
using CookingGrenades.Config;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace CookingGrenades.Patches;

/// <summary>
/// 修复"投雷后偏好手雷被原生重置为其他类型"的问题。
/// 原生 FastAccessGrenadeItemView.OnItemRemoved 在投掉当前偏好手雷后调用
/// SetNewTopPriorityGrenade，把 TopPriorityGrenade 重置为按 ThrowType 排序的第一个手雷
/// （往往不是用户上次在轮盘选的那类），导致"投完第一种，G 键换雷"要反复再开轮盘。
/// 本 Postfix 在原生重置后，若仍有剩余手雷与用户最近轮盘选择(GrenadeWheel.PreferredTemplateId)
/// 同模板，则把偏好重新指回该模板的剩余手雷，并刷新生效中的手雷快捷槽图标。
/// 若偏好模板已无剩余，则保持原生结果（回退到排序首位）。
/// </summary>
public class SetNewTopPriorityGrenadePatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(FastAccessGrenadeItemView), "SetNewTopPriorityGrenade");
	}

	[PatchPostfix]
	public static void PatchPostfix(FastAccessGrenadeItemView __instance)
	{
		try
		{
			// 轮盘未选择过手雷或功能禁用时不干预
			if (string.IsNullOrEmpty(GrenadeWheel.PreferredTemplateId) ||
				!ConfigManager.EnableGrenadeWheel.Value)
			{
				return;
			}

			var controller = Traverse.Create(__instance)
				.Field("InventoryController").GetValue<InventoryController>();
			if (controller == null) return;
			var equipment = controller.Inventory?.Equipment;
			if (equipment == null) return;

			// 查剩余手雷中是否仍有偏好模板（原生重新排序后的列表）
			var remaining = controller.GetThrowablePriorityGrenadesList();
			if (remaining == null || remaining.Count == 0) return;
			var target = remaining.FirstOrDefault(tw => tw.StringTemplateId == GrenadeWheel.PreferredTemplateId);
			if (target == null) return; // 偏好模板已投完 → 保持原生结果
			if (equipment.TopPriorityGrenade == target) return;

			equipment.TopPriorityGrenade = target;

			Plugin.log.LogInfo($"[SetNewTopPriorityGrenadePatch] 投雷后重新锁定偏好模板: {target.ShortName.Localized()}");

			// 刷新手雷快捷槽图标与提示（原生已显示按 ThrowType 排序的第一个）。
			// 注意：SetItem 每次都会创建新的 ItemView 且不移除旧视图，原生 SetNewTopPriorityGrenade
			// 在 SetItem 前先调用了 RemoveItemView 故不堆叠；此处若直接 SetItem 会与原生创建的
			// 视图叠加成"多个图标叠在一起"。因此先 RemoveItemView 清掉原生视图，再刷新为偏好手雷。
			// itemUiContext 通过反射获取，可能为 null（字段名变化）→ 判空跳过 UI 刷新，偏好锁定不受影响
			var itemUiContext = Traverse.Create(__instance).Field("ItemUiContext").GetValue<ItemUiContext>();
			if (itemUiContext != null)
			{
				__instance.RemoveItemView();
				__instance.SetItem(target, controller, itemUiContext);
				__instance.ShowInfoPanel(target);
			}
			Traverse.Create(__instance).Field("Item").SetValue(target);
		}
		catch (Exception e)
		{
			Plugin.log.LogError($"[SetNewTopPriorityGrenadePatch] {e.Message}");
		}
	}
}