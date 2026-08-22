using BepInEx.Configuration;
using UnityEngine;

namespace CookingGrenades.Config;

internal static class ConfigManager
{
	public static ConfigEntry<bool> EnableCookingNotification;

	public static ConfigEntry<float> AutoThrowLeadTime;

	public static ConfigEntry<bool> ShowDefaultFuseTimeInInventoryUI;

	public static ConfigEntry<bool> UseAlternativePinSound;

	public static ConfigEntry<bool> RealisticFuseTimeEnable;

	public static ConfigEntry<float> FuseTimeSpreadFactor;

	public static ConfigEntry<float> TimeSimulationValue;

	public static ConfigEntry<int> FuseTimeTestCount;

	public static ConfigEntry<bool> TimeSimulationOutput;

	public static ConfigEntry<bool> DebugGUI;

	public static ConfigEntry<bool> UserWarningConfirmed;

	// 抛物线预测配置
	public static ConfigEntry<bool> EnableTrajectory;
	public static ConfigEntry<int> TrajectoryDisplayMode;
	public static ConfigEntry<Color> TrajectoryColor;
	public static ConfigEntry<Color> LandingPointColor;
	public static ConfigEntry<float> LandingPointRadius;
	public static ConfigEntry<int> TrajectoryPoints;
	public static ConfigEntry<float> TrajectoryStepTime;
	public static ConfigEntry<float> TrajectoryLineWidth;
	public static ConfigEntry<float> ThrowForceMultiplier;
	public static ConfigEntry<int> TrajectoryRecalcFrames;

	// 温雷提示配置
	public static ConfigEntry<bool> EnableCookIndicator;
	public static ConfigEntry<float> CookIndicatorHeight;
	public static ConfigEntry<float> CookIndicatorScale;
	public static ConfigEntry<float> CookIndicatorOffsetX;
	public static ConfigEntry<float> CookIndicatorOffsetY;
	public static ConfigEntry<float> CookIndicatorAnimDuration;

	// 手雷轮盘配置
	public static ConfigEntry<bool> EnableGrenadeWheel;
	public static ConfigEntry<KeyCode> GrenadeWheelKey;
	public static ConfigEntry<bool> EquipImmediatelyOnSelect;
	public static ConfigEntry<bool> SwitchImmediatelyWhenHolding;

	// 医药轮盘配置
	public static ConfigEntry<bool> EnableMedicineWheel;
	public static ConfigEntry<KeyCode> MedicineWheelKey;
	public static ConfigEntry<bool> MedicineWheelScanSecure;
	public static ConfigEntry<bool> MedicineWheelScanBackpack;
	public static ConfigEntry<bool> MedicineWheelIncludeFood;

	public static void Init(ConfigFile configFile)
	{
		EnableCookingNotification = configFile.Bind<bool>("0. Cooking Grenades", "Enable Cooking Notification", false, new ConfigDescription("Show a notification when grenade cooking starts", (AcceptableValueBase)null, new object[1]
		{
			new ConfigurationManagerAttributes
			{
				Order = 1
			}
		}));
		AutoThrowLeadTime = configFile.Bind<float>("0. Cooking Grenades", "Auto Throw Lead Time", 0.8f, new ConfigDescription("Fuse time remaining (seconds) before a cooking grenade is force-thrown to avoid exploding in hand. Higher = thrown earlier (safer).", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0.1f, 3f), new object[1]
		{
			new ConfigurationManagerAttributes
			{
				Order = 2
			}
		}));
		ShowDefaultFuseTimeInInventoryUI = configFile.Bind<bool>("0. Cooking Grenades", "Show Default Fuse Time In Inventory UI", true, new ConfigDescription("If enabled, shows the default fuse time in inventory UI instead of randomized value.", (AcceptableValueBase)null, new object[1]
		{
			new ConfigurationManagerAttributes()
		}));
		UseAlternativePinSound = configFile.Bind<bool>("0. Cooking Grenades", "Use Alternative Pin Sound", true, new ConfigDescription("If enabled, plays an alternative pin sound (TripwirePin) for certain grenades instead of the fuse sound.\nAffected grenades: M67, V40, M18, M7290, RDG-2B", (AcceptableValueBase)null, new object[1]
		{
			new ConfigurationManagerAttributes
			{
				Order = -1
			}
		}));
		RealisticFuseTimeEnable = configFile.Bind<bool>("1. Realistic Fuse Time", "Realistic Fuse Time Enable", true, new ConfigDescription("", (AcceptableValueBase)null, new object[1]
		{
			new ConfigurationManagerAttributes
			{
				Order = 2
			}
		}));
		FuseTimeSpreadFactor = configFile.Bind<float>("1. Realistic Fuse Time", "Fuse Time Spread Factor", 0.085f, new ConfigDescription("Controls how much grenade fuse times vary (0.001 = almost fixed, 0.6 = wide range).", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0.001f, 0.6f), new object[1]
		{
			new ConfigurationManagerAttributes
			{
				Order = 1
			}
		}));
		TimeSimulationValue = configFile.Bind<float>("2. Realistic Fuse Iime Tester", "Simulation Target Value", 5f, new ConfigDescription("The value you want to simulate", (AcceptableValueBase)(object)new AcceptableValueRange<float>(1f, 10f), new object[1]
		{
			new ConfigurationManagerAttributes()
		}));
		FuseTimeTestCount = configFile.Bind<int>("2. Realistic Fuse Iime Tester", "Fuse Time Test Count", 10000, new ConfigDescription("Number of iterations for fuse time distribution test.", (AcceptableValueBase)(object)new AcceptableValueRange<int>(1, 100000), new object[1]
		{
			new ConfigurationManagerAttributes()
		}));
		TimeSimulationOutput = configFile.Bind<bool>("2. Realistic Fuse Iime Tester", "Time Simulation To Output", false, new ConfigDescription("The simulation will run once when the value is set to true.\nYou can check the results in BepInEx/LogOutput.log.", (AcceptableValueBase)null, new object[1]
		{
			new ConfigurationManagerAttributes()
		}));
		DebugGUI = configFile.Bind<bool>("3. Debug", "Enable Cooking Time GUI", false, new ConfigDescription("", (AcceptableValueBase)null, new object[1]
		{
			new ConfigurationManagerAttributes()
		}));
		UserWarningConfirmed = configFile.Bind<bool>("3. Debug", "User Warning Confirmed", false, new ConfigDescription("", (AcceptableValueBase)null, new object[1]
		{
			new ConfigurationManagerAttributes
			{
				IsAdvanced = true
			}
		}));

		// 抛物线预测配置
		EnableTrajectory = configFile.Bind<bool>("4. Trajectory Prediction", "Enable Trajectory", true, new ConfigDescription("Enable grenade trajectory prediction line.", null, new object[1]
		{
			new ConfigurationManagerAttributes { Order = 10 }
		}));
		TrajectoryDisplayMode = configFile.Bind<int>("4. Trajectory Prediction", "Display Mode", 0, new ConfigDescription("0 = Always when holding grenade, 1 = Only when aiming (holding fire button).", new AcceptableValueRange<int>(0, 1), new object[1]
		{
			new ConfigurationManagerAttributes { Order = 9 }
		}));
		TrajectoryColor = configFile.Bind<Color>("4. Trajectory Prediction", "Trajectory Color", new Color(1f, 0.3f, 0.1f, 0.8f), new ConfigDescription("Color of the trajectory line.", null, new object[1]
		{
			new ConfigurationManagerAttributes { Order = 8 }
		}));
		LandingPointColor = configFile.Bind<Color>("4. Trajectory Prediction", "Landing Point Color", new Color(1f, 0f, 0f, 0.9f), new ConfigDescription("Color of the landing point marker.", null, new object[1]
		{
			new ConfigurationManagerAttributes { Order = 7 }
		}));
		LandingPointRadius = configFile.Bind<float>("4. Trajectory Prediction", "Landing Point Radius", 0.3f, new ConfigDescription("Radius of the landing point marker circle.", new AcceptableValueRange<float>(0.05f, 2f), new object[1]
		{
			new ConfigurationManagerAttributes { Order = 6 }
		}));
		TrajectoryPoints = configFile.Bind<int>("4. Trajectory Prediction", "Trajectory Points", 60, new ConfigDescription("Number of sample points along the trajectory. More = smoother but heavier.", new AcceptableValueRange<int>(10, 200), new object[1]
		{
			new ConfigurationManagerAttributes { Order = 5 }
		}));
		TrajectoryStepTime = configFile.Bind<float>("4. Trajectory Prediction", "Trajectory Step Size", 0.5f, new ConfigDescription("Horizontal distance (meters) between each trajectory sample point. Smaller = more points = smoother.", new AcceptableValueRange<float>(0.1f, 2f), new object[1]
		{
			new ConfigurationManagerAttributes { Order = 4 }
		}));
		TrajectoryLineWidth = configFile.Bind<float>("4. Trajectory Prediction", "Trajectory Line Width", 0.015f, new ConfigDescription("Width of the trajectory line.", new AcceptableValueRange<float>(0.005f, 0.1f), new object[1]
		{
			new ConfigurationManagerAttributes { Order = 3 }
		}));
		ThrowForceMultiplier = configFile.Bind<float>("4. Trajectory Prediction", "Throw Force Multiplier", 1f, new ConfigDescription("Multiplier for throw force estimation. Adjust if trajectory doesn't match actual throw distance.", new AcceptableValueRange<float>(0.5f, 3f), new object[1]
		{
			new ConfigurationManagerAttributes { Order = 2 }
		}));
		// 未移动/未瞄准微调时，间隔 N 帧才重算一次抛物线，显著降低持雷时的物理检测开销
		TrajectoryRecalcFrames = configFile.Bind<int>("4. Trajectory Prediction", "Recalc Interval (frames)", 2, new ConfigDescription("Recalculate the trajectory at most once per N frames when throw params are unchanged. 1 = recalc every frame. Higher = cheaper but less responsive.", new AcceptableValueRange<int>(1, 10), new object[1]
		{
			new ConfigurationManagerAttributes { Order = 1 }
		}));

		// 温雷提示配置
		EnableCookIndicator = configFile.Bind<bool>("5. Cook Indicator", "Enable Cook Indicator", true, new ConfigDescription("Show a blinking indicator above the grenade while cooking. Blink rate increases as time runs out, turns red in the last 2 seconds.", null, new object[1]
		{
			new ConfigurationManagerAttributes { Order = 10 }
		}));
		CookIndicatorHeight = configFile.Bind<float>("5. Cook Indicator", "Indicator Height", 1f, new ConfigDescription("Height of the indicator above the grenade (meters).", new AcceptableValueRange<float>(0.1f, 2f), new object[1]
		{
			new ConfigurationManagerAttributes { Order = 9 }
		}));
		CookIndicatorScale = configFile.Bind<float>("5. Cook Indicator", "Indicator Scale", 0.15f, new ConfigDescription("Size of the indicator icon.", new AcceptableValueRange<float>(0.05f, 1f), new object[1]
		{
			new ConfigurationManagerAttributes { Order = 8 }
		}));
		CookIndicatorOffsetX = configFile.Bind<float>("5. Cook Indicator", "Offset X (px)", 200f, new ConfigDescription("Horizontal offset from screen center in pixels. Positive = right, negative = left.", new AcceptableValueRange<float>(-500f, 500f), new object[1]
		{
			new ConfigurationManagerAttributes { Order = 7 }
		}));
		CookIndicatorOffsetY = configFile.Bind<float>("5. Cook Indicator", "Offset Y (px)", 0f, new ConfigDescription("Vertical offset from screen center in pixels. Positive = up, negative = down.", new AcceptableValueRange<float>(-500f, 500f), new object[1]
		{
			new ConfigurationManagerAttributes { Order = 6 }
		}));
		CookIndicatorAnimDuration = configFile.Bind<float>("5. Cook Indicator", "Throw Animation Duration (s)", 2.5f, new ConfigDescription("Duration of the throw rotation+fade animation in seconds.", new AcceptableValueRange<float>(0.1f, 5f), new object[1]
		{
			new ConfigurationManagerAttributes { Order = 5 }
		}));

		// 手雷轮盘配置
		EnableGrenadeWheel = configFile.Bind<bool>("6. Grenade Wheel", "Enable Grenade Wheel", true, new ConfigDescription("Enable the grenade wheel selector. Hold the configured key to show a radial wheel of available grenades, move mouse to select, release to equip.", null, new object[1]
		{
			new ConfigurationManagerAttributes { Order = 10 }
		}));
		GrenadeWheelKey = configFile.Bind<KeyCode>("6. Grenade Wheel", "Grenade Wheel Key", KeyCode.G, new ConfigDescription("Key to hold to open the grenade wheel.", null, new object[1]
		{
			new ConfigurationManagerAttributes { Order = 9 }
		}));
		EquipImmediatelyOnSelect = configFile.Bind<bool>("6. Grenade Wheel", "Equip Immediately On Select", true, new ConfigDescription("If enabled, the selected grenade is immediately equipped in hand when the wheel closes. If disabled, only sets it as preferred grenade (press G again to take it out).", null, new object[1]
		{
			new ConfigurationManagerAttributes { Order = 8 }
		}));
		SwitchImmediatelyWhenHolding = configFile.Bind<bool>("6. Grenade Wheel", "Switch Immediately When Holding", true, new ConfigDescription("If enabled and you already have a grenade in hand, selecting a different one in the wheel will immediately switch to it. If disabled, only sets the preference without switching (you need to press G again).", null, new object[1]
		{
			new ConfigurationManagerAttributes { Order = 7 }
		}));

		// 医药轮盘配置
		EnableMedicineWheel = configFile.Bind<bool>("7. Medicine Wheel", "Enable Medicine Wheel", true, new ConfigDescription("Enable the medicine wheel selector. Hold the configured key to show a radial wheel of available medicine, move mouse to select, release to use.", null, new object[1]
		{
			new ConfigurationManagerAttributes { Order = 10 }
		}));
		MedicineWheelKey = configFile.Bind<KeyCode>("7. Medicine Wheel", "Medicine Wheel Key", KeyCode.H, new ConfigDescription("Key to hold to open the medicine wheel.", null, new object[1]
		{
			new ConfigurationManagerAttributes { Order = 9 }
		}));
		MedicineWheelScanSecure = configFile.Bind<bool>("7. Medicine Wheel", "Scan Secure Container For Medicine", true, new ConfigDescription("If enabled, the secure container is also scanned for medicine.", null, new object[1]
		{
			new ConfigurationManagerAttributes { Order = 8 }
		}));
		MedicineWheelScanBackpack = configFile.Bind<bool>("7. Medicine Wheel", "Scan Backpack For Medicine", false, new ConfigDescription("If enabled, the backpack is also scanned for medicine. If disabled, only pockets and tactical rig are scanned. The secure container scan is controlled by its own option.", null, new object[1]
		{
			new ConfigurationManagerAttributes { Order = 7 }
		}));
		MedicineWheelIncludeFood = configFile.Bind<bool>("7. Medicine Wheel", "Include Food And Drinks", true, new ConfigDescription("If enabled, food and drinks also appear in the medicine wheel.", null, new object[1]
		{
			new ConfigurationManagerAttributes { Order = 6 }
		}));
	}
}