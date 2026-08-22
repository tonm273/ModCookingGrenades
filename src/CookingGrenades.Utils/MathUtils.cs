using System;
using UnityEngine;

namespace CookingGrenades.Utils;

public class MathUtils
{
	public static float GenerateNormalRandomBoxMuller(float mean, float stdDev)
	{
		float value = UnityEngine.Random.value;
		float value2 = UnityEngine.Random.value;
		float num = Mathf.Sqrt(-2f * Mathf.Log(value)) * Mathf.Cos(Mathf.PI * 2f * value2);
		return mean + stdDev * num;
	}

	public static float GenerateNormalRandomFast(float mean, float stdDev)
	{
		float value = UnityEngine.Random.value;
		float value2 = UnityEngine.Random.value;
		float num = (value + value2 - 1f) * 1.732f;
		return mean + stdDev * num;
	}
}