using System;
using System.Reflection;
using Comfort.Common;
using CookingGrenades.Config;
using EFT;
using EFT.UI;
using HarmonyLib;
using UnityEngine;

namespace CookingGrenades;

public class TrajectoryRenderer : MonoBehaviour
{
    private LineRenderer _line;
    private GameObject _landingSphere;
    private GameWorld _cachedGameWorld;
    private Renderer _sphereRenderer;
    private Vector3[] _positions;
    private Vector3 _playerVelocity;

    private float _mass = 0.55f;
    private string _itemName;
    private float _gravity;
    private const float LinearDrag = 0.1f;

    // GrenadePrefab 字段：通过反射搜索类型为 GrenadePrefab 的字段（不硬编码名称）
    private static FieldInfo _grenadePrefabField;
    private static bool _grenadePrefabFieldChecked;

    // HitMask 反射缓存（只初始化一次）
    private static int _cachedHitMask = -1;

    // 上次控制器类型名（变化时输出日志）
    private string _lastControllerTypeName;

    // 抛物线重算节流：未移动/未瞄准微调时，间隔 N 帧才重算一次（降低持雷时的物理检测开销）
    private int _recalcCounter;
    private int _lastPositionCount;
    private bool _lastCollided;
    private Vector3 _lastThrowPos;
    private Vector3 _lastThrowVelocity;
    private bool _hasComputed;
    private const float RecalcMoveSqr = 0.06f * 0.06f;   // 位置位移阈值：6cm
    private const float RecalcVelSqr = 0.08f * 0.08f;   // 初速变化阈值：8cm/s

    private void Start()
    {
        _gravity = -Physics.gravity.y;
        _positions = new Vector3[ConfigManager.TrajectoryPoints.Value];
        _playerVelocity = Vector3.zero;

        Plugin.log.LogInfo("[Trajectory] TrajectoryRenderer 已启动");
    }

    private void EnsureLinesCreated()
    {
        if (_line != null) return;

        _line = gameObject.AddComponent<LineRenderer>();
        // 关键：世界空间渲染，否则线的位置相对于GameObject会出错
        _line.useWorldSpace = true;
        // Shader回退：Sprites/Default 不行就用 Unlit/Color（更可靠）
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        _line.material = new Material(shader);
        _line.numCapVertices = 1;
        _line.numCornerVertices = 2;
        _line.startColor = ConfigManager.TrajectoryColor.Value;
        _line.endColor = ConfigManager.LandingPointColor.Value;
        // 线宽放大：配置值 * 5（默认0.015太细，调到0.075米/7.5厘米才明显）
        _line.startWidth = ConfigManager.TrajectoryLineWidth.Value * 5f;
        _line.endWidth = ConfigManager.TrajectoryLineWidth.Value * 2.5f;
        _line.enabled = false;

        _landingSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _landingSphere.transform.localScale = Vector3.one * ConfigManager.LandingPointRadius.Value;
        _landingSphere.GetComponent<Collider>().enabled = false;
        _sphereRenderer = _landingSphere.GetComponent<Renderer>();
        _sphereRenderer.material = new Material(Shader.Find("Sprites/Default"))
        {
            color = ConfigManager.LandingPointColor.Value
        };
        _sphereRenderer.enabled = false;

        Plugin.log.LogInfo("[Trajectory] LineRenderer + 落点Sphere 已创建");
    }

    /// <summary>
    /// 通过反射搜索类型为 GrenadePrefab 的字段（4.1去混淆后字段名变了，不硬编码）
    /// </summary>
    private static FieldInfo FindGrenadePrefabField(Type controllerType)
    {
        if (_grenadePrefabFieldChecked)
            return _grenadePrefabField;
        _grenadePrefabFieldChecked = true;

        Type t = controllerType;
        while (t != null && t != typeof(object))
        {
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (f.FieldType == typeof(GrenadePrefab))
                {
                    _grenadePrefabField = f;
                    Plugin.log.LogInfo($"[Trajectory] 找到 GrenadePrefab 字段: {f.DeclaringType?.Name}.{f.Name}");
                    return _grenadePrefabField;
                }
            }
            t = t.BaseType;
        }

        Plugin.log.LogWarning("[Trajectory] 未找到 GrenadePrefab 字段，将使用默认质量 0.55kg");
        return null;
    }

    /// <summary>
    /// 参考 VisualAssist.CalculateGrenadeThrow
    /// </summary>
    private GrenadeThrow CalculateGrenadeThrow(Player player, bool lowThrow)
    {
        var lowHighThrow = lowThrow ? 0.66f : 1f + player.Skills.StrengthBuffThrowDistanceInc;
        var forcePower = EFTHardSettings.Instance.GrenadeForce;

        var rootTransform = player.PlayerBones.WeaponRoot.Original;

        if (!(bool)player.Skills.ThrowingEliteBuff)
        {
            var handStamina = player.Physical.HandsStamina.NormalValue;
            lowHighThrow *= Mathf.Lerp(0.4f, 1f, handStamina + 0.5f);
        }

        var direction = -rootTransform.up;
        var force = direction * (forcePower * lowHighThrow) + _playerVelocity;
        var throwPosition = rootTransform.position + 0.5f * direction;

        return new GrenadeThrow { ThrowPosition = throwPosition, ThrowForce = force };
    }

    /// <summary>
    /// 参考 VisualAssist.GetBallisticArcWithLinearDrag
    /// </summary>
    private static bool GetBallisticArcWithLinearDrag(
        Vector3[] positions,
        Vector3 startPosition,
        Vector3 initialVelocity,
        float intervalDistance,
        float maxDistance,
        float gravity,
        float linearDragCoefficient,
        out int positionCount
    )
    {
        var i = 0;
        positions[i] = startPosition;
        i++;

        var k = linearDragCoefficient;
        if (k < 0.0001f) k = 0.0001f;

        var v0X = initialVelocity.x;
        var v0Y = initialVelocity.y;
        var v0Z = initialVelocity.z;

        var horizontalSpeed = Mathf.Sqrt(v0X * v0X + v0Z * v0Z);
        if (horizontalSpeed < 0.001f)
        {
            positionCount = i;
            return false;
        }

        var maxReachableDistance = horizontalSpeed / k;
        var currentDistance = intervalDistance;

        while (currentDistance <= maxDistance && currentDistance < maxReachableDistance * 0.999f && i < positions.Length)
        {
            var ratio = currentDistance * k / horizontalSpeed;
            var t = -Mathf.Log(1f - ratio) / k;
            var dragTerm = Mathf.Exp(-k * t);

            var x = startPosition.x + (v0X / k) * (1f - dragTerm);
            var y = startPosition.y + (1f / k) * ((v0Y + gravity / k) * (1f - dragTerm) - gravity * t);
            var z = startPosition.z + (v0Z / k) * (1f - dragTerm);
            var candidatePos = new Vector3(x, y, z);

            var prevPos = positions[i - 1];
            var arcLine = candidatePos - prevPos;

            if (Physics.SphereCast(
                    prevPos, 0.05f, arcLine.normalized, out var hit, arcLine.magnitude,
                    GetHitMask()))
            {
                positions[i] = hit.point;
                positionCount = i + 1;
                return true;
            }

            positions[i] = candidatePos;
            currentDistance += intervalDistance;
            i++;
        }

        positionCount = i;
        return false;
    }

    private void LateUpdate()
    {
        if (!ConfigManager.EnableTrajectory.Value)
        {
            if (_line != null) _line.enabled = false;
            if (_sphereRenderer != null) _sphereRenderer.enabled = false;
            return;
        }

        if (_cachedGameWorld == null)
            _cachedGameWorld = Singleton<GameWorld>.Instance;
        GameWorld gameWorld = _cachedGameWorld;
        if (gameWorld == null)
        {
            if (_line != null) _line.enabled = false;
            if (_sphereRenderer != null) _sphereRenderer.enabled = false;
            return;
        }

        Player player = gameWorld.MainPlayer;
        if (player == null || player.HealthController == null || !player.HealthController.IsAlive)
        {
            if (_line != null) _line.enabled = false;
            if (_sphereRenderer != null) _sphereRenderer.enabled = false;
            return;
        }

        _playerVelocity = Vector3.Lerp(player.Velocity, _playerVelocity, 0.9f);

        // 和 VisualAssist 一致：用 Player.GrenadeHandsController
        var grenadeHandsController = player.HandsController as Player.GrenadeHandsController;

        if (grenadeHandsController == null)
        {
            var typeName = player.HandsController?.GetType().Name ?? "null";
            if (typeName != _lastControllerTypeName)
            {
                _lastControllerTypeName = typeName;
                Plugin.log.LogInfo($"[Trajectory] 未手持手雷 (当前控制器: {typeName})");
            }
            if (_line != null) _line.enabled = false;
            if (_sphereRenderer != null) _sphereRenderer.enabled = false;
            return;
        }

        // 控制器类型变化时输出日志
        var controllerName = grenadeHandsController.GetType().Name;
        if (controllerName != _lastControllerTypeName)
        {
            _lastControllerTypeName = controllerName;
            Plugin.log.LogInfo($"[Trajectory] 检测到手雷控制器: {controllerName}");
        }

        // 直接属性访问（Player.GrenadeHandsController 已验证存在这两个属性）
        bool isLowThrow = grenadeHandsController.WaitingForLowThrow;
        bool isHighThrow = grenadeHandsController.WaitingForHighThrow;
        bool inThrowPrep = isHighThrow || isLowThrow;

        // 模式0=手持即显示，模式1=仅准备投掷时显示
        bool shouldShow = ConfigManager.TrajectoryDisplayMode.Value == 0 || inThrowPrep;

        if (!shouldShow)
        {
            if (_line != null) _line.enabled = false;
            if (_sphereRenderer != null) _sphereRenderer.enabled = false;
            return;
        }

        EnsureLinesCreated();

        // 搜索 GrenadePrefab 字段（只搜索一次）并获取手雷质量（有缓存，只在切换手雷时读取）
        if (grenadeHandsController.Item != null && _itemName != grenadeHandsController.Item.Name)
        {
            GrenadePrefab prefab = null;
            var field = FindGrenadePrefabField(grenadeHandsController.GetType());
            if (field != null)
            {
                prefab = field.GetValue(grenadeHandsController) as GrenadePrefab;
            }

            if (prefab != null && prefab.GrenadeItself != null && prefab.GrenadeItself.gameObject != null)
            {
                var rigidbody = prefab.GrenadeItself.gameObject.GetComponent<Rigidbody>();
                if (rigidbody != null)
                {
                    _mass = rigidbody.mass;
                }
            }
            _itemName = grenadeHandsController.Item.Name;
            Plugin.log.LogInfo($"[Trajectory] 手雷: {_itemName}, 质量: {_mass}kg");
        }

        // 计算投掷参数，应用配置的 ThrowForceMultiplier
        GrenadeThrow grenadeThrow = CalculateGrenadeThrow(player, isLowThrow);
        grenadeThrow.ThrowForce *= ConfigManager.ThrowForceMultiplier.Value;

        var throwVelocity = grenadeThrow.ThrowForce / _mass;
        var intervalDistance = ConfigManager.TrajectoryStepTime.Value;
        var maxDistance = ConfigManager.TrajectoryPoints.Value * ConfigManager.TrajectoryStepTime.Value;

        // 抛物线重算节流：当投掷起点/初速变化不大时，间隔 N 帧才重算一次，
        // 避免持雷不动时每帧跑满 60 次 SphereCast。移动或转向瞄准时自动恢复实时重算。
        bool paramsChanged = !_hasComputed
            || (grenadeThrow.ThrowPosition - _lastThrowPos).sqrMagnitude > RecalcMoveSqr
            || (throwVelocity - _lastThrowVelocity).sqrMagnitude > RecalcVelSqr;

        // 人物移动时强制实时重算：世界坐标抛物线锚在旧起点，若不跟随人物，
        // 节流会让线"卡几帧→猛跳一段"，造成移动时持续抖动。静止时才启用节流降开销。
        bool isMoving = _playerVelocity.sqrMagnitude > (0.05f * 0.05f);

        bool collided;
        int positionCount;
        if (paramsChanged || isMoving || _recalcCounter <= 0)
        {
            collided = GetBallisticArcWithLinearDrag(
                _positions, grenadeThrow.ThrowPosition, throwVelocity,
                intervalDistance, maxDistance, _gravity, LinearDrag, out positionCount
            );
            _lastCollided = collided;
            _lastPositionCount = positionCount;
            _lastThrowPos = grenadeThrow.ThrowPosition;
            _lastThrowVelocity = throwVelocity;
            _hasComputed = true;
            _recalcCounter = Math.Max(1, ConfigManager.TrajectoryRecalcFrames.Value);
        }
        else
        {
            _recalcCounter--;
            collided = _lastCollided;
            positionCount = _lastPositionCount;
        }

        // 更新显示
        _line.startColor = ConfigManager.TrajectoryColor.Value;
        _line.endColor = ConfigManager.LandingPointColor.Value;
        _line.startWidth = ConfigManager.TrajectoryLineWidth.Value * 5f;
        _line.endWidth = ConfigManager.TrajectoryLineWidth.Value * 2.5f;
        _line.positionCount = positionCount;
        _line.SetPositions(_positions);
        _line.enabled = true;

        if (collided && positionCount > 0)
        {
            _sphereRenderer.enabled = true;
            _landingSphere.transform.position = _positions[positionCount - 1];
            _sphereRenderer.material.color = ConfigManager.LandingPointColor.Value;
            _landingSphere.transform.localScale = Vector3.one * ConfigManager.LandingPointRadius.Value;
        }
        else
        {
            _sphereRenderer.enabled = false;
        }
    }

    /// <summary>
    /// 反射获取 HitMask（只执行一次，缓存结果）
    /// 4.1 中 LayerMasksDataAbstractClass 可能改名，扩展搜索策略
    /// </summary>
    private static int GetHitMask()
    {
        if (_cachedHitMask >= 0) return _cachedHitMask;

        try
        {
            // 先搜 Assembly-CSharp（游戏主程序集），性能最高
            Assembly csharpAsm = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "Assembly-CSharp")
                { csharpAsm = asm; break; }
            }

            if (csharpAsm != null)
            {
                foreach (var type in csharpAsm.GetTypes())
                {
                    // 搜类名包含 LayerMask / HitMask / Layer 的静态类
                    if (type.IsClass && (type.Name.Contains("LayerMask") || type.Name.Contains("HitMask") || type.Name.Contains("Layer")))
                    {
                        // 找静态字段类型为 Mask / LayerMask / 有 .value 属性的 int 包装
                        foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                        {
                            if (f.Name == "HitMask" || f.FieldType.Name.Contains("Mask"))
                            {
                                var val = f.GetValue(null);
                                if (val == null) continue;
                                var vp = val.GetType().GetProperty("value");
                                if (vp != null && vp.PropertyType == typeof(int))
                                {
                                    _cachedHitMask = (int)vp.GetValue(val);
                                    Plugin.log.LogInfo($"[Trajectory] HitMask 获取成功: {type.Name}.{f.Name} = {_cachedHitMask}");
                                    return _cachedHitMask;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Plugin.log.LogWarning($"[Trajectory] 获取 HitMask 异常: {e.Message}");
        }

        // 兜底：用合理的层掩码，排除 UI(5)、Water(4)、IgnoreRaycast(2) 等常见不相关层
        // 0 = Default, 1 = TransparentFX, 3 = 空, 6,7,8,9,10,11,12,13,14,15,16+ 游戏自定义层
        _cachedHitMask = ~(1 << 2 | 1 << 5); // 排除 IgnoreRaycast 和 UI
        Plugin.log.LogWarning($"[Trajectory] 使用兜底 HitMask: ~(IgnoreRaycast|UI) = {_cachedHitMask}");
        return _cachedHitMask;
    }

    private void OnDestroy()
    {
        if (_line != null) Destroy(_line);
        if (_landingSphere != null) Destroy(_landingSphere);
    }
}

public struct GrenadeThrow
{
    public Vector3 ThrowPosition;
    public Vector3 ThrowForce;
}
