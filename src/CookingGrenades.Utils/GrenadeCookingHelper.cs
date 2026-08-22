using System.Collections.Generic;
using System.Linq;
using AnimationEventSystem;
using CookingGrenades.Config;
using CookingGrenades.Patches;
using EFT;
using EFT.Communications;
using EFT.InventoryLogic;
using UnityEngine;

namespace CookingGrenades.Utils;

public static class GrenadeCookingHelper
{
	public static class GrenadeIDs
	{
		public const string GRENADE_F1_HAND = "5710c24ad2720bc3458b45a3";

		public const string GRENADE_M18_SMOKE_GRENADE_GREEN = "617aa4dd8166f034d57de9c5";

		public const string GRENADE_M67_HAND = "58d3db5386f77426186285a0";

		public const string GRENADE_MODEL_7290_FLASH_BANG = "619256e5f8af2c1a4e1f5d92";

		public const string GRENADE_RDG2B_SMOKE = "5a2a57cfc4a2826c6e06d44a";

		public const string GRENADE_RGD5_HAND = "5448be9a4bdc2dfd2f8b456a";

		public const string GRENADE_RGN_HAND = "617fd91e5539a84ec44ce155";

		public const string GRENADE_RGO_HAND = "618a431df1eb8e24b8741deb";

		public const string GRENADE_V40_MINI = "66dae7cbeb28f0f96809f325";

		public const string GRENADE_VOG17_KHATTABKA_IMPROVISED_HAND = "5e32f56fcb6d5863cc5e5ee4";

		public const string GRENADE_VOG25_KHATTABKA_IMPROVISED_HAND = "5e340dcdcb6d5863cc5e5efb";

		public const string GRENADE_ZARYA_STUN = "5a0c27731526d80618476ac4";
	}

	private static readonly HashSet<string> SkipFuseSoundGrenades = new HashSet<string> { "58d3db5386f77426186285a0", "66dae7cbeb28f0f96809f325", "617aa4dd8166f034d57de9c5", "619256e5f8af2c1a4e1f5d92", "5a2a57cfc4a2826c6e06d44a" };

	public static void StartCookingWithLeverSound(EFT.Player.GrenadeHandsController controller)
	{
		GrenadeCookingTimer cookingTimer = GrenadeCookingManager.GetCookingTimer();
		cookingTimer.SetCookingItem(controller);
		PlaySound(controller);
		if (ConfigManager.EnableCookingNotification.Value)
		{
			NotificationManager.DisplayMessageNotification("Cooking Started", (ENotificationDurationType)0, (ENotificationIconType)0, (Color?)null);
		}
		cookingTimer.StartCooking(controller);
	}

	private static void PlaySound(EFT.Player.GrenadeHandsController controller)
	{
		IAnimator animator = controller.FirearmsAnimator.Animator;
		BaseSoundPlayer component = controller.ControllerGameObject.GetComponent<BaseSoundPlayer>();
		AnimationEvent val = controller.AnimationEventsEmitter._animationEventsStateBehaviours.OfType<AnimationEventsStateBehaviour>().SelectMany((AnimationEventsStateBehaviour x) => x.AnimationEvents).FirstOrDefault((AnimationEvent evt) => evt._functionName == "SoundAtPoint" && evt.Parameter.StringParam == "SndFuse");
		if (val == null || (ConfigManager.UseAlternativePinSound.Value && ShouldSkipFuseSound(((Item)controller.Item).StringTemplateId)))
		{
			component.SoundEventHandler("TripwirePin");
		}
		else
		{
			AnimatorStateInfoWrapper currentAnimatorStateInfo = animator.GetCurrentAnimatorStateInfo(1);
			controller.AnimationEventsEmitter.FireEventIfConditionsPassed(val, animator, ref currentAnimatorStateInfo, val.Time);
		}
		BaseSoundPlayerOnSoundAtPointPatch.HaveToNotRunFuseSound = component;
	}

	private static bool ShouldSkipFuseSound(string input)
	{
		return SkipFuseSoundGrenades.Contains(input);
	}
}