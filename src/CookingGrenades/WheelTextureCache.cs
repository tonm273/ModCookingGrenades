using System.Collections.Generic;
using UnityEngine;

namespace CookingGrenades;

/// <summary>
/// 轮盘程序化纹理的静态缓存。
///
/// 背景：轮盘每次 OpenWheel 都会在 <see cref="WheelBase{T}.CreateWheelUI"/> 里主线程同步重建
/// 多张 RingTexSize(700)×700 纹理（SetPixels32 + Apply，且扇形/弧段逐像素算 sqrt/atan2），
/// 约两百万像素写，导致每次打开轮盘都出现短暂卡顿。
///
/// 修复：把纹理改为静态缓存复用——
///  - 恒定形状（圆环、分隔线）：全局生成一次，手雷轮盘与医药轮盘共用；
///  - 随扇区数变化的形状（扇形高亮、弧段高亮）：扇形/弧段宽度依赖 sectorCount(=手雷种数+1)，
///    按 sectorCount 字典缓存，每种数量只在首次用到时生成一次。
/// 启动时由 <see cref="WheelTextureCache.WarmUp"/> 预烤常见扇区数，连首次打开也不卡。
///
/// 纹理/精灵由静态引用保持存活，不随轮盘 UI 销毁，跨开合一复用。
/// </summary>
internal static class WheelTextureCache
{
    public const int RingTexSize = 700;
    public const float CircleInnerR = 160f;
    public const float CircleOuterR = 163f;
    public const float DividerInnerR = 163f;
    public const float DividerOuterR = 366f;

    // 恒定形状（两份共用一个）
    private static Texture2D _circleRing;
    private static Sprite _circleRingSprite;
    private static Texture2D _divider;
    private static Sprite _dividerSprite;

    // 随扇区数变化的形状（key = sectorCount）
    private static readonly Dictionary<int, Texture2D> SectorTextures = new Dictionary<int, Texture2D>();
    private static readonly Dictionary<int, Sprite> SectorSprites = new Dictionary<int, Sprite>();
    private static readonly Dictionary<int, Texture2D> ArcTextures = new Dictionary<int, Texture2D>();
    private static readonly Dictionary<int, Sprite> ArcSprites = new Dictionary<int, Sprite>();

    public static Sprite CircleRingSprite => GetCircleRingSprite();
    public static Sprite DividerSprite => GetDividerSprite();

    public static Sprite GetCircleRingSprite()
    {
        if (_circleRing == null)
            _circleRing = BuildCircleRingTexture();
        if (_circleRingSprite == null)
            _circleRingSprite = CreateSprite(_circleRing);
        return _circleRingSprite;
    }

    public static Sprite GetDividerSprite()
    {
        if (_divider == null)
            _divider = BuildDividerTexture();
        if (_dividerSprite == null)
            _dividerSprite = CreateSprite(_divider);
        return _dividerSprite;
    }

    public static Sprite GetSectorSprite(int sectorCount)
    {
        Texture2D tex;
        if (!SectorTextures.TryGetValue(sectorCount, out tex))
        {
            tex = BuildSectorHighlightTexture(sectorCount);
            SectorTextures.Add(sectorCount, tex);
        }
        if (!SectorSprites.TryGetValue(sectorCount, out var spr))
        {
            spr = CreateSprite(tex);
            SectorSprites.Add(sectorCount, spr);
        }
        return spr;
    }

    public static Sprite GetArcSprite(int sectorCount)
    {
        Texture2D tex;
        if (!ArcTextures.TryGetValue(sectorCount, out tex))
        {
            tex = BuildHighlightArcTexture(sectorCount);
            ArcTextures.Add(sectorCount, tex);
        }
        if (!ArcSprites.TryGetValue(sectorCount, out var spr))
        {
            spr = CreateSprite(tex);
            ArcSprites.Add(sectorCount, spr);
        }
        return spr;
    }

    /// <summary>启动时预烤常见扇区数（2..12 对应 1..11 种手雷），使首次打开轮盘也不卡。</summary>
    public static void WarmUp(int maxSectorCount = 12)
    {
        GetCircleRingSprite();
        GetDividerSprite();
        for (int sc = 1; sc <= maxSectorCount; sc++)
        {
            GetSectorSprite(sc);
            GetArcSprite(sc);
        }
    }

    private static Sprite CreateSprite(Texture2D tex)
        => Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

    private static Texture2D BuildCircleRingTexture()
    {
        var tex = new Texture2D(RingTexSize, RingTexSize, TextureFormat.RGBA32, false);
        var pixels = new Color32[RingTexSize * RingTexSize];
        var center = new Vector2(RingTexSize / 2f, RingTexSize / 2f);
        var ringColor = new Color32(200, 200, 200, 220);
        var empty = new Color32(0, 0, 0, 0);
        for (int y = 0; y < RingTexSize; y++)
        {
            for (int x = 0; x < RingTexSize; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                pixels[y * RingTexSize + x] = (dist >= CircleInnerR && dist <= CircleOuterR) ? ringColor : empty;
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D BuildDividerTexture()
    {
        const float lineHalfWidth = 2f;
        var tex = new Texture2D(RingTexSize, RingTexSize, TextureFormat.RGBA32, false);
        var pixels = new Color32[RingTexSize * RingTexSize];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(0, 0, 0, 0);
        var center = new Vector2(RingTexSize / 2f, RingTexSize / 2f);

        for (float r = DividerInnerR; r <= DividerOuterR; r += 0.5f)
        {
            float t = (r - DividerInnerR) / (DividerOuterR - DividerInnerR);
            byte alpha = (byte)Mathf.Lerp(200, 0, t);
            var color = new Color32(255, 255, 255, alpha);

            for (float w = -lineHalfWidth; w <= lineHalfWidth; w += 0.5f)
            {
                float px = center.x + w;
                float py = center.y + r;
                int x = Mathf.RoundToInt(px);
                int y = Mathf.RoundToInt(py);
                if (x >= 0 && x < RingTexSize && y >= 0 && y < RingTexSize)
                    pixels[y * RingTexSize + x] = color;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D BuildHighlightArcTexture(int sectorCount)
    {
        float innerR = CircleInnerR - 10f;
        float outerR = CircleInnerR - 3f;
        float arcDeg = (360f / sectorCount) * 0.75f;
        return BuildAngularTexture(innerR, outerR, arcDeg, 255, saturated: true);
    }

    private static Texture2D BuildSectorHighlightTexture(int sectorCount)
    {
        float innerR = CircleOuterR - 2f;
        float outerR = DividerOuterR + 40f;
        float arcDeg = 360f / sectorCount;
        return BuildAngularTexture(innerR, outerR, arcDeg, 0.35f, saturated: false);
    }

    /// <summary>
    /// 逐像素生成一个以正上方（数学角 90°）为中心、张角 arcDeg 的环形区域。
    /// saturated=true → 不透明实心（弧段高亮）；false → 由内向外渐隐（扇形高亮，alpha = maxAlpha*(1-t)）。
    /// </summary>
    private static Texture2D BuildAngularTexture(float innerR, float outerR, float arcDeg, float maxAlpha, bool saturated)
    {
        var tex = new Texture2D(RingTexSize, RingTexSize, TextureFormat.RGBA32, false);
        var pixels = new Color32[RingTexSize * RingTexSize];
        var center = new Vector2(RingTexSize / 2f, RingTexSize / 2f);
        float halfArc = arcDeg / 2f;
        var empty = new Color32(0, 0, 0, 0);

        for (int y = 0; y < RingTexSize; y++)
        {
            float dy = y - center.y;
            for (int x = 0; x < RingTexSize; x++)
            {
                float dx = x - center.x;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist >= innerR && dist <= outerR)
                {
                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (angle < 0) angle += 360;
                    float diff = angle - 90f;
                    if (diff > 180) diff -= 360;
                    if (diff < -180) diff += 360;

                    if (Mathf.Abs(diff) <= halfArc)
                    {
                        if (saturated)
                        {
                            pixels[y * RingTexSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(maxAlpha * 255f));
                        }
                        else
                        {
                            float t = (dist - innerR) / (outerR - innerR);
                            byte a = (byte)Mathf.RoundToInt(Mathf.Lerp(maxAlpha, 0f, t) * 255f);
                            pixels[y * RingTexSize + x] = new Color32(160, 160, 160, a);
                        }
                    }
                    else
                    {
                        pixels[y * RingTexSize + x] = empty;
                    }
                }
                else
                {
                    pixels[y * RingTexSize + x] = empty;
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }
}