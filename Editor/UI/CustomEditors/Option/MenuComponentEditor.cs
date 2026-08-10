namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(MenuComponent))]
internal sealed class MenuComponentEditor : FaceTuneSectionEditorBase<MenuComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateSection("menu.section.label", new MenuSectionDrawer(serializedObject), defaultExpanded: false) };
}

internal sealed class MenuSectionDrawer : ISectionDrawer
{
    private readonly PropertiesSectionDrawer _menuSettings;
    private readonly SerializedProperty _kind;
    private readonly SerializedProperty _binding;
    private readonly SerializedProperty _name;
    private readonly SerializedProperty _initialValue;
    private readonly SerializedProperty _selectedValue;

    public MenuSectionDrawer(SerializedObject serializedObject)
    {
        _menuSettings = new PropertiesSectionDrawer(new PropertiesSectionDrawer.Entry(
            serializedObject.FindProperty(nameof(MenuComponent.Menu))));
        _kind = serializedObject.FindProperty(nameof(MenuComponent.MenuKind));
        _binding = serializedObject.FindProperty(nameof(MenuComponent.Binding));
        _name = serializedObject.FindProperty(nameof(MenuComponent.Name));
        _initialValue = serializedObject.FindProperty(nameof(MenuComponent.InitialValue));
        _selectedValue = serializedObject.FindProperty(nameof(MenuComponent.SelectedValue));
    }

    public float GetHeight()
    {
        var height = _menuSettings.GetHeight() + GUIHelper.PropertyHeight(_kind);
        if (IsFolder()) return height;
        height += GUIHelper.PropertyHeight(_binding);
        height += GUIHelper.PropertyHeight(_name);
        if (!IsExisting()) height += GUIHelper.PropertyHeight(_initialValue);
        if (IsToggle() && !IsGroup()) height += GUIHelper.PropertyHeight(_selectedValue);
        return height;
    }

    public void Draw(Rect position)
    {
        _menuSettings.Draw(position);
        position.y += _menuSettings.GetHeight();
        GUIHelper.DrawProperty(ref position, _kind, "menu.mode.label");
        if (IsFolder()) return;

        GUIHelper.DrawProperty(ref position, _binding, "menu.binding.label");
        var nameLabel = IsGroup() ? "Group Name" : "Parameter Name";
        EditorGUI.PropertyField(position, _name, new GUIContent(nameLabel));
        position.NewLine();
        if (!IsExisting()) GUIHelper.DrawProperty(ref position, _initialValue, "Initial Value");
        if (IsToggle() && !IsGroup()) GUIHelper.DrawProperty(ref position, _selectedValue, "Selected Value");
    }

    private bool IsFolder() => !_kind.hasMultipleDifferentValues && _kind.enumValueIndex == (int)MenuComponent.Kind.Folder;
    private bool IsToggle() => _kind.hasMultipleDifferentValues || _kind.enumValueIndex == (int)MenuComponent.Kind.Toggle;
    private bool IsGroup() => !_binding.hasMultipleDifferentValues && _binding.enumValueIndex == (int)MenuComponent.ParameterBinding.GenerateGroup;
    private bool IsExisting() => !_binding.hasMultipleDifferentValues && _binding.enumValueIndex == (int)MenuComponent.ParameterBinding.Existing;
}
