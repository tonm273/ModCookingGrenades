using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using CookingGrenades.Config;
using EFT;
using EFT.InventoryLogic;
using EFT.NetworkPackets;
using UnityEngine;
using UnityEngine.UI;

namespace CookingGrenades;

/// <summary>
/// 医药轮盘（Medicine Wheel）
/// 继承 WheelBase，复用轮盘 UI/交互逻辑：长按配置键唤出，鼠标选择，松开即使用选中的药品/食物。
/// 与手雷轮盘互斥（同一时间只能打开一个），温雷期间禁用。
/// </summary>
public class MedicineWheel : WheelBase<MedicineWheel>
{
    private class GroupedMedicine
    {
        public string TemplateId;
        public string Name;       // 槽位显示名（ShortName 优先）
        public string FullName;   // 中心标签完整名
        public int Count;
        public Color Color;
        public Item FirstItem;
    }

    private readonly List<GroupedMedicine> _displayedMedicine = new List<GroupedMedicine>();

    // ── WheelBase 抽象实现 ──────────────────────────────────────

    protected override string WheelLogName => "MedicineWheel";

    protected override bool EnabledByConfig => ConfigManager.EnableMedicineWheel.Value;

    protected override KeyCode HoldKey => ConfigManager.MedicineWheelKey.Value;

    protected override bool IsOtherWheelOpen() =>
        GrenadeWheel.Instance != null && GrenadeWheel.Instance.IsOpen;

    protected override int DisplayCount => _displayedMedicine.Count;

    protected override string GetSlotName(int index) => _displayedMedicine[index].Name;

    protected override string GetSlotFullName(int index) => _displayedMedicine[index].FullName;

    protected override int GetSlotCount(int index) => _displayedMedicine[index].Count;

    protected override void LoadSlotIcon(int index, Image iconImage, TMPro.TextMeshProUGUI fallbackText)
    {
        var dm = _displayedMedicine[index];
        LoadItemIconAsync(dm.FirstItem, dm.TemplateId, iconImage, fallbackText);
    }

    protected override void OnSelect(int index) => UseMedicine(_displayedMedicine[index]);

    protected override void ScanItems()
    {
        Plugin.log.LogInfo("[MedicineWheel] 扫描药品...");
        ScanMedicine();
    }

    /// <summary>
    /// 扫描可用药品：从 Equipment 根出发，用显式栈 + 防环 + 数量上限做有限深度遍历，
    /// 过滤医疗品/食物饮水，并按配置排除安全箱与背包。
    /// 不依赖 GetPlayerItems()/GetAllItems() 等无防环的递归 API（实测可能死循环卡死）。
    /// </summary>
    private void ScanMedicine()
    {
        _displayedMedicine.Clear();

        try
        {
            bool scanBackpack = ConfigManager.MedicineWheelScanBackpack.Value;
            bool scanSecure = ConfigManager.MedicineWheelScanSecure.Value;
            bool includeFood = ConfigManager.MedicineWheelIncludeFood.Value;

            var equipment = _player.Inventory?.Equipment;
            if (equipment == null) return;

            // 安全箱/背包根对象（用于父链判断），各自 try-catch 防止 GetSlot 异常
            Item securedRoot = null;
            Item backpackRoot = null;
            try { securedRoot = equipment.GetSlot(EquipmentSlot.SecuredContainer)?.ContainedItem; } catch (Exception) { }
            try { backpackRoot = equipment.GetSlot(EquipmentSlot.Backpack)?.ContainedItem; } catch (Exception) { }

            var flat = new List<Item>();
            var visited = new List<Item>();        // 防环：ReferenceEquals 判重
            var stack = new Stack<Item>();
            stack.Push(equipment);

            const int MaxItems = 5000;              // 数量上限，防止异常情况下的无限展开
            int guard = 0;

            while (stack.Count > 0 && guard++ < MaxItems)
            {
                var cur = stack.Pop();
                if (cur == null) continue;

                // 防环：已访问过则跳过
                bool seen = false;
                for (int i = 0; i < visited.Count; i++)
                {
                    if (ReferenceEquals(visited[i], cur)) { seen = true; break; }
                }
                if (seen) continue;
                visited.Add(cur);

                // 收集药品
                if (cur is Meds || (includeFood && cur is FoodDrink))
                {
                    if (!scanSecure && IsUnderSafe(cur, securedRoot)) continue;   // 安全箱按配置
                    if (!scanBackpack && IsUnderSafe(cur, backpackRoot)) continue; // 背包按配置
                    flat.Add(cur);
                }

                // 展开容器（仅 ContainerCollection 有子容器），全部 try-catch 防异常中断
                if (cur is ContainerCollection cc)
                {
                    IEnumerable<IContainer> containers = null;
                    try { containers = cc.Containers; } catch (Exception) { continue; }
                    if (containers == null) continue;
                    foreach (var container in containers)
                    {
                        if (container == null) continue;
                        IEnumerable<Item> items = null;
                        try { items = container.Items; } catch (Exception) { continue; }
                        if (items == null) continue;
                        foreach (var child in items)
                        {
                            if (child != null) stack.Push(child);
                        }
                    }
                }
            }

            Plugin.log.LogInfo($"[MedicineWheel] 扫描完成，遍历 {visited.Count} 个物品，命中药品 {flat.Count} 个");

            _displayedMedicine.Clear();
            _displayedMedicine.AddRange(flat
                .GroupBy(it => it.TemplateId.ToString())
                .Select(g =>
                {
                    var first = g.First();
                    // Name/ShortName 返回的是本地化 key（如 "5751a25924597722c463c472 Name"），必须 .Localized() 才能显示中文
                    return new GroupedMedicine
                    {
                        TemplateId = g.Key,
                        Name = string.IsNullOrEmpty(first.ShortName) ? first.Name.Localized() : first.ShortName.Localized(),
                        FullName = first.Name.Localized(),
                        Count = g.Count(),
                        Color = GetMedicineColor(first),
                        FirstItem = first
                    };
                }));

            foreach (var dm in _displayedMedicine)
                Plugin.log.LogInfo($"[MedicineWheel] 分组: {dm.FullName} x{dm.Count}");
        }
        catch (Exception e)
        {
            Plugin.log.LogError($"[MedicineWheel] 扫描药品异常: {e.Message}");
        }
    }

    /// <summary>
    /// 判断物品是否位于指定根物品（如安全箱/背包容器）之下，向上遍历父链。
    /// 带深度上限（32 层）与异常防护（Item.Parent 在无父时会抛异常），保证必然终止。
    /// </summary>
    private static bool IsUnderSafe(Item item, Item root)
    {
        if (root == null || item == null) return false;
        var cursor = item;
        int depth = 0;
        const int MaxDepth = 32;
        while (cursor != null && depth++ < MaxDepth)
        {
            if (ReferenceEquals(cursor, root)) return true;
            Item next = null;
            try { next = cursor.Parent?.Container?.ParentItem; } catch (Exception) { break; }
            cursor = next;
        }
        return false;
    }

    // ── 选中行为 ───────────────────────────────────────────────

    /// <summary>
    /// 使用选中的药品。
    /// Meds（急救包/绷带/止痛/兴奋剂等）与 FoodDrink（食物/饮水）走 MedsController 路径，
    /// 其他可快速使用的物品走通用 QuickUse 路径。
    /// </summary>
    private void UseMedicine(GroupedMedicine group)
    {
        if (_player == null || group?.FirstItem == null) return;

        try
        {
            var item = group.FirstItem;

            if (item is Meds meds)
            {
                // 用药过滤器（对应游戏"连续治疗"语义，原版 TargetBodyParts/BodyPartsPriority 同源逻辑）：
                // ① 急救包（MedKit，补血）→ 连续治疗：BodyPartsPriority(item, true) 返回全部可治疗受伤部位，
                //    MedsController 自动逐个治疗，队列弹空（伤治完）或资源耗尽才停 —— "治疗类可以一直用"；
                // ② 止痛药/兴奋剂（Drugs/Stimulator）→ 单次：必须传具体部位（Head）。
                //    TryGetBodyPartToApply 对 Drugs 要求部位非 Common、对 StimulatorBuffs 走 Head，
                //    传 Common 会 CanApplyItem=false 导致 FailedToApply；
                // ③ 其他医疗用品（绷带/手术包等）→ 单次：Common 自动匹配伤势
                //    （NeedDequeue 对 Common 恒 true，队列弹空 → HaveToContinue false → 用一次即停）。
                if (meds is MedKit)
                {
                    if (_player.HealthController.CanApplyItem(meds, EBodyPart.Common))
                    {
                        var bodyParts = _player.HealthController.BodyPartsPriority(meds, continuousHealEnabled: true);
                        _player.Proceed(meds, bodyParts, null, 0, true);
                    }
                    else
                    {
                        Plugin.log.LogInfo($"[MedicineWheel] 当前无需治疗，跳过: {group.FullName}");
                        return;
                    }
                }
                else if (meds is Drugs || meds is Stimulator)
                {
                    _player.Proceed(meds, new OneAndList<EBodyPart>(EBodyPart.Common), null, 0, true);
                }
                else
                {
                    if (_player.HealthController.CanApplyItem(meds, EBodyPart.Common))
                    {
                        _player.Proceed(meds, new OneAndList<EBodyPart>(EBodyPart.Common), null, 0, true);
                    }
                    else
                    {
                        Plugin.log.LogInfo($"[MedicineWheel] 当前无需治疗，跳过: {group.FullName}");
                        return;
                    }
                }
            }
            else if (item is FoodDrink food)
            {
                _player.Proceed(food, 1f, null, 0, true);
            }
            else
            {
                _player.Proceed(item, (Callback<IQuickUseItem>)null, true);
            }

            Plugin.log.LogInfo($"[MedicineWheel] 正在使用: {group.FullName} x1");
        }
        catch (Exception e)
        {
            Plugin.log.LogError($"[MedicineWheel] 使用失败: {e.Message}");
        }
    }

    // ── 图标加载 ───────────────────────────────────────────────

    /// <summary>
    /// 异步加载游戏内建物品图标（共享缓存 ItemIconCache：主菜单已预加载或此前加载过则立即返回）。
    /// 加载成功后显示图标并隐藏文字；失败/无图标时保留文字显示（回退）。
    /// await 延续在 Unity 主线程（UnitySynchronizationContext），可安全操作 UI。
    /// </summary>
    private async void LoadItemIconAsync(Item item, string templateId, Image iconImage, TMPro.TextMeshProUGUI fallbackText)
    {
        if (item == null || iconImage == null) return;
        var sprite = await ItemIconCache.GetOrLoadAsync(item, templateId);
        if (sprite == null)
        {
            Plugin.log.LogInfo($"[MedicineWheel] 无内建图标，使用文字显示: {item.ShortName.Localized()}");
            return;
        }
        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.enabled = true;
            if (fallbackText != null) fallbackText.enabled = false;
        }
    }

    private static Color GetMedicineColor(Item item)
    {
        if (item is MedKit) return new Color(0.35f, 0.75f, 0.4f);                          // 急救包：绿
        if (item is Drugs || item is Stimulator) return new Color(0.8f, 0.5f, 0.85f);      // 药品/兴奋剂/止痛药：紫
        if (item is FoodDrink) return new Color(0.9f, 0.65f, 0.3f);                        // 食物/饮水：橙
        if (item is Meds) return new Color(0.45f, 0.65f, 0.9f);                            // 其他医疗用品（绷带/手术包等）：蓝
        return new Color(0.6f, 0.6f, 0.6f);
    }
}