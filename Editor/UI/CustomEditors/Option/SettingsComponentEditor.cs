using nadena.dev.ndmf.runtime;

namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(AvatarSettingsComponent))]
internal sealed class SettingsComponentEditor : FaceTuneSectionEditorBase<AvatarSettingsComponent>
{
    protected override IReadOnlyList<FaceTuneSection> CreateSections()
        => new[]
        {
            CreateSection("settings.section.label", new SettingsSectionDrawer(serializedObject), defaultExpanded: false),
            CreateSection("avatarSettings.advancedSettings.label", new AdvancedSettingsSectionDrawer(serializedObject), defaultExpanded: false)
        };
}

internal sealed class SettingsSectionDrawer : ISectionDrawer
{
    private static readonly ReorderableListOptions ExcludedBlendShapeListOptions = new(
        Header: ReorderableListOptions.HeaderMode.Label,
        NestContent: false);

    private readonly SerializedObject _serializedObject;
    private readonly SerializedProperty _faceObject;
    private readonly SerializedProperty _excludedBlendShapes;

    public SettingsSectionDrawer(SerializedObject serializedObject)
    {
        _serializedObject = serializedObject;
        var settings = serializedObject.FindProperty(nameof(AvatarSettingsComponent.Settings));
        _faceObject = settings.FindPropertyRelative(nameof(AvatarSettings.FaceObjectReference));
        _excludedBlendShapes = settings.FindPropertyRelative(nameof(AvatarSettings.ExcludedBlendShapeNames));
    }

    public float GetHeight()
    {
        var height = GUIHelper.LineHeight;
        if (HasMeshObject)
            height += GUIHelper.VerticalSpacing + GUIHelper.PropertyHeight(_faceObject);
        height += GUIHelper.VerticalSpacing + GUIHelper.LineHeight;
        if (_excludedBlendShapes.arraySize > 0)
            height += GUIHelper.VerticalSpacing + GUIHelper.GetListHeight(_excludedBlendShapes, ExcludedBlendShapeListOptions);
        return height;
    }

    public void Draw(Rect position)
    {
        var isAutomatic = !HasMeshObject;
        var faceMode = GUIHelper.LocalizedPopup(
            position,
            isAutomatic ? 0 : 1,
            "avatarSettings.faceMesh.label",
            new[] { "avatarSettings.faceMesh.option.auto", "avatarSettings.faceMesh.option.manual" });
        if (faceMode != (isAutomatic ? 0 : 1))
        {
            if (faceMode == 0)
                _faceObject.CopyFrom(new AvatarObjectReference());
            else
                AssignDetectedFaceObject();
        }
        var hasMeshObject = HasMeshObject;
        if (hasMeshObject)
        {
            position.NewLine();
            GUIHelper.DrawPropertyWithIndentedLabel(ref position, _faceObject, "avatarSettings.meshObject.label");
        }
        else
        {
            position.NewLine();
        }

        var excludeSome = _excludedBlendShapes.arraySize > 0;
        var blendShapeMode = GUIHelper.LocalizedPopup(
            position,
            excludeSome ? 1 : 0,
            "avatarSettings.blendShapes.label",
            new[] { "avatarSettings.blendShapes.option.all", "avatarSettings.blendShapes.option.excludeSome" });
        if (blendShapeMode != (excludeSome ? 1 : 0))
            _excludedBlendShapes.arraySize = blendShapeMode == 0 ? 0 : 1;

        if (_excludedBlendShapes.arraySize == 0) return;
        position.NewLine();
        position.height = GUIHelper.GetListHeight(_excludedBlendShapes, ExcludedBlendShapeListOptions);
        position.Indent();
        GUIHelper.DrawList(position, _excludedBlendShapes, "avatarSettings.excludedBlendShapes.label".LG(), ExcludedBlendShapeListOptions);
    }

    private bool HasMeshObject => !AvatarObjectReference.IsNull(_faceObject);

    private void AssignDetectedFaceObject()
    {
        if (_serializedObject.targetObject is not Component component) return;

        var renderer = AvatarContext.TryGet(component.gameObject, out var context, out _ )
            ? context.FaceRenderer
            : RuntimeUtil.FindAvatarInParents(component.transform)
                ?.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .FirstOrDefault();
        if (renderer != null)
            _faceObject.CopyFrom(new AvatarObjectReference(renderer.gameObject));
    }
}

internal sealed class AdvancedSettingsSectionDrawer : ISectionDrawer
{
    private readonly SerializedProperty _avoidEyeBlinkConflicts;
    private readonly SerializedProperty _avoidLipSyncConflicts;

    public AdvancedSettingsSectionDrawer(SerializedObject serializedObject)
    {
        var settings = serializedObject.FindProperty(nameof(AvatarSettingsComponent.Settings));
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
}
