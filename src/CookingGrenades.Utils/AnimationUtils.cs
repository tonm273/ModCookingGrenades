using System;
using System.Linq;
using System.Reflection;
using SPT.Reflection.Utils;

namespace CookingGrenades.Utils;

public static class AnimationUtils
{
	public static readonly MethodInfo GetAnimStateMethod;

	static AnimationUtils()
	{
		GetAnimStateMethod = PatchConstants.EftTypes.SelectMany((Type t) => t.GetMethods(BindingFlags.Static | BindingFlags.Public)).FirstOrDefault((MethodInfo m) => m.Name == "GetAnimStateByNameHash" && m.ReturnType == typeof(string) && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(int));
		if (GetAnimStateMethod == null)
		{
			Plugin.log.LogError((object)"Failed to find GetAnimStateByNameHash in EftTypes");
		}
	}

	public static bool IsRemovePullRingCompleted(IAnimator animator)
	{
		string text = (string)GetAnimStateMethod.Invoke(null, new object[1] { animator.GetCurrentAnimatorStateInfo(1).shortNameHash });
		if (text != "ALT FIRE START")
		{
			return text != "FIRE START";
		}
		return false;
	}
}