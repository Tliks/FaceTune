namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(AvatarSettingsComponent))]
internal sealed class AvatarSettingsComponentEditor : FaceTuneSectionEditorBase<AvatarSettingsComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[] { CreateAvatarSection(), CreateAdvancedSection() };

    private FaceTuneSection CreateAvatarSection()
        => CreateSection("settings.section.label", new AvatarSettingsSectionDrawer(serializedObject), false);

    private FaceTuneSection CreateAdvancedSection()
        => CreateSection("avatarSettings.advancedSettings.label", new AvatarAdvancedSettingsSectionDrawer(serializedObject), false);
}

internal sealed class AvatarSettingsSectionDrawer : ISectionDrawer
{
    private const float WarningHeight = 30f;
    private static readonly ReorderableListOptions ExcludedBlendShapesOptions = new(Header: ReorderableListOptions.HeaderMode.None, Controls: ReorderableListOptions.ControlsPlacement.Header, NestContent: false);
    private readonly SerializedProperty _faceObject;
    private readonly SerializedProperty _excludedBlendShapes;

    public AvatarSettingsSectionDrawer(SerializedObject serializedObject)
    {
        _faceObject = serializedObject.FindProperty(nameof(AvatarSettingsComponent.FaceObjectReference));
        _excludedBlendShapes = serializedObject.FindProperty(nameof(AvatarSettingsComponent.ExcludedBlendShapeNames));
    }

    public float GetHeight()
    {
        var height = GUIHelper.LineHeight;
        if (IsManualFaceSelection)
        {
            height += GUIHelper.VerticalSpacing + EditorGUI.GetPropertyHeight(_faceObject, GUIContent.none, true);
            if (AvatarObjectReference.IsNull(_faceObject)) height += GUIHelper.VerticalSpacing + WarningHeight;
        }
        height += GUIHelper.VerticalSpacing + GUIHelper.LineHeight;
        if (UsesExclusionList)
            height += GUIHelper.VerticalSpacing + GUIHelper.GetListHeight(_excludedBlendShapes, ExcludedBlendShapesOptions);
        return height;
    }

    public void Draw(Rect position)
    {
        position.SetSingleHeight();
        var faceMode = IsManualFaceSelection ? 1 : 0;
        var nextFaceMode = GUIHelper.LocalizedPopup(position, faceMode, "avatarSettings.faceMesh.label", new[] { "avatarSettings.faceMesh.option.auto", "avatarSettings.faceMesh.option.manual" });
        if (nextFaceMode != faceMode && nextFaceMode == 0) ClearAvatarObjectReference(_faceObject);
        position.NewLine();
        if (nextFaceMode == 1)
        {
            GUIHelper.DrawPropertyWithIndentedLabel(ref position, _faceObject, "avatarSettings.meshObject.label");
            if (AvatarObjectReference.IsNull(_faceObject))
            {
                position.height = WarningHeight;
                var warning = position;
                warning.Indent();
                EditorGUI.HelpBox(warning, "avatarSettings.faceMesh.empty.message".LS(), MessageType.Warning);
                position.NewLine();
            }
        }

        position.SetSingleHeight();
        var blendShapeMode = UsesExclusionList ? 1 : 0;
        var nextBlendShapeMode = GUIHelper.LocalizedPopup(position, blendShapeMode, "avatarSettings.blendShapes.label", new[] { "avatarSettings.blendShapes.option.all", "avatarSettings.blendShapes.option.excludeSome" });
        if (nextBlendShapeMode != blendShapeMode)
        {
            if (nextBlendShapeMode == 0) _excludedBlendShapes.ClearArray();
            else
            {
                _excludedBlendShapes.InsertArrayElementAtIndex(0);
                _excludedBlendShapes.GetArrayElementAtIndex(0).stringValue = string.Empty;
            }
        }
        if (nextBlendShapeMode == 0) return;
        position.NewLine();
        position.Indent();
        position.height = GUIHelper.GetListHeight(_excludedBlendShapes, ExcludedBlendShapesOptions);
        GUIHelper.DrawList(position, _excludedBlendShapes, "avatarSettings.excludedBlendShapes.label".LG(), ExcludedBlendShapesOptions);
    }

    private bool IsManualFaceSelection => _faceObject.hasMultipleDifferentValues || !AvatarObjectReference.IsNull(_faceObject);
    private bool UsesExclusionList => _excludedBlendShapes.hasMultipleDifferentValues || _excludedBlendShapes.arraySize != 0;

    private static void ClearAvatarObjectReference(SerializedProperty property)
    {
        property.FindPropertyRelative("referencePath").stringValue = string.Empty;
        property.FindPropertyRelative("targetObject").objectReferenceValue = null;
    }
}

internal sealed class AvatarAdvancedSettingsSectionDrawer : ISectionDrawer
{
    private readonly SerializedProperty _eyeBlink;
    private readonly SerializedProperty _lipSync;

    public AvatarAdvancedSettingsSectionDrawer(SerializedObject serializedObject)
    {
        _eyeBlink = serializedObject.FindProperty(nameof(AvatarSettingsComponent.AvoidEyeBlinkConflicts));
        _lipSync = serializedObject.FindProperty(nameof(AvatarSettingsComponent.AvoidLipSyncConflicts));
    }

    public float GetHeight() => GUIHelper.GetLinesHeight(2);

    public void Draw(Rect position)
    {
        GUIHelper.DrawToggleLeft(position, _eyeBlink, "avatarSettings.avoidEyeBlinkConflicts.label".LG());
        position.NewLine();
        GUIHelper.DrawToggleLeft(position, _lipSync, "avatarSettings.avoidLipSyncConflicts.label".LG());
    }
}
