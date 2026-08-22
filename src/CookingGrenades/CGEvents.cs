using System;

namespace CookingGrenades;

/// <summary>
/// 温雷（烹饪）事件广播。投掷发生时触发，携带烹饪时长（秒）。
/// 供第三方插件（如 CGFika 联机同步）订阅。
/// </summary>
public static class CGEvents
{
    /// <summary>手雷投掷事件，参数为烹饪时长（秒），0 表示未烹饪</summary>
    public static event Action<float> OnGrenadeThrown;

    internal static void Fire(float cookTime)
    {
        try
        {
            OnGrenadeThrown?.Invoke(cookTime);
        }
        catch
        {
            // 订阅者异常不影响主逻辑
        }
    }
}
