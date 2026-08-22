using System;
using System.Collections.Generic;
using CookingGrenades.Config;
using UnityEngine;

namespace CookingGrenades.Utils;

public class DebugDisplay : MonoBehaviour
{
	private class DisplayItem
	{
		public string Label;

		public Func<object> ValueProvider;

		public DisplayItem(string label, Func<object> valueProvider)
		{
			Label = label;
			ValueProvider = valueProvider;
		}

		public object GetValue()
		{
			try
			{
				return ValueProvider();
			}
			catch (Exception)
			{
				return "System.Exception";
			}
		}
	}

	private static DebugDisplay _instance;

	public bool Enable;

	private Rect _windowRect = new Rect(20f, 20f, 300f, 50f);

	private Vector2 _scrollPosition = Vector2.zero;

	private List<DisplayItem> _displayItems = new List<DisplayItem>();

	public static DebugDisplay Instance
	{
		get
		{
			if ((UnityEngine.Object)(object)_instance == (UnityEngine.Object)null)
			{
				GameObject val = new GameObject("DebugDisplay");
				_instance = val.AddComponent<DebugDisplay>();
			UnityEngine.Object.DontDestroyOnLoad((UnityEngine.Object)val);
			}
			return _instance;
		}
	}

	public void InsertDisplayObject(string label, Func<object> valueProvider, bool checkDuplicate = true)
	{
		if (!checkDuplicate)
		{
			_displayItems.Add(new DisplayItem(label, valueProvider));
		}
		else if (_displayItems.Find((DisplayItem x) => x.Label == label) == null)
		{
			_displayItems.Add(new DisplayItem(label, valueProvider));
		}
	}

	public void RemoveDisplayObject(string label)
	{
		_displayItems.RemoveAll((DisplayItem item) => item.Label == label);
	}

	public void ClearDisplayObjects()
	{
		_displayItems.Clear();
	}

	private void Awake()
	{
		if ((UnityEngine.Object)(object)_instance != (UnityEngine.Object)null && (UnityEngine.Object)(object)_instance != (UnityEngine.Object)(object)this)
		{
			UnityEngine.Object.Destroy((UnityEngine.Object)(object)((Component)this).gameObject);
			return;
		}
		_instance = this;
		UnityEngine.Object.DontDestroyOnLoad((UnityEngine.Object)(object)((Component)this).gameObject);
	}

	private void OnGUI()
	{
		if (ConfigManager.DebugGUI.Value)
		{
			_windowRect = GUI.Window(0, _windowRect, new GUI.WindowFunction(DrawWindow), "Debug Window");
		}
	}

	private void DrawWindow(int windowId)
	{
		GUILayout.BeginVertical(Array.Empty<GUILayoutOption>());
		float num = CalculateContentHeight();
		float num2 = Mathf.Clamp(num + 60f, 100f, 400f);
		_windowRect.height = num2;
		if (num > 340f)
		{
			_scrollPosition = GUILayout.BeginScrollView(_scrollPosition, (GUILayoutOption[])(object)new GUILayoutOption[2]
			{
				GUILayout.Width(280f),
				GUILayout.Height(num2 - 60f)
			});
		}
		if (_displayItems.Count == 0)
		{
			GUILayout.Label("No objects to display.", Array.Empty<GUILayoutOption>());
		}
		else
		{
			foreach (DisplayItem displayItem in _displayItems)
			{
				object value = displayItem.GetValue();
				GUILayout.Label(FormatDisplayText(displayItem.Label, value), Array.Empty<GUILayoutOption>());
			}
		}
		if (num > 340f)
		{
			GUILayout.EndScrollView();
		}
		if (GUILayout.Button("Clear All", Array.Empty<GUILayoutOption>()))
		{
			ClearDisplayObjects();
		}
		GUILayout.EndVertical();
		GUI.DragWindow();
	}

	private float CalculateContentHeight()
	{
		float num = GUI.skin.label.CalcHeight(new GUIContent("A"), 280f);
		int num2 = ((_displayItems.Count <= 0) ? 1 : _displayItems.Count);
		return num * (float)num2 + 10f;
	}

	private string FormatDisplayText(string label, object value)
	{
		if (value == null)
		{
			return label + ": Null";
		}
		return $"{label}: {value}";
	}
}