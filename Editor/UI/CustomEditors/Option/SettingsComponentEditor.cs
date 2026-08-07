namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(SettingsComponent))]
internal sealed class SettingsComponentEditor : FaceTuneSectionEditorBase<SettingsComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[]
        {
            CreateSection(
                "settings.section.label",
                new SettingsSectionDrawer(serializedObject),
                defaultExpanded: false),
            CreateSection(
                "avatarSettings.advancedSettings.label",
                new AdvancedSettingsSectionDrawer(serializedObject),
                defaultExpanded: false)
        };
}

internal sealed class SettingsSectionDrawer : ISectionDrawer
{
    private const float MissingFaceMeshWarningHeight = 30f;

    private static readonly ReorderableListOptions ExcludedBlendShapeListOptions = new(
        Header: ReorderableListOptions.HeaderMode.Label);

    private readonly SerializedProperty _faceMeshSelection;
    private readonly SerializedProperty _faceObject;
    private readonly SerializedProperty _excludedBlendShapes;

    public SettingsSectionDrawer(SerializedObject serializedObject)
    {
        var settings = serializedObject.FindProperty(nameof(SettingsComponent.Settings));
        _faceMeshSelection = settings.FindPropertyRelative(nameof(AvatarSettings.FaceMeshSelection));
        _faceObject = settings.FindPropertyRelative(nameof(AvatarSettings.FaceObjectReference));
        _excludedBlendShapes = settings.FindPropertyRelative(nameof(AvatarSettings.ExcludedBlendShapeNames));
    }

    public float GetHeight()
        => GUIHelper.LineHeight
         + GUIHelper.VerticalSpacing
         + (IsManualFaceMeshSelection ? GUIHelper.PropertyHeight(_faceObject) : 0f)
         + (HasMissingManualFaceMesh ? MissingFaceMeshWarningHeight + GUIHelper.VerticalSpacing : 0f)
         + GUIHelper.GetListHeight(_excludedBlendShapes, ExcludedBlendShapeListOptions);

    public void Draw(Rect position)
    {
        position.height = GUIHelper.LineHeight;
        var faceSelection = GUIHelper.LocalizedPopup(
            position,
            _faceMeshSelection.enumValueIndex,
            "avatarSettings.faceSelection.label",
            new[] { "avatarSettings.faceSelection.option.auto", "avatarSettings.faceSelection.option.manual" });
        if (faceSelection != _faceMeshSelection.enumValueIndex)
        {
            _faceMeshSelection.enumValueIndex = faceSelection;
            if (faceSelection == (int)FaceMeshSelectionMode.Automatic)
                _faceObject.CopyFrom(AvatarSettings.CreateDefaultFaceObjectReference());
        }
        position.NewLine();

        if (IsManualFaceMeshSelection)
            GUIHelper.DrawPropertyWithIndentedLabel(ref position, _faceObject, "avatarSettings.faceMesh.label");
        if (HasMissingManualFaceMesh)
        {
            position.height = MissingFaceMeshWarningHeight;
            var warningPosition = position;
            warningPosition.Indent();
            EditorGUI.HelpBox(warningPosition, "avatarSettings.faceMesh.empty.message".LS(), MessageType.Warning);
            position.NewLine();
        }

        position.height = GUIHelper.GetListHeight(_excludedBlendShapes, ExcludedBlendShapeListOptions);
        GUIHelper.DrawList(
            position,
            _excludedBlendShapes,
            "avatarSettings.excludedBlendShapes.label".LG(),
            ExcludedBlendShapeListOptions);
    }

    public void Reset()
    {
        _faceMeshSelection.CopyFrom(AvatarSettings.DefaultFaceMeshSelection);
        _faceObject.CopyFrom(AvatarSettings.CreateDefaultFaceObjectReference());
        _excludedBlendShapes.CopyFrom(AvatarSettings.CreateDefaultExcludedBlendShapeNames());
    }

    private bool IsManualFaceMeshSelection
        => _faceMeshSelection.hasMultipleDifferentValues
            || _faceMeshSelection.enumValueIndex == (int)FaceMeshSelectionMode.Manual;

    private bool HasMissingManualFaceMesh
        => IsManualFaceMeshSelection && AvatarObjectReference.IsEmpty(_faceObject);
}

internal sealed class AdvancedSettingsSectionDrawer : ISectionDrawer
{
    private readonly SerializedProperty _avoidEyeBlinkConflicts;
    private readonly SerializedProperty _avoidLipSyncConflicts;

    public AdvancedSettingsSectionDrawer(SerializedObject serializedObject)
    {
        var settings = serializedObject.FindProperty(nameof(SettingsComponent.Settings));
        _avoidEyeBlinkConflicts = settings.FindPropertyRelative(nameof(AvatarSettings.AvoidEyeBlinkConflicts));
        _avoidLipSyncConflicts = settings.FindPropertyRelative(nameof(AvatarSettings.AvoidLipSyncConflicts));
    }

    public float GetHeight() => GUIHelper.GetLinesHeight(2);

    public void Draw(Rect position)
    {
        GUIHelper.DrawToggleLeft(position, _avoidEyeBlinkConflicts, "avatarSettings.avoidEyeBlinkConflicts.label".LG());
        position.NewLine();
        GUIHelper.DrawToggleLeft(position, _avoidLipSyncConflicts, "avatarSettings.avoidLipSyncConflicts.label".LG());
    }

    public void Reset()
    {
        _avoidEyeBlinkConflicts.CopyFrom(AvatarSettings.DefaultAvoidEyeBlinkConflicts);
        _avoidLipSyncConflicts.CopyFrom(AvatarSettings.DefaultAvoidLipSyncConflicts);
    }
}
