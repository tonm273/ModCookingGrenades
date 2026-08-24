using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using CookingGrenades.Config;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;
using UnityEngine.UI;

namespace CookingGrenades;

public class GrenadeWheel : WheelBase<GrenadeWheel>
{
    /// <summary>
    /// 用户最近一次在轮盘选中的手雷模板（TemplateId）。
    /// 用于在原生 SetNewTopPriorityGrenade 把 TopPriorityGrenade 重置为其他类型时，
    /// 把偏好重新指回同一模板的剩余手雷，避免"投完第一种后又换雷"反复开轮盘。
    /// </summary>
    public static string PreferredTemplateId;

    private class GroupedGrenade
    {
        public string TemplateId;
        public string Name;       // 槽位显示名（ShortName 优先）
        public string FullName;   // 中心标签完整名
        public int Count;
        public Color Color;
        public ThrowWeap FirstItem;
    }

    private readonly List<GroupedGrenade> _displayedGrenades = new List<GroupedGrenade>();

    // ── WheelBase 抽象实现 ──────────────────────────────────────

    protected override string WheelLogName => "GrenadeWheel";

    protected override bool EnabledByConfig => ConfigManager.EnableGrenadeWheel.Value;

    protected override KeyCode HoldKey => ConfigManager.GrenadeWheelKey.Value;

    protected override bool IsOtherWheelOpen() =>
        MedicineWheel.Instance != null && MedicineWheel.Instance.IsOpen;

    protected override int DisplayCount => _displayedGrenades.Count;

    protected override string GetSlotName(int index) => _displayedGrenades[index].Name;

    protected override string GetSlotFullName(int index) => _displayedGrenades[index].FullName;

    protected override int GetSlotCount(int index) => _displayedGrenades[index].Count;

    protected override void LoadSlotIcon(int index, Image iconImage, TMPro.TextMeshProUGUI fallbackText)
    {
        var dg = _displayedGrenades[index];
        // 与医药轮盘一致：仅异步加载游戏内建图标，成功则覆盖显示；失败/无图标则保留文字回退
        LoadBuiltInIconAsync(dg.TemplateId, dg.FirstItem, iconImage, fallbackText);
    }

    protected override void OnSelect(int index) => EquipGrenade(_displayedGrenades[index]);

    /// <summary>
    /// 打开轮盘时的初始选中：优先当前偏好手雷（TopPriorityGrenade），找不到则 -1。
    /// 先按实例引用匹配，再按模板 ID 匹配（偏好手雷可能是同模板的另一实例），
    /// 使模组圆环初始高亮与原版手雷栏当前选择的偏好一致。
    /// </summary>
    protected override int GetInitialSelectedIndex()
    {
        try
        {
            var equipment = _player?.InventoryController?.Inventory?.Equipment;
            if (equipment == null) return -1;
            var top = equipment.TopPriorityGrenade;
            if (top == null) return -1;
            var topTemplate = top.StringTemplateId;
            for (int i = 0; i < _displayedGrenades.Count; i++)
            {
                var dg = _displayedGrenades[i];
                if (ReferenceEquals(dg.FirstItem, top))
                    return i;
                if (!string.IsNullOrEmpty(topTemplate) && dg.TemplateId == topTemplate)
                    return i;
            }
        }
        catch (Exception e)
        {
            Plugin.log.LogWarning($"[GrenadeWheel] 读取偏好手雷失败: {e.Message}");
        }
        return -1;
    }

    protected override void ScanItems()
    {
        Plugin.log.LogInfo("[GrenadeWheel] 使用 GetThrowablePriorityGrenadesList 扫描手雷...");

        var flatGrenades = new List<ThrowWeap>();

        try
        {
            var allGrenades = _player.InventoryController.GetThrowablePriorityGrenadesList();
            if (allGrenades != null)
                flatGrenades.AddRange(allGrenades);
        }
        catch (Exception e)
        {
            Plugin.log.LogWarning($"[GrenadeWheel] GetThrowablePriorityGrenadesList 异常: {e.Message}，使用后备扫描模式");

            try
            {
                // 后备：遍历背包容器
                var equipment = _player.Inventory?.Equipment;
                if (equipment != null)
                {
                    foreach (EquipmentSlot slotVal in Enum.GetValues(typeof(EquipmentSlot)))
                    {
                        var slot = equipment.GetSlot(slotVal);
                        if (slot?.ContainedItem is ThrowWeap tw)
                        {
                            flatGrenades.Add(tw);
                            continue;
                        }

                        if (slot?.ContainedItem is IContainer container)
                        {
                            foreach (var item in container.Items)
                            {
                                if (item is ThrowWeap tw2)
                                    flatGrenades.Add(tw2);
                            }
                        }
                    }
                }
            }
            catch (Exception e2)
            {
                Plugin.log.LogError($"[GrenadeWheel] 后备扫描也异常: {e2.Message}");
            }
        }

        _displayedGrenades.Clear();
        if (flatGrenades.Count > 0)
        {
            _displayedGrenades.AddRange(flatGrenades
                .GroupBy(tw => tw.StringTemplateId ?? "unknown")
                .Select(g =>
                {
                    var first = g.First();
                    // Name/ShortName 返回的是本地化 key，必须 .Localized() 才能显示中文（与医药轮盘一致）
                    return new GroupedGrenade
                    {
                        TemplateId = g.Key,
                        Name = string.IsNullOrEmpty(first.ShortName) ? first.Name.Localized() : first.ShortName.Localized(),
                        FullName = first.Name.Localized(),
                        Count = g.Count(),
                        Color = GetGrenadeColor(first),
                        FirstItem = first
                    };
                }));

            foreach (var dg in _displayedGrenades)
                Plugin.log.LogInfo($"[GrenadeWheel] 分组: {dg.FullName} x{dg.Count}");
        }

        if (_displayedGrenades.Count == 0)
        {
            Plugin.log.LogInfo("[GrenadeWheel] 背包中没有手雷，但轮盘仍会显示");
        }
    }

    // ── 选中行为 ───────────────────────────────────────────────

    private void EquipGrenade(GroupedGrenade group)
    {
        if (_player == null || group?.FirstItem == null) return;

        try
        {
            // 记录用户选中的手雷模板，供 SetNewTopPriorityGrenadePatch 在投雷后被原生重置偏好时重新锁定
            PreferredTemplateId = group.TemplateId;

            // 始终设置 TopPriorityGrenade 偏好，让原版 G 键也能切到选中的手雷
            var equipment = _player.InventoryController?.Inventory?.Equipment;
            if (equipment != null)
                equipment.TopPriorityGrenade = group.FirstItem;

            Plugin.log.LogInfo($"[GrenadeWheel] 已设为偏好手雷: {group.Name}");

            // 判断当前是否已有手雷在手上
            bool holdingGrenade = _player.HandsController is Player.GrenadeHandsController;
            bool shouldEquipImmediately;

            if (holdingGrenade)
            {
                // 手上已有手雷 → 使用 SwitchImmediatelyWhenHolding 配置
                shouldEquipImmediately = ConfigManager.SwitchImmediatelyWhenHolding.Value;
            }
            else
            {
                // 手上没有手雷 → 使用 EquipImmediatelyOnSelect 配置
                shouldEquipImmediately = ConfigManager.EquipImmediatelyOnSelect.Value;
            }

            if (shouldEquipImmediately)
            {
                Plugin.log.LogInfo($"[GrenadeWheel] 立即{(holdingGrenade ? "切换" : "装备")}: {group.Name}");
                _player.Proceed(group.FirstItem, (Callback<IGrenadeController>)null, true);
            }
            else
            {
                Plugin.log.LogInfo($"[GrenadeWheel] 仅设为偏好，松开 G 后再按 G 键{(holdingGrenade ? "切换到该手雷" : "掏出")}");
            }
        }
        catch (Exception e)
        {
            Plugin.log.LogError($"[GrenadeWheel] 装备失败: {e.Message}");
        }
    }

    // ── 图标加载 ───────────────────────────────────────────────

    /// <summary>
    /// 异步加载游戏内建图标（共享缓存：主菜单已预加载则立即返回）。
    /// 加载成功后显示图标并隐藏文字；失败/无图标时保留文字显示（回退）。
    /// </summary>
    private async void LoadBuiltInIconAsync(string templateId, ThrowWeap item, Image iconImage, TMPro.TextMeshProUGUI fallbackText)
    {
        if (string.IsNullOrEmpty(templateId) || item == null || iconImage == null) return;
        var sprite = await ItemIconCache.GetOrLoadAsync(item, templateId);
        if (sprite == null)
        {
            Plugin.log.LogInfo($"[GrenadeWheel] 无内建图标，使用文字显示: {item.ShortName.Localized()}");
            return;
        }
        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.enabled = true;
            if (fallbackText != null) fallbackText.enabled = false;
        }
    }

    private static Color GetGrenadeColor(ThrowWeap grenade)
    {
        string tid = grenade.StringTemplateId;
        if (tid == null) return new Color(0.6f, 0.6f, 0.6f);

        if (tid.Contains("5710c24a")) return new Color(0.85f, 0.7f, 0.15f);
        if (tid.Contains("5448be9a")) return new Color(0.5f, 0.65f, 0.3f);
        if (tid.Contains("58d3db53")) return new Color(0.55f, 0.7f, 0.25f);
        if (tid.Contains("5e32f56f")) return new Color(0.6f, 0.45f, 0.15f);
        if (tid.Contains("5e340dcd")) return new Color(0.65f, 0.4f, 0.1f);
        if (tid.Contains("617fd91e")) return new Color(0.5f, 0.6f, 0.4f);
        if (tid.Contains("618a431d")) return new Color(0.7f, 0.45f, 0.2f);
        if (tid.Contains("617aa4dd")) return new Color(0.35f, 0.7f, 0.35f);
        if (tid.Contains("619256e5")) return new Color(0.9f, 0.85f, 0.6f);
        if (tid.Contains("5a2a57cf")) return new Color(0.4f, 0.65f, 0.5f);
        if (tid.Contains("66dae7cb")) return new Color(0.7f, 0.5f, 0.4f);
        if (tid.Contains("5a0c2771")) return new Color(0.95f, 0.9f, 0.45f);

        return new Color(0.6f, 0.6f, 0.6f);
    }
}