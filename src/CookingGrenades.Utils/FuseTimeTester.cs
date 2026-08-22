using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using CookingGrenades.Config;
using UnityEngine;

namespace CookingGrenades.Utils;

internal class FuseTimeTester
{
	public static void Init()
	{
		ConfigEntry<bool> timeSimulationOutput = ConfigManager.TimeSimulationOutput;
		timeSimulationOutput.SettingChanged += delegate
		{
			if (timeSimulationOutput.Value)
			{
				RunFuseTimeTest(ConfigManager.TimeSimulationValue.Value);
				timeSimulationOutput.Value = false;
			}
		};
	}

	public static void RunFuseTimeTest(float tsetValue)
	{
		float[] array = new float[ConfigManager.FuseTimeTestCount.Value];
		Dictionary<float, int> dictionary = new Dictionary<float, int>();
		float num = float.MaxValue;
		float num2 = float.MinValue;
		float num3 = 0f;
		float key;
		int value;
		for (int i = 0; i < ConfigManager.FuseTimeTestCount.Value; i++)
		{
			float num4 = (array[i] = MathUtils.GenerateNormalRandomFast(tsetValue, tsetValue * ConfigManager.FuseTimeSpreadFactor.Value));
			float num5 = (float)Math.Round(num4, 1);
			if (dictionary.ContainsKey(num5))
			{
				key = num5;
				value = dictionary[key]++;
			}
			else
			{
				dictionary.Add(num5, 1);
			}
			num = Mathf.Min(num, num4);
			num2 = Mathf.Max(num2, num4);
			num3 += num4;
		}
		float num6 = num3 / (float)ConfigManager.FuseTimeTestCount.Value;
		float num7 = 0f;
		for (int j = 0; j < ConfigManager.FuseTimeTestCount.Value; j++)
		{
			num7 += Mathf.Pow(array[j] - num6, 2f);
		}
		float num8 = Mathf.Sqrt(num7 / (float)ConfigManager.FuseTimeTestCount.Value);
		Plugin.log.LogInfo((object)"=== Distribution Test Results ===");
		Plugin.log.LogInfo((object)$"Target Value: {tsetValue}, FuseTime Spread Factor: {ConfigManager.FuseTimeSpreadFactor.Value}, Test Count {ConfigManager.FuseTimeTestCount.Value}");
		Plugin.log.LogInfo((object)$"Minimum Time: {num:F3} seconds");
		Plugin.log.LogInfo((object)$"Maximum Time: {num2:F3} seconds");
		Plugin.log.LogInfo((object)$"Average Time: {num6:F3} seconds");
		Plugin.log.LogInfo((object)$"Standard Deviation: {num8:F3} seconds");
		Plugin.log.LogInfo((object)"=== Frequency Distribution ===");
		foreach (KeyValuePair<float, int> item in dictionary.OrderBy((KeyValuePair<float, int> x) => x.Key))
		{
			item.Deconstruct(out key, out value);
			float num9 = key;
			int num10 = value;
			float num11 = (float)num10 / (float)ConfigManager.FuseTimeTestCount.Value * 100f;
			Plugin.log.LogInfo((object)$"Time: {num9,5:F2}, Count: {num10,5}, Percentage: {num11,6:F2}%");
		}
	}
}