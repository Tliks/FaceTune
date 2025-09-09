using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui;

internal abstract class FaceTuneCustomEditorBase<T> : Editor where T : FaceTuneTagComponent
{
    public T Component = null!;
    public string ComponentName => typeof(T).Name;

    public virtual void OnEnable()
    {
        Component = (target as T)!;
    }

    public virtual void OnDisable()
    {
    }
}

internal abstract class FaceTuneIMGUIEditorBase<T> : FaceTuneCustomEditorBase<T> where T : FaceTuneTagComponent
{
    public override void OnInspectorGUI()
    {
        Localization.DrawLanguageSwitcher();
        EditorGUILayout.Space();
        serializedObject.Update();
        OnInnerInspectorGUI();
        serializedObject.ApplyModifiedProperties();
    }

    protected abstract void OnInnerInspectorGUI();

    protected void DrawDefaultInspector(bool localized)
    {
        var iterator = serializedObject.GetIterator();
        iterator.NextVisible(true);
        while (iterator.NextVisible(false))
        {
            if (localized)
            {
                LocalizedPropertyField(iterator);
            }
            else
            {
                EditorGUILayout.PropertyField(iterator);
            }
        }
    }

    /// <summary>
    /// keyは省略された場合$"{コンポーネント名}:prop:{プロパティ名}"を使用
    /// </summary>
    protected void LocalizedPropertyField(SerializedProperty property, string? key = null, bool includeChildren = true)
    {
        key ??= $"{typeof(T).Name}:prop:{property.name}";
        LocalizedUI.PropertyField(property, key, includeChildren);
    }
}

internal abstract class FaceTuneUElementEditorBase<T> : FaceTuneCustomEditorBase<T> where T : FaceTuneTagComponent
{
    private VisualElement? _visualElement;

    public sealed override VisualElement CreateInspectorGUI()
    {
        if (_visualElement == null)
        {
            _visualElement = new VisualElement();
            Localization.OnLanguageChanged += RebuildUI;
        }
        else
        {
            _visualElement.Clear();
        }

        _visualElement.Add(Localization.CreateLanguageSwitcher());

        _visualElement.Add(new VisualElement { style = { height = 8 } });

        var inner = CreateInnerInspectorGUI();
        _visualElement.Add(inner);

        return _visualElement;
    }

    protected abstract VisualElement CreateInnerInspectorGUI();

    private void RebuildUI()
    {
        CreateInspectorGUI();
    }

    protected virtual void OnDestroy()
    {
        Localization.OnLanguageChanged -= RebuildUI;
    }
}