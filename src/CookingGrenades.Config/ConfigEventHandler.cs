using CookingGrenades.Patches;

namespace CookingGrenades.Config;

public static class ConfigEventHandler
{
	public static void Init()
	{
		ConfigManager.FuseTimeSpreadFactor.SettingChanged += delegate
		{
			ThrowWeapGetExplDelayPatch.ResetExplDelay();
		};
	}
}