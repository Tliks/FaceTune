namespace Aoyon.FaceTune.Gui;

internal static class LocalizedUI
{
    public static void PropertyField(SerializedProperty property, string key, bool includeChildren = true)
    {
        EditorGUILayout.PropertyField(property, key.G(), includeChildren);
    }

    public static void PropertyField(Rect position, SerializedProperty property, string key, bool includeChildren = true)
    {
        EditorGUI.PropertyField(position, property, key.G(), includeChildren);
    }
}

internal sealed class LocalizedPopup : IDisposable
{
	private readonly string? _labelKey;
	private GUIContent? _labelContent;

	private readonly string[] _optionKeys;
    private GUIContent[] _displayContents;

	// PropertyDrawerで使う場合Disposeできない
	// その場合にOnLanguageChangedの不要な更新が積み重なる問題を回避する為に、描画が呼ばれたときに始めて更新されるようにする
	private bool _rebuildRequired;
	private bool _disposed;

	public LocalizedPopup(string? labelKey, IEnumerable<string> optionKeys)
	{
		_labelKey = labelKey;
		_optionKeys = optionKeys.ToArray();
		(_, _displayContents) = BuildDisplayContents();
		Localization.OnLanguageChanged += OnLanguageChanged;
	}

	/// <summary>
	/// labelKey: labelKey
	/// optionKeys: {enumType.Name}:enum:enumValue
	/// </summary>
	public LocalizedPopup(string labelKey, Type enumType) : this(labelKey, enumType.GetEnumNames().Select(k => $"{enumType.Name}:enum:{k}"))
	{
	}

	/// <summary>
	/// labelKey: {enumType.Name}
	/// optionKeys: {enumType.Name}:enum:enumValue
	/// </summary>
	public LocalizedPopup(Type enumType) : this(enumType.Name, enumType.GetEnumNames().Select(k => $"{enumType.Name}:enum:{k}"))
	{
	}

	private void OnLanguageChanged()
	{
		if (_disposed) return;
		_rebuildRequired = true;
	}

	private (GUIContent? labelContent, GUIContent[] displayContents) BuildDisplayContents()
	{
		_labelContent = _labelKey == null ? null : _labelKey.G();
        _displayContents = _optionKeys.Select(k => k.G()).ToArray();
		return (_labelContent, _displayContents);
	}

	public int Draw(int selectedIndex, params GUILayoutOption[] options)
	{
		if (_disposed) return selectedIndex;
		if (_rebuildRequired)
		{
			(_, _displayContents) = BuildDisplayContents();
			_rebuildRequired = false;
		}
		return EditorGUILayout.Popup(_labelContent ?? GUIContent.none, selectedIndex, _displayContents, options);
	}

	public int Draw(Rect position, int selectedIndex)
	{
		if (_disposed) return selectedIndex;
		if (_rebuildRequired)
		{
			(_, _displayContents) = BuildDisplayContents();
			_rebuildRequired = false;
		}
		return EditorGUI.Popup(position, _labelContent ?? GUIContent.none, selectedIndex, _displayContents);
	}

	public void Field(SerializedProperty enumProperty, Action<int>? onValueChanged = null, params GUILayoutOption[] options)
	{
		var currentIndex = enumProperty.enumValueIndex;
		var newIndex = Draw(currentIndex, options);
		if (newIndex != currentIndex)
		{
			enumProperty.enumValueIndex = newIndex;
			onValueChanged?.Invoke(newIndex);
		}
	}

	public void Field(Rect position, SerializedProperty enumProperty, Action<int>? onValueChanged = null)
	{
		var currentIndex = enumProperty.enumValueIndex;
		var newIndex = Draw(position, currentIndex);
		if (newIndex != currentIndex)
		{
			enumProperty.enumValueIndex = newIndex;
			onValueChanged?.Invoke(newIndex);
		}
	}

	public void Dispose()
	{
		if (_disposed) return;
		Localization.OnLanguageChanged -= OnLanguageChanged;
		_disposed = true;
	}
}

internal sealed class LocalizedToolbar : IDisposable
{
	private readonly string[] _optionKeys;
    private GUIContent[] _displayContents;

	// PropertyDrawerで使う場合Disposeできない
	// その場合にOnLanguageChangedの不要な更新が積み重なる問題を回避する為に、描画が呼ばれたときに始めて更新されるようにする
	private bool _rebuildRequired;
	private bool _disposed;

	public LocalizedToolbar(IEnumerable<string> optionKeys)
	{
		_optionKeys = optionKeys.ToArray();
		_displayContents = BuildDisplayContents();
		Localization.OnLanguageChanged += OnLanguageChanged;
	}

	/// <summary>
	/// optionKeys: {enumType.Name}:enum:enumValue
	/// </summary>
	public LocalizedToolbar(Type enumType) : this(enumType.GetEnumNames().Select(k => $"{enumType.Name}:enum:{k}"))
	{
	}


	private void OnLanguageChanged()
	{
		if (_disposed) return;
		_rebuildRequired = true;
	}

	private GUIContent[] BuildDisplayContents()
	{
        return _optionKeys.Select(k => k.G()).ToArray();
	}

	public int Draw(int selectedIndex, params GUILayoutOption[] options)
	{
		if (_disposed) return selectedIndex;
		if (_rebuildRequired)
		{
			_displayContents = BuildDisplayContents();
			_rebuildRequired = false;
		}
		return GUILayout.Toolbar(selectedIndex, _displayContents, options);
	}
	
	public void Dispose()
	{
		if (_disposed) return;
		Localization.OnLanguageChanged -= OnLanguageChanged;
		_disposed = true;
	}
}