using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using UnityEngine;
using UnityEngine.UI;

namespace CookingGrenades;

/// <summary>
/// 轮盘基类：负责两个轮盘完全共用的渲染 UI、主循环、纹理生成、鼠标选择逻辑。
/// 子类（GrenadeWheel / MedicineWheel）只实现扫描、图标加载、选中行为等差异点。
/// 通过泛型单例为每个子类类型分别保留一份 Instance。
/// </summary>
public abstract class WheelBase<T> : MonoBehaviour where T : WheelBase<T>
{
    public static T Instance { get; protected set; }
    public bool IsOpen { get; protected set; }

    protected Player _player;
    protected int _selectedIndex = -1;

    private Canvas _canvas;
    private TMPro.TextMeshProUGUI _centerLabel;
    private GameWorld _cachedGameWorld;
    private GameObject _highlightObj;
    private GameObject _sectorHighlightObj;
    private readonly List<Graphic> _slotGraphics = new List<Graphic>();
    private readonly List<GameObject> _dividerObjects = new List<GameObject>();
    private float _currentRotZ = 0f;
    private float _targetRotZ = 0f;
    private float _sectorCurrentRotZ = 0f;
    private float _sectorTargetRotZ = 0f;

    private float _raidStartTime = -1f;
    private float _keyHoldStartTime = -1f;
    private bool _keyWasHeld = false;

    private const float CenterDeadZone = 35f;
    private const float RaidDelay = 3f;
    private const float HoldDuration = 0.5f;
    private const int RingTexSize = 700;
    private const float CircleInnerR = 160f;
    private const float CircleOuterR = 163f;
    private const float DividerInnerR = 163f;
    private const float DividerOuterR = 366f;
    private const float RingDisplaySize = 620f;
    private const float HighlightSmoothing = 15f;

    /// <summary>
    /// 轮盘中央"未选择"占位文案，跟随游戏语言（LocalizationManager.Instance.Culture）。
    /// 中文显示"无"，其他语言显示"None"。
    /// </summary>
    protected static string GetNoSelectionText()
    {
        try
        {
            var culture = LocalizationManager.Instance?.Culture;
            if (!string.IsNullOrEmpty(culture)
                && culture.StartsWith("ch", StringComparison.OrdinalIgnoreCase))
                return "无";
        }
        catch { }
        return "None";
    }

    // ── 子类差异扩展点 ──────────────────────────────────────────

    /// <summary>日志前缀（如 GrenadeWheel / MedicineWheel）</summary>
    protected abstract string WheelLogName { get; }

    /// <summary>是否开启（对应各自配置）</summary>
    protected abstract bool EnabledByConfig { get; }

    /// <summary>长按唤出轮盘的按键</summary>
    protected abstract KeyCode HoldKey { get; }

    /// <summary>互斥：另一个轮盘当前是否打开</summary>
    protected abstract bool IsOtherWheelOpen();

    /// <summary>填充数据（扫描物品并分组到子类列表），然后通过 DisplayCount/GetSlotX 供 UI 读取</summary>
    protected abstract void ScanItems();

    /// <summary>当前分组数量</summary>
    protected abstract int DisplayCount { get; }

    /// <summary>槽位短名（图标/名称文字）</summary>
    protected abstract string GetSlotName(int index);

    /// <summary>中心标签完整名</summary>
    protected abstract string GetSlotFullName(int index);

    /// <summary>槽位数量文字</summary>
    protected abstract int GetSlotCount(int index);

    /// <summary>异步加载槽位图标（成功则覆盖 iconImage 显示并隐藏回退文字）</summary>
    protected abstract void LoadSlotIcon(int index, Image iconImage, TMPro.TextMeshProUGUI fallbackText);

    /// <summary>选中确认后的行为（装备手雷 / 使用药品）</summary>
    protected abstract void OnSelect(int index);

    // ── 生命周期 ────────────────────────────────────────────────

    protected virtual void Awake()
    {
        if (Instance != null)
        {
            Destroy(this);
            return;
        }
        Instance = (T)(object)this;
        DontDestroyOnLoad(gameObject);
        Plugin.log.LogInfo($"[{WheelLogName}] 已初始化");
    }

    private void Update()
    {
        // 不能只判断 _cachedGameWorld == null（Unity 的 == 对已释放但未销毁的对象返回 false）。
        var current = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
        if (!ReferenceEquals(_cachedGameWorld, current))
            _cachedGameWorld = current;
        var gameWorld = _cachedGameWorld;
        if (gameWorld?.MainPlayer == null)
        {
            _raidStartTime = -1f;
            if (IsOpen) ForceClose();
            return;
        }

        _player = gameWorld.MainPlayer;
        if (!_player.HealthController.IsAlive)
        {
            if (IsOpen) ForceClose();
            return;
        }

        if (_raidStartTime < 0f)
        {
            _raidStartTime = Time.time;
            Plugin.log.LogInfo($"[{WheelLogName}] 战局开始，3秒后可使用轮盘");
        }

        // 温雷期间禁用
        if (GrenadeCookingManager.Timer.IsCooking)
        {
            if (IsOpen) ForceClose();
            return;
        }

        if (IsOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (!Input.GetKey(HoldKey))
            {
                CloseAndApply();
            }
        }
        else
        {
            if (!EnabledByConfig) return;

            // 另一个轮盘打开时禁用（互斥）
            if (IsOtherWheelOpen()) return;

            // 进入战局3秒后才允许呼出
            if (Time.time - _raidStartTime < RaidDelay) return;

            KeyCode key = HoldKey;
            bool keyDown = Input.GetKeyDown(key);
            bool keyHeld = Input.GetKey(key);

            if (keyDown)
            {
                _keyHoldStartTime = Time.time;
                _keyWasHeld = false;
                Plugin.log.LogInfo($"[{WheelLogName}] 按键 {key} 按下，开始计时长按");
            }

            if (keyHeld && !_keyWasHeld && _keyHoldStartTime > 0f)
            {
                if (Time.time - _keyHoldStartTime >= HoldDuration)
                {
                    _keyWasHeld = true;
                    _keyHoldStartTime = -1f;
                    Plugin.log.LogInfo($"[{WheelLogName}] 按键 {key} 已长按{HoldDuration}秒，呼出轮盘");
                    OpenWheel();
                }
            }

            if (!keyHeld)
            {
                _keyHoldStartTime = -1f;
            }
        }
    }

    private void LateUpdate()
    {
        if (IsOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UpdateSelection();
        }
    }

    // ── 打开 / 关闭 ────────────────────────────────────────────

    public void OpenWheel()
    {
        if (IsOpen) return;
        if (!EnabledByConfig) return;
        if (IsOtherWheelOpen()) return;

        var gameWorld = Singleton<GameWorld>.Instance;
        _player = gameWorld?.MainPlayer;
        if (_player == null) return;

        var inventoryController = _player.InventoryController;
        if (inventoryController == null)
        {
            Plugin.log.LogWarning($"[{WheelLogName}] InventoryController 为空");
            return;
        }

        Plugin.log.LogInfo($"[{WheelLogName}] 扫描物品...");
        // 扫描前强制清空，确保 DisplayCount 与实际一致
        ScanItems();

        IsOpen = true;
        _selectedIndex = -1;
        CreateWheelUI();
        Plugin.log.LogInfo($"[{WheelLogName}] 轮盘已开启 ({DisplayCount} 种)");
    }

    public void CloseAndApply()
    {
        if (!IsOpen) return;
        IsOpen = false;
        DestroyWheelUI();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (_selectedIndex >= 0 && _selectedIndex < DisplayCount)
        {
            OnSelect(_selectedIndex);
        }
    }

    public void ForceClose()
    {
        IsOpen = false;
        DestroyWheelUI();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ── UI 创建 ────────────────────────────────────────────────

    private void CreateWheelUI()
    {
        var canvasObj = new GameObject($"{WheelLogName}_Canvas");
        DontDestroyOnLoad(canvasObj);
        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 31000;
        canvasObj.AddComponent<CanvasScaler>();

        int count = DisplayCount;

        // 中心文字
        var centerObj = new GameObject("CenterLabel");
        centerObj.transform.SetParent(canvasObj.transform, false);
        _centerLabel = centerObj.AddComponent<TMPro.TextMeshProUGUI>();
        _centerLabel.text = GetNoSelectionText();
        _centerLabel.fontSize = 24;
        _centerLabel.alignment = TMPro.TextAlignmentOptions.Center;
        _centerLabel.color = new Color(1, 1, 1, 0.6f);
        _centerLabel.raycastTarget = false;
        var centerRect = centerObj.GetComponent<RectTransform>();
        centerRect.anchorMin = centerRect.anchorMax = new Vector2(0.5f, 0.5f);
        centerRect.anchoredPosition = Vector2.zero;
        centerRect.sizeDelta = new Vector2(120, 36);

        if (count == 0) return;

        float scale = RingDisplaySize / RingTexSize;

        // 1. 白色圆环
        var circleTex = CreateCircleRingTexture(RingTexSize, CircleInnerR, CircleOuterR);
        var circleSprite = Sprite.Create(circleTex, new Rect(0, 0, RingTexSize, RingTexSize), new Vector2(0.5f, 0.5f));
        var circleObj = new GameObject("CircleRing");
        circleObj.transform.SetParent(canvasObj.transform, false);
        var circleImage = circleObj.AddComponent<Image>();
        circleImage.sprite = circleSprite;
        circleImage.color = Color.white;
        circleImage.raycastTarget = false;
        var circleRect = circleObj.GetComponent<RectTransform>();
        circleRect.anchorMin = circleRect.anchorMax = new Vector2(0.5f, 0.5f);
        circleRect.anchoredPosition = Vector2.zero;
        circleRect.sizeDelta = new Vector2(RingDisplaySize, RingDisplaySize);

        // 2. 分隔线（单独对象，仅选中区域显示）
        var divTex = CreateSingleDividerTexture(RingTexSize, DividerInnerR, DividerOuterR, 2f);
        var divSprite = Sprite.Create(divTex, new Rect(0, 0, RingTexSize, RingTexSize), new Vector2(0.5f, 0.5f));
        _dividerObjects.Clear();
        for (int i = 0; i < count; i++)
        {
            var divObj = new GameObject($"Divider_{i}");
            divObj.transform.SetParent(canvasObj.transform, false);
            var divImage = divObj.AddComponent<Image>();
            divImage.sprite = divSprite;
            divImage.color = Color.white;
            divImage.raycastTarget = false;
            var divRect = divObj.GetComponent<RectTransform>();
            divRect.anchorMin = divRect.anchorMax = new Vector2(0.5f, 0.5f);
            divRect.anchoredPosition = Vector2.zero;
            divRect.sizeDelta = new Vector2(RingDisplaySize, RingDisplaySize);
            // 分隔线 i 位于槽位 i 和 i+1 之间
            float dividerAngle = 90f - (360f / count) * (i + 0.5f);
            float rotZ = dividerAngle - 90f;
            divRect.localRotation = Quaternion.Euler(0, 0, rotZ);
            divObj.SetActive(false);
            _dividerObjects.Add(divObj);
        }

        // 3. 扇形区域高亮（覆盖选中槽位的整个图标区域，淡灰色由内向外渐浅至透明）
        float sectorArcDeg = 360f / count;
        float sectorInnerR = CircleOuterR - 2f;
        float sectorOuterR = DividerOuterR + 40f;
        var secTex = CreateSectorHighlightTexture(RingTexSize, sectorInnerR, sectorOuterR, sectorArcDeg);
        var secSprite = Sprite.Create(secTex, new Rect(0, 0, RingTexSize, RingTexSize), new Vector2(0.5f, 0.5f));
        var secObj = new GameObject("SectorHighlight");
        secObj.transform.SetParent(canvasObj.transform, false);
        var secImage = secObj.AddComponent<Image>();
        secImage.sprite = secSprite;
        secImage.color = Color.white;
        secImage.raycastTarget = false;
        var secRect = secObj.GetComponent<RectTransform>();
        secRect.anchorMin = secRect.anchorMax = new Vector2(0.5f, 0.5f);
        secRect.anchoredPosition = Vector2.zero;
        secRect.sizeDelta = new Vector2(RingDisplaySize, RingDisplaySize);
        _sectorHighlightObj = secObj;
        _sectorHighlightObj.SetActive(false);

        // 4. 高亮弧段（可旋转，贴圆环内壁滑动）
        float hlArcDeg = (360f / count) * 0.75f;
        var hlTex = CreateHighlightArcTexture(RingTexSize, CircleInnerR - 10f, CircleInnerR - 3f, hlArcDeg);
        var hlSprite = Sprite.Create(hlTex, new Rect(0, 0, RingTexSize, RingTexSize), new Vector2(0.5f, 0.5f));
        var hlObj = new GameObject("Highlight");
        hlObj.transform.SetParent(canvasObj.transform, false);
        var hlImage = hlObj.AddComponent<Image>();
        hlImage.sprite = hlSprite;
        hlImage.color = new Color(1, 1, 1, 0.85f);
        hlImage.raycastTarget = false;
        var hlRect = hlObj.GetComponent<RectTransform>();
        hlRect.anchorMin = hlRect.anchorMax = new Vector2(0.5f, 0.5f);
        hlRect.anchoredPosition = Vector2.zero;
        hlRect.sizeDelta = new Vector2(RingDisplaySize, RingDisplaySize);
        _highlightObj = hlObj;
        _highlightObj.SetActive(false);

        // 5. 槽位（图标 + 名称文字 + 数量文字）
        float textRadius = ((CircleOuterR + DividerOuterR) / 2f) * scale;
        for (int i = 0; i < count; i++)
        {
            float angleDeg = 90f - (360f / count) * i;
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector2 pos = new Vector2(
                Mathf.Cos(angleRad) * textRadius,
                Mathf.Sin(angleRad) * textRadius);

            // 图标（初始隐藏，图标就绪后显示；成功则覆盖并隐藏回退文字）
            var iconObj = new GameObject($"Icon_{i}");
            iconObj.transform.SetParent(canvasObj.transform, false);
            var iconImage = iconObj.AddComponent<Image>();
            iconImage.color = new Color(0.6f, 0.6f, 0.6f);
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
            iconImage.enabled = false;
            var iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = pos;
            iconRect.sizeDelta = new Vector2(112, 112);
            _slotGraphics.Add(iconImage);

            // 回退名称文字
            var nameObj = new GameObject($"Name_{i}");
            nameObj.transform.SetParent(canvasObj.transform, false);
            var nameText = nameObj.AddComponent<TMPro.TextMeshProUGUI>();
            nameText.text = GetSlotName(i);
            nameText.fontSize = 20;
            nameText.fontStyle = TMPro.FontStyles.Bold;
            nameText.alignment = TMPro.TextAlignmentOptions.Center;
            nameText.color = new Color(0.6f, 0.6f, 0.6f);
            nameText.raycastTarget = false;
            var nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = nameRect.anchorMax = new Vector2(0.5f, 0.5f);
            nameRect.anchoredPosition = pos + new Vector2(0, 4f);
            nameRect.sizeDelta = new Vector2(100, 28);
            _slotGraphics.Add(nameText);

            // 数量文字
            var countObj = new GameObject($"Count_{i}");
            countObj.transform.SetParent(canvasObj.transform, false);
            var countText = countObj.AddComponent<TMPro.TextMeshProUGUI>();
            countText.text = $"{GetSlotCount(i)}";
            countText.fontSize = 24;
            countText.alignment = TMPro.TextAlignmentOptions.Center;
            countText.color = new Color(1, 1, 1, 0.5f);
            countText.raycastTarget = false;
            var countRect = countObj.GetComponent<RectTransform>();
            countRect.anchorMin = countRect.anchorMax = new Vector2(0.5f, 0.5f);
            countRect.anchoredPosition = pos + new Vector2(0, -42f);
            countRect.sizeDelta = new Vector2(70, 24);

            LoadSlotIcon(i, iconImage, nameText);
        }

        _currentRotZ = 0f;
        _targetRotZ = 0f;
        _sectorCurrentRotZ = 0f;
        _sectorTargetRotZ = 0f;
    }

    private void DestroyWheelUI()
    {
        if (_canvas != null)
        {
            Destroy(_canvas.gameObject);
            _canvas = null;
        }
        _centerLabel = null;
        _highlightObj = null;
        _sectorHighlightObj = null;
        _slotGraphics.Clear();
        _dividerObjects.Clear();
    }

    // ── 鼠标选择 ───────────────────────────────────────────────

    private void UpdateSelection()
    {
        if (_slotGraphics.Count == 0) return;

        Vector2 mousePos = Input.mousePosition;
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 dir = mousePos - screenCenter;

        int count = DisplayCount;
        float slotAngleSize = 360f / count;
        float halfSlotAngle = slotAngleSize * 0.5f;

        if (dir.magnitude < CenterDeadZone)
        {
            if (_highlightObj != null)
                _highlightObj.SetActive(false);
            if (_sectorHighlightObj != null)
                _sectorHighlightObj.SetActive(false);

            for (int i = 0; i < _slotGraphics.Count; i++)
            {
                if (_slotGraphics[i] != null)
                {
                    _slotGraphics[i].color = new Color(0.6f, 0.6f, 0.6f);
                    _slotGraphics[i].rectTransform.localScale = Vector3.one;
                }
            }

            // 隐藏所有分隔线
            for (int i = 0; i < _dividerObjects.Count; i++)
            {
                if (_dividerObjects[i].activeSelf)
                    _dividerObjects[i].SetActive(false);
            }

            if (_centerLabel != null)
            {
                _centerLabel.text = GetNoSelectionText();
                _centerLabel.color = new Color(1, 1, 1, 0.6f);
            }
            _selectedIndex = -1;
            return;
        }

        // 计算目标旋转角度（Z旋转 = mouseAngle - 90）
        float mouseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        if (mouseAngle < 0) mouseAngle += 360;
        _targetRotZ = mouseAngle - 90f;

        // 平滑插值（弧段滑块：跟随鼠标）
        _currentRotZ = Mathf.LerpAngle(_currentRotZ, _targetRotZ, Time.deltaTime * HighlightSmoothing);

        // 旋转高亮弧段
        if (_highlightObj != null)
        {
            if (!_highlightObj.activeSelf)
                _highlightObj.SetActive(true);
            _highlightObj.transform.localRotation = Quaternion.Euler(0, 0, _currentRotZ);
        }

        // 计算选中索引
        float currentMathAngle = _currentRotZ + 90f;
        float selFloat = (90f - currentMathAngle) / slotAngleSize;
        int newSel = Mathf.RoundToInt(selFloat);
        newSel = ((newSel % count) + count) % count;
        _selectedIndex = newSel;

        // 扇形高亮目标角度：对齐到选中槽位的中心
        _sectorTargetRotZ = -slotAngleSize * _selectedIndex;
        _sectorCurrentRotZ = Mathf.LerpAngle(_sectorCurrentRotZ, _sectorTargetRotZ, Time.deltaTime * HighlightSmoothing);
        if (_sectorHighlightObj != null)
        {
            if (!_sectorHighlightObj.activeSelf)
                _sectorHighlightObj.SetActive(true);
            _sectorHighlightObj.transform.localRotation = Quaternion.Euler(0, 0, _sectorCurrentRotZ);
        }

        // 更新图标/文字颜色和缩放（基于与高亮的角度距离，平滑过渡）。
        // _slotGraphics 每槽存 2 个元素（i*2=图标、i*2+1=回退文字），角度必须按槽位索引 i 计算。
        for (int i = 0; i < count; i++)
        {
            float slotRotZ = -slotAngleSize * i;
            float angleDist = Mathf.Abs(Mathf.DeltaAngle(slotRotZ, _currentRotZ));
            float proximity = Mathf.Clamp01(1f - angleDist / halfSlotAngle);
            float scl = 1f + 0.3f * proximity;
            Color c = Color.Lerp(new Color(0.6f, 0.6f, 0.6f), Color.white, proximity);

            int iconIdx = i * 2;
            int nameIdx = i * 2 + 1;
            if (iconIdx < _slotGraphics.Count && _slotGraphics[iconIdx] != null)
            {
                _slotGraphics[iconIdx].rectTransform.localScale = Vector3.one * scl;
                _slotGraphics[iconIdx].color = c;
            }
            if (nameIdx < _slotGraphics.Count && _slotGraphics[nameIdx] != null)
            {
                _slotGraphics[nameIdx].rectTransform.localScale = Vector3.one * scl;
                _slotGraphics[nameIdx].color = c;
            }
        }

        // 仅显示选中槽位两侧的分隔线
        for (int i = 0; i < _dividerObjects.Count; i++)
        {
            bool shouldShow = (i == _selectedIndex) || (i == ((_selectedIndex - 1 + count) % count));
            if (_dividerObjects[i].activeSelf != shouldShow)
                _dividerObjects[i].SetActive(shouldShow);
        }

        // 中心文字
        if (_centerLabel != null && _selectedIndex >= 0)
        {
            _centerLabel.text = GetSlotFullName(_selectedIndex);
            _centerLabel.color = Color.white;
        }
    }

    // ── 程序化纹理生成 ─────────────────────────────────────────

    private Texture2D CreateCircleRingTexture(int texSize, float innerR, float outerR)
    {
        var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        var pixels = new Color32[texSize * texSize];
        var center = new Vector2(texSize / 2f, texSize / 2f);
        var ringColor = new Color32(200, 200, 200, 220);
        var empty = new Color32(0, 0, 0, 0);
        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                pixels[y * texSize + x] = (dist >= innerR && dist <= outerR) ? ringColor : empty;
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private Texture2D CreateSingleDividerTexture(int texSize, float innerR, float outerR, float lineHalfWidth)
    {
        var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        var pixels = new Color32[texSize * texSize];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(0, 0, 0, 0);
        var center = new Vector2(texSize / 2f, texSize / 2f);

        for (float r = innerR; r <= outerR; r += 0.5f)
        {
            float t = (r - innerR) / (outerR - innerR);
            byte alpha = (byte)Mathf.Lerp(200, 0, t);
            var color = new Color32(255, 255, 255, alpha);

            for (float w = -lineHalfWidth; w <= lineHalfWidth; w += 0.5f)
            {
                float px = center.x + w;
                float py = center.y + r;
                int x = Mathf.RoundToInt(px);
                int y = Mathf.RoundToInt(py);
                if (x >= 0 && x < texSize && y >= 0 && y < texSize)
                    pixels[y * texSize + x] = color;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private Texture2D CreateHighlightArcTexture(int texSize, float innerR, float outerR, float arcDeg)
    {
        var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        var pixels = new Color32[texSize * texSize];
        var center = new Vector2(texSize / 2f, texSize / 2f);
        float halfArc = arcDeg / 2f;
        var fill = new Color32(255, 255, 255, 255);
        var empty = new Color32(0, 0, 0, 0);

        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                float dx = x - center.x;
                float dy = y - center.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist >= innerR && dist <= outerR)
                {
                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (angle < 0) angle += 360;
                    float diff = angle - 90f;
                    if (diff > 180) diff -= 360;
                    if (diff < -180) diff += 360;
                    pixels[y * texSize + x] = Mathf.Abs(diff) <= halfArc ? fill : empty;
                }
                else
                {
                    pixels[y * texSize + x] = empty;
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    private Texture2D CreateSectorHighlightTexture(int texSize, float innerR, float outerR, float arcDeg)
    {
        var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        var pixels = new Color32[texSize * texSize];
        var center = new Vector2(texSize / 2f, texSize / 2f);
        float halfArc = arcDeg / 2f;
        var empty = new Color32(0, 0, 0, 0);

        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                float dx = x - center.x;
                float dy = y - center.y;
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
                        float t = (dist - innerR) / (outerR - innerR);
                        byte a = (byte)Mathf.RoundToInt(Mathf.Lerp(0.35f, 0f, t) * 255f);
                        pixels[y * texSize + x] = new Color32(160, 160, 160, a);
                    }
                    else
                    {
                        pixels[y * texSize + x] = empty;
                    }
                }
                else
                {
                    pixels[y * texSize + x] = empty;
                }
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    protected virtual void OnDestroy()
    {
        if (Instance != null && Instance == (T)(object)this)
            Instance = null;
    }
}