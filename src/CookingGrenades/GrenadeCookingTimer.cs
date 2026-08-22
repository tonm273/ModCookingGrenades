using System;
using System.Collections;
using System.Reflection;
using Comfort.Common;
using CookingGrenades.Config;
using CookingGrenades.Utils;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace CookingGrenades;

public class GrenadeCookingTimer
{
	private float _cookingStartTime;

	public Coroutine existingCoroutine;

	private CookIndicator _cookIndicator;

	public float CookingStartTime => _cookingStartTime;

	public bool IsCooking
	{
		get
		{
			if (_cookingStartTime > 0f)
			{
				return Controller != null;
			}
			return false;
		}
	}

	public EFT.Player.GrenadeHandsController Controller { get; private set; }

	public GrenadeCookingTimer()
	{
		Controller = null;
	}

	public GrenadeCookingTimer(EFT.Player.GrenadeHandsController controller)
	{
		Controller = controller;
	}

	public void SetCookingItem(EFT.Player.GrenadeHandsController controller)
	{
		Controller = controller;
		_cookingStartTime = 0f;
	}

	public void StartCooking(EFT.Player.GrenadeHandsController controller)
	{
		_cookingStartTime = Time.time;
		existingCoroutine = ((MonoBehaviour)controller).StartCoroutine(ForceThrowCoroutine(controller));

		// 温雷提示图标
		if (ConfigManager.EnableCookIndicator.Value)
		{
			ThrowWeap throwWeap = controller.Item as ThrowWeap;
			if (throwWeap != null)
			{
				GameObject indicatorObj = new GameObject("CookIndicator");
				_cookIndicator = indicatorObj.AddComponent<CookIndicator>();
				_cookIndicator.Initialize(controller.transform, throwWeap, _cookingStartTime);
				Plugin.log.LogInfo("[CookIndicator] 温雷提示已启动");
			}
		}

		if (!ConfigManager.DebugGUI.Value)
		{
			return;
		}
		DebugDisplay.Instance.InsertDisplayObject("Cooking Time ", delegate
		{
			Player mainPlayer = Singleton<GameWorld>.Instance.MainPlayer;
			float num = 0f;
			Item item = mainPlayer.HandsController.Item;
			ThrowWeap val = (ThrowWeap)(object)((item is ThrowWeap) ? item : null);
			if (val != null)
			{
				num = val.GetExplDelay;
			}
			return $"{GetCookingTime():F3}/{num:F3} sec ({GetCookingTime() / num * 100f:F1}%)";
		});
	}

	public float GetCookingTime()
	{
		if (!IsCooking)
		{
			return 0f;
		}
		return Time.time - _cookingStartTime;
	}

	public void Reset(EFT.Player.GrenadeHandsController oldController)
	{
		// 温雷提示：投掷时播放旋转消失动画（绕屏幕中心逆时针45°，时长可在F12菜单配置）
		if (_cookIndicator != null)
		{
			_cookIndicator.PlayThrowAnimation();
			_cookIndicator = null;
		}

		Controller = null;
		_cookingStartTime = 0f;
		((MonoBehaviour)oldController).StopCoroutine(existingCoroutine);
		oldController = null;
	}

	private IEnumerator ForceThrowCoroutine(EFT.Player.GrenadeHandsController controller)
	{
		// 引信剩余 AutoThrowLeadTime 秒时强制投出，避免在手中爆炸（可配置，默认 0.8s）
		yield return (object)new WaitForSeconds(controller.Item.GetExplDelay - Math.Max(ConfigManager.AutoThrowLeadTime.Value, 0.05f));
		if (IsCooking && controller != null)
		{
			ForceThrow(controller);
		}
	}

	private void ForceThrow(EFT.Player.GrenadeHandsController controller)
	{
		MethodInfo methodInfo = (controller.WaitingForHighThrow ? AccessTools.Method(typeof(EFT.Player.GrenadeHandsController), "HandleFireInput", (Type[])null, (Type[])null) : AccessTools.Method(typeof(EFT.Player.GrenadeHandsController), "HandleAltFireInput", (Type[])null, (Type[])null));
		if (methodInfo != null)
		{
			methodInfo.Invoke(controller, null);
		}
		else
		{
			Plugin.log.LogError((object)"Throw method not found");
		}
	}
}
