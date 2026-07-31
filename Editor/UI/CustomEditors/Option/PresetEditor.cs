namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(PresetComponent))]
internal sealed class PresetEditor : FaceTuneSectionEditor<PresetComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[]
        {
            CreatePresetSection(),
            CreateMenuSettingsSection()
        };

    private FaceTuneSection CreatePresetSection()
        => new(
            "preset.section.label".LG(),
            () => GetPropertyHeight(nameof(PresetComponent.DefaultSelected)),
            DrawPresetContent,
            true);

    private FaceTuneSection CreateMenuSettingsSection()
        => new(
            "menuSettings.section.label".LG(),
            () => GetPropertyHeight(nameof(PresetComponent.MenuName))
                + GUIHelper.VerticalSpacing
                + GetPropertyHeight(nameof(PresetComponent.Icon))
                + GUIHelper.VerticalSpacing
                + GetPropertyHeight(nameof(PresetComponent.InstallSettings)),
            DrawMenuSettingsContent,
            false);

    private void DrawPresetContent(Rect position)
    {
        var property = serializedObject.FindProperty(nameof(PresetComponent.DefaultSelected));
        GUIHelper.DrawProperty(ref position, property, "menu.defaultSelected.label");
    }

    private void DrawMenuSettingsContent(Rect position)
    {
        var menuName = serializedObject.FindProperty(nameof(PresetComponent.MenuName));
        MenuGUI.DrawMenuName(ref position, menuName, Component, "menu.name.label".LG());
        GUIHelper.DrawProperty(ref position, serializedObject.FindProperty(nameof(PresetComponent.Icon)), "menu.icon.label");
        GUIHelper.DrawProperty(ref position, serializedObject.FindProperty(nameof(PresetComponent.InstallSettings)), "menu.destination.label");
    }
}
