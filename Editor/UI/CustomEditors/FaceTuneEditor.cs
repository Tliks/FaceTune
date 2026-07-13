namespace Aoyon.FaceTune.Gui;

internal abstract class FaceTuneSectionEditor<T> : FaceTuneEditor<T> where T : FaceTuneTagComponent
{
    private bool _expanded;
    private bool _expandedInitialized;

    protected virtual bool DefaultExpanded => true;
    protected abstract GUIContent SectionLabel { get; }
    protected abstract float GetSectionContentHeight();
    protected abstract void DrawSectionContent(Rect position);

    protected sealed override float GetInspectorHeight()
    {
        EnsureExpandedInitialized();
        return GUIHelper.GetShurikenSectionHeight(_expanded, GetSectionContentHeight());
    }

    protected sealed override void DrawInspector(Rect position)
    {
        EnsureExpandedInitialized();
        var contentHeight = GetSectionContentHeight();
        if (GUIHelper.DrawShurikenSection(
                position,
                ref _expanded,
                SectionLabel,
                contentHeight,
                out var content))
            DrawSectionContent(content);
    }

    private void EnsureExpandedInitialized()
    {
        if (_expandedInitialized) return;
        _expanded = DefaultExpanded;
        _expandedInitialized = true;
    }
}

internal abstract class FaceTuneEditor<T> : Editor where T : FaceTuneTagComponent
{
    protected T Component => (T)target;
    protected virtual bool ShowLanguageSwitcher => false;

    public sealed override void OnInspectorGUI()
    {
        if (ShowLanguageSwitcher)
        {
            Localization.DrawLanguageSwitcher();
            EditorGUILayout.Space();
        }
        serializedObject.UpdateIfRequiredOrScript();

        var height = GetInspectorHeight();
        var position = EditorGUILayout.GetControlRect(false, height, GUIStyle.none);
        DrawInspector(position);

        serializedObject.ApplyModifiedProperties();
    }

    protected virtual float GetInspectorHeight()
    {
        var height = 0f;
        var iterator = serializedObject.GetIterator();
        var enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.propertyPath == "m_Script") continue;
            height += GUIHelper.PropertyHeight(iterator);
        }
        return Mathf.Max(0f, height - GUIHelper.VerticalSpacing);
    }

    protected virtual void DrawInspector(Rect position)
    {
        var iterator = serializedObject.GetIterator();
        var enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.propertyPath == "m_Script") continue;
            var property = iterator.Copy();
            position.height = EditorGUI.GetPropertyHeight(property, true);
            EditorGUI.PropertyField(position, property, true);
            position.NewLine();
        }
    }

    protected float GetPropertyHeight(string propertyName, bool includeChildren = true)
    {
        var property = serializedObject.FindProperty(propertyName);
        return property == null ? 0f : GUIHelper.PropertyHeight(property, includeChildren);
    }

    protected void DrawProperty(ref Rect position, string propertyName, bool includeChildren = true)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property == null) return;

        position.height = EditorGUI.GetPropertyHeight(property, includeChildren);
        EditorGUI.PropertyField(position, property, includeChildren);
        position.NewLine();
    }

    protected static bool IsMode(SerializedProperty mode, int value)
        => !mode.hasMultipleDifferentValues && mode.enumValueIndex == value;
}
