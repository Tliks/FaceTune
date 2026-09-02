namespace Aoyon.FaceTune.Gui;

internal readonly record struct SectionActionField(
    string PropertyPath,
    Func<object?> CreateDefaultValue)
{
    internal static SectionActionField From(
        SerializedProperty property,
        Func<object?> createDefaultValue)
        => new(property.propertyPath, createDefaultValue);
}

internal sealed class SectionActionSet
{
    public SerializedObject SerializedObject { get; }
    public IReadOnlyList<SectionActionField> Fields { get; }
    public string Key { get; }

    internal SectionActionSet(
        SerializedObject serializedObject,
        IEnumerable<SectionActionField> fields,
        string key = "")
    {
        SerializedObject = serializedObject;
        Fields = fields.ToArray();
        ScopeProperty = Fields.Count == 0
            ? null
            : serializedObject.FindProperty(Fields[0].PropertyPath);
        Key = key;
    }

    public SerializedProperty? ScopeProperty { get; }

    internal SectionActionSet WithKey(string key)
        => new(SerializedObject, Fields, key);
}

internal static class SectionOperations
{
    internal static void Reset(SectionActionSet section)
    {
        if (section.Fields.Count == 0) return;

        RunUndo("section.reset.label".LS(), () =>
        {
            var serializedObject = section.SerializedObject;
            serializedObject.UpdateIfRequiredOrScript();
            ResetValues(section);
            serializedObject.ApplyModifiedProperties();
        });
    }

    internal static void ResetValues(SectionActionSet section)
    {
        foreach (var field in section.Fields)
        {
            var property = section.SerializedObject.FindProperty(field.PropertyPath);
            if (property == null) continue;
            property.CopyFrom(field.CreateDefaultValue());
        }
    }

    internal static bool CanCopy(SectionActionSet section)
        => SectionClipboard.CanCopy(section);

    internal static void Copy(SectionActionSet section)
        => SectionClipboard.Copy(section);

    internal static bool CanPaste(SectionActionSet section)
        => SectionClipboard.CanPaste(section);

    internal static void Paste(SectionActionSet section)
        => RunUndo("section.paste.label".LS(), () => SectionClipboard.Paste(section));

    internal static void RunUndo(string name, Action action)
    {
        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(name);
        try
        {
            action();
        }
        finally
        {
            Undo.CollapseUndoOperations(group);
        }
    }
}

internal static class SectionHeaderMenu
{
    internal static bool ActionsEnabled(ISectionActionProvider provider)
        => provider is not ISectionActionAvailability availability
           || availability.ActionsEnabled;

    internal static GenericMenu Create(
        SectionActionSet actions,
        Action<GenericMenu>? populate = null,
        bool enabled = true)
    {
        var menu = new GenericMenu();
        if (!enabled)
        {
            menu.AddDisabledItem("section.copy.label".LG());
            menu.AddDisabledItem("section.paste.label".LG());
            menu.AddDisabledItem("section.reset.label".LG());
            return menu;
        }

        if (SectionOperations.CanCopy(actions))
            menu.AddItem(
                "section.copy.label".LG(),
                false,
                () => SectionOperations.Copy(actions));
        else
            menu.AddDisabledItem("section.copy.label".LG());

        if (SectionOperations.CanPaste(actions))
            menu.AddItem(
                "section.paste.label".LG(),
                false,
                () => SectionOperations.Paste(actions));
        else
            menu.AddDisabledItem("section.paste.label".LG());

        menu.AddItem(
            "section.reset.label".LG(),
            false,
            () => SectionOperations.Reset(actions));

        if (populate == null) return menu;
        menu.AddSeparator(string.Empty);
        populate(menu);
        return menu;
    }
}

[InitializeOnLoad]
internal static class SectionClipboard
{
    private static SerializedObject? _source;
    private static string? _sectionKey;
    private static string[]? _propertyPaths;

    static SectionClipboard()
    {
        AssemblyReloadEvents.beforeAssemblyReload += Clear;
        EditorApplication.quitting += Clear;
    }

    internal static bool CanCopy(SectionActionSet section)
    {
        if (section.Fields.Count == 0
            || section.SerializedObject.targetObjects.Length != 1
            || section.SerializedObject.targetObject == null) return false;

        return section.Fields.All(field =>
            section.SerializedObject.FindProperty(field.PropertyPath) != null);
    }

    internal static void Copy(SectionActionSet section)
    {
        if (!CanCopy(section)) return;
        if (section.SerializedObject.targetObject is not Object target) return;

        var source = new SerializedObject(target);
        source.Update();

        // Keep this stream untouched after storing it so it represents copy-time data.
        Clear();
        _source = source;
        _sectionKey = section.Key;
        _propertyPaths = section.Fields
            .Select(field => field.PropertyPath)
            .ToArray();
    }

    internal static bool CanPaste(SectionActionSet section)
    {
        if (_source == null
            || _propertyPaths == null
            || _sectionKey != section.Key
            || _propertyPaths.Length != section.Fields.Count
            || section.SerializedObject.targetObject == null) return false;
        if (_source.targetObject == null)
        {
            Clear();
            return false;
        }

        for (var i = 0; i < section.Fields.Count; i++)
        {
            var field = section.Fields[i];
            if (_propertyPaths[i] != field.PropertyPath) return false;

            var sourceProperty = _source.FindProperty(_propertyPaths[i]);
            var targetProperty = section.SerializedObject.FindProperty(field.PropertyPath);
            if (sourceProperty == null
                || targetProperty == null
                || sourceProperty.propertyType != targetProperty.propertyType
                || sourceProperty.type != targetProperty.type
                || sourceProperty.isArray != targetProperty.isArray) return false;
        }

        return true;
    }

    internal static void Paste(SectionActionSet section)
    {
        if (!CanPaste(section)) return;

        var target = section.SerializedObject;
        target.UpdateIfRequiredOrScript();
        if (!CanPaste(section)) return;

        foreach (var field in section.Fields)
        {
            var sourceProperty = _source!.FindProperty(field.PropertyPath);
            if (sourceProperty != null)
                target.CopyFromSerializedProperty(sourceProperty);
        }
        target.ApplyModifiedProperties();
    }

    private static void Clear()
    {
        var source = _source;
        _source = null;
        _sectionKey = null;
        _propertyPaths = null;
        source?.Dispose();
    }
}
