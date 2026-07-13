namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(PresetComponent))]
internal sealed class PresetEditor : FaceTuneEditor<PresetComponent>
{
    private bool _presetExpanded = true;
    private bool _menuSettingsExpanded;

    protected override float GetInspectorHeight()
    {
        var presetContentHeight = GetPropertyHeight(nameof(PresetComponent.DefaultSelected))
                                - GUIHelper.VerticalSpacing;
        var menuContentHeight = GetPropertyHeight(nameof(PresetComponent.MenuName))
                              + GetPropertyHeight(nameof(PresetComponent.Icon))
                              + GetPropertyHeight(nameof(PresetComponent.InstallSettings))
                              - GUIHelper.VerticalSpacing;
        return GUIHelper.GetShurikenSectionHeight(_presetExpanded, presetContentHeight)
             + GUIHelper.HeaderSpacing
             + GUIHelper.GetShurikenSectionHeight(_menuSettingsExpanded, menuContentHeight);
    }

    protected override void DrawInspector(Rect position)
    {
        var presetContentHeight = GetPropertyHeight(nameof(PresetComponent.DefaultSelected))
                                - GUIHelper.VerticalSpacing;
        var presetHeight = GUIHelper.GetShurikenSectionHeight(_presetExpanded, presetContentHeight);
        var presetRect = new Rect(position.x, position.y, position.width, presetHeight);
        if (GUIHelper.DrawShurikenSection(
                presetRect,
                ref _presetExpanded,
                "preset.section.label".LG(),
                presetContentHeight,
                out var presetContent))
        {
            GUIHelper.DrawProperty(
                ref presetContent,
                serializedObject.FindProperty(nameof(PresetComponent.DefaultSelected)),
                "menu.defaultSelected.label");
        }

        var menuContentHeight = GetPropertyHeight(nameof(PresetComponent.MenuName))
                              + GetPropertyHeight(nameof(PresetComponent.Icon))
                              + GetPropertyHeight(nameof(PresetComponent.InstallSettings))
                              - GUIHelper.VerticalSpacing;
        var menuRect = new Rect(
            position.x,
            presetRect.yMax + GUIHelper.HeaderSpacing,
            position.width,
            GUIHelper.GetShurikenSectionHeight(_menuSettingsExpanded, menuContentHeight));
        if (!GUIHelper.DrawShurikenSection(
                menuRect,
                ref _menuSettingsExpanded,
                "menuSettings.section.label".LG(),
                menuContentHeight,
                out var menuContent)) return;

        var menuName = serializedObject.FindProperty(nameof(PresetComponent.MenuName));
        MenuGUI.DrawMenuName(ref menuContent, menuName, Component, "menu.name.label".LG());
        GUIHelper.DrawProperty(ref menuContent, serializedObject.FindProperty(nameof(PresetComponent.Icon)), "menu.icon.label");
        GUIHelper.DrawProperty(ref menuContent, serializedObject.FindProperty(nameof(PresetComponent.InstallSettings)), "menu.destination.label");
    }
}
