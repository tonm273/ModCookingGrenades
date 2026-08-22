using System.Collections;
using System.IO;
using System.Reflection;
using CookingGrenades.Config;
using EFT.InventoryLogic;
using UnityEngine;
using UnityEngine.UI;

namespace CookingGrenades;

/// <summary>
/// 温雷提示组件：屏幕空间闪烁手雷图标
/// - 图标固定在屏幕中心右侧偏移位置，温雷时始终可见
/// - 使用三色手雷 PNG 图标：黄色(>2s) → 红色(≤2s)，闪烁关闭态为灰色
/// - 闪烁频率随剩余时间加快，最后 2 秒变红快闪
/// - 投掷时：围绕屏幕中心逆时针旋转 45°，动画时长可配置（默认 1.5 秒）
/// </summary>
public class CookIndicator : MonoBehaviour
{
    // UI 元素
    private Canvas _canvas;
    private RectTransform _indicatorRect;
    private Image _indicatorImage;

    // 图标精灵
    private static Sprite _spriteYellow;
    private static Sprite _spriteRed;
    private static Sprite _spriteGrey;
    private static bool _spritesLoaded;

    private ThrowWeap _throwWeap;
    private float _totalFuseTime;
    private float _cookingStartTime;
    private bool _isActive;

    // 闪烁参数
    private float _blinkTimer;
    private bool _visible = true;
    private const float BaseBlinkInterval = 0.5f;
    private const float MinBlinkInterval = 0.08f;

    // 投掷后动画
    private bool _inThrowAnimation;
    private float _throwAnimTimer;
    private Quaternion _startRotation;
    private Quaternion _targetRotation;

    private float ThrowAnimDuration => ConfigManager.CookIndicatorAnimDuration.Value;

    // 缓存图标大小（像素），根据 CookIndicatorScale 计算
    private float _iconSizePx;

    /// <summary>
    /// 加载指示器图标精灵（嵌入资源）
    /// </summary>
    private static void LoadSprites()
    {
        if (_spritesLoaded) return;

        var asm = Assembly.GetExecutingAssembly();
        var resourceNames = asm.GetManifestResourceNames();

        _spriteYellow = LoadSprite(asm, resourceNames, "Yellow.png");
        _spriteRed = LoadSprite(asm, resourceNames, "Red.png");
        _spriteGrey = LoadSprite(asm, resourceNames, "Grey.png");

        _spritesLoaded = true;

        if (_spriteYellow == null)
            Plugin.log.LogWarning("[CookIndicator] 未加载到 Yellow.png 图标");
        if (_spriteRed == null)
            Plugin.log.LogWarning("[CookIndicator] 未加载到 Red.png 图标");
        if (_spriteGrey == null)
            Plugin.log.LogWarning("[CookIndicator] 未加载到 Grey.png 图标");
    }

    private static Sprite LoadSprite(Assembly asm, string[] resourceNames, string suffix)
    {
        string matched = null;
        foreach (var name in resourceNames)
        {
            if (name.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
            {
                matched = name;
                break;
            }
        }

        if (matched == null) return null;

        using (var stream = asm.GetManifestResourceStream(matched))
        {
            if (stream == null) return null;

            byte[] data = new byte[stream.Length];
            stream.Read(data, 0, data.Length);

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(data);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            Plugin.log.LogInfo($"[CookIndicator] 图标加载成功: {suffix} ({tex.width}x{tex.height})");
            return sprite;
        }
    }

    /// <summary>
    /// 初始化温雷提示（屏幕空间）
    /// </summary>
    public void Initialize(Transform grenadeTransform, ThrowWeap throwWeap, float cookingStartTime)
    {
        _throwWeap = throwWeap;
        _cookingStartTime = cookingStartTime;
        _totalFuseTime = throwWeap.GetExplDelay;
        _isActive = true;
        _inThrowAnimation = false;

        LoadSprites();
        CreateCanvasAndIcon();
        UpdateIconSize();

        Plugin.log.LogInfo($"[CookIndicator] 初始化: 引信={_totalFuseTime:F2}s, 手雷={throwWeap.Name}");
    }

    private void CreateCanvasAndIcon()
    {
        // 全屏 Canvas（Screen Space Overlay，不需要相机）
        GameObject canvasObj = new GameObject("CookIndicator_Canvas");
        DontDestroyOnLoad(canvasObj);
        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 32000; // 尽量最上层

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        // 图标 GameObject
        GameObject imgObj = new GameObject("CookIndicator_Icon");
        imgObj.transform.SetParent(_canvas.transform, false);

        _indicatorImage = imgObj.AddComponent<Image>();
        // 默认用黄色图标，LateUpdate 中根据状态切换
        _indicatorImage.sprite = _spriteYellow ?? _spriteGrey ?? _spriteRed;
        _indicatorImage.color = Color.white; // 图标自带颜色，不再用 tint
        _indicatorImage.raycastTarget = false;

        _indicatorRect = imgObj.GetComponent<RectTransform>();
        _indicatorRect.pivot = new Vector2(0.5f, 0.5f);
        _indicatorRect.anchorMin = _indicatorRect.anchorMax = new Vector2(0.5f, 0.5f);
    }

    /// <summary>
    /// 根据 CookIndicatorScale 计算并更新图标像素大小
    /// </summary>
    private void UpdateIconSize()
    {
        float basePx = Mathf.Min(Screen.width, Screen.height);
        _iconSizePx = Mathf.Clamp(basePx * 0.08f * ConfigManager.CookIndicatorScale.Value, 20f, 200f);

        if (_indicatorRect != null)
        {
            _indicatorRect.sizeDelta = new Vector2(_iconSizePx, _iconSizePx);
        }
    }

    private void LateUpdate()
    {
        if (_canvas == null || _indicatorRect == null) return;

        if (_inThrowAnimation)
        {
            UpdateThrowAnimation();
            return;
        }

        if (!_isActive || _throwWeap == null) return;

        float cookedTime = Time.time - _cookingStartTime;
        float remainingTime = _totalFuseTime - cookedTime;

        if (remainingTime <= 0f)
        {
            Hide();
            return;
        }

        // 位置：屏幕中心 + 配置的 X/Y 偏移
        float xOffset = ConfigManager.CookIndicatorOffsetX.Value;
        float yOffset = ConfigManager.CookIndicatorOffsetY.Value + (ConfigManager.CookIndicatorHeight.Value - 0.3f) * 50f;
        _indicatorRect.anchoredPosition = new Vector2(xOffset, yOffset);
        _indicatorRect.localRotation = Quaternion.identity;

        // 闪烁频率（渐快）
        float blinkInterval;

        if (remainingTime > 2f)
        {
            float t = Mathf.Clamp01(cookedTime / Mathf.Max(0.1f, _totalFuseTime - 2f));
            blinkInterval = Mathf.Lerp(BaseBlinkInterval, 0.25f, t);
        }
        else
        {
            float t = Mathf.Clamp01(1f - remainingTime / 2f);
            blinkInterval = Mathf.Lerp(0.25f, MinBlinkInterval, t);
        }

        // 闪烁逻辑
        _blinkTimer += Time.deltaTime;
        if (_blinkTimer >= blinkInterval)
        {
            _blinkTimer = 0f;
            _visible = !_visible;
        }

        // 图标切换
        if (remainingTime > 4f)
        {
            // >4s：灰色常亮，不闪烁
            _indicatorImage.sprite = _spriteGrey ?? _spriteYellow ?? _indicatorImage.sprite;
            _indicatorImage.color = Color.white;
            _indicatorImage.enabled = true;
        }
        else if (remainingTime > 2f)
        {
            // 2s~4s：黄色闪烁
            _indicatorImage.sprite = _spriteYellow ?? _indicatorImage.sprite;
            _indicatorImage.color = Color.white;
            _indicatorImage.enabled = _visible;
        }
        else
        {
            // ≤2s：红色闪烁
            _indicatorImage.sprite = _spriteRed ?? _indicatorImage.sprite;
            _indicatorImage.color = Color.white;
            _indicatorImage.enabled = _visible;
        }
    }

    /// <summary>
    /// 手雷投掷时触发：围绕屏幕中心逆时针旋转 45°，动画时长可在 F12 菜单配置
    /// </summary>
    public void PlayThrowAnimation()
    {
        if (_canvas == null || _indicatorRect == null) return;

        _isActive = false;
        _inThrowAnimation = true;
        _throwAnimTimer = 0f;

        // 计算围绕屏幕中心旋转时的起始/目标角度
        Vector2 iconCenter = _indicatorRect.anchoredPosition;
        Vector2 pivot = -iconCenter;
        Vector2 size = _indicatorRect.sizeDelta;
        _indicatorRect.pivot = new Vector2(0.5f - pivot.x / Mathf.Max(1f, size.x), 0.5f - pivot.y / Mathf.Max(1f, size.y));
        _indicatorRect.anchoredPosition = new Vector2(
            iconCenter.x + (0.5f - _indicatorRect.pivot.x) * size.x,
            iconCenter.y + (0.5f - _indicatorRect.pivot.y) * size.y
        );

        _startRotation = _indicatorRect.localRotation;
        _targetRotation = _startRotation * Quaternion.Euler(0f, 0f, 45f);
    }

    private void UpdateThrowAnimation()
    {
        _throwAnimTimer += Time.deltaTime;
        float t = Mathf.Clamp01(_throwAnimTimer / ThrowAnimDuration);

        _indicatorRect.localRotation = Quaternion.Slerp(_startRotation, _targetRotation, t);
        Color c = _indicatorImage.color;
        c.a = Mathf.Lerp(1f, 0f, t);
        _indicatorImage.color = c;

        if (t >= 1f)
        {
            Hide();
        }
    }

    public void Hide()
    {
        _isActive = false;
        _inThrowAnimation = false;
        if (_canvas != null)
        {
            Destroy(_canvas.gameObject);
            _canvas = null;
            _indicatorRect = null;
            _indicatorImage = null;
        }
    }

    private void OnDestroy()
    {
        Hide();
    }
}