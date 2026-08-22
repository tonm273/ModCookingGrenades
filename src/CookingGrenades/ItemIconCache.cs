using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using UnityEngine;

namespace CookingGrenades;

/// <summary>
/// 共享物品图标缓存：手雷轮盘 / 医药轮盘共用。
/// 战局内按需加载并缓存，避免重复资源 IO。
/// </summary>
public static class ItemIconCache
{
    private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

    /// <summary>
    /// 获取图标：有缓存立即返回；无缓存则异步加载并写入缓存。
    /// await 延续在 Unity 主线程，可安全操作 UI。
    /// </summary>
    public static async Task<Sprite> GetOrLoadAsync(Item item, string templateId)
    {
        if (!string.IsNullOrEmpty(templateId) && _cache.TryGetValue(templateId, out var cached) && cached != null)
            return cached;
        if (item == null) return null;

        try
        {
            var sprite = await ItemViewFactory.GetItemSpriteAsync(item, 1);
            if (sprite != null && !string.IsNullOrEmpty(templateId))
                _cache[templateId] = sprite;
            return sprite;
        }
        catch (Exception e)
        {
            Plugin.log.LogWarning($"[ItemIconCache] 图标加载失败 {templateId}: {e.Message}");
            return null;
        }
    }
}
