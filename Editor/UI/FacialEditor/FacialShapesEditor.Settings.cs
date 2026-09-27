using Aoyon.FaceTune.Platforms;
using Aoyon.FaceTune.Preview;
using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal partial class FacialShapesEditor
{
    public static FacialShapesEditor? TryOpenSettingsEditor(
        SerializedProperty settings,
        ShapesEditorMode mode,
        int activeListIndex = 0)
    {
        using var sample = new Utils.ProfilingSampleScope("ShapesEditor.OpenSettings");
        if (settings.serializedObject.targetObjects.Length != 1
            || settings.serializedObject.targetObject is not FaceTuneTagComponent component
            || !AvatarContext.TryGet(component.gameObject, out var avatar, out _)
            || TryOpenEditor() is not FacialShapesEditor window)
            return null;

        var lists = GetShapeLists(settings, mode);
        var editableLists = mode == ShapesEditorMode.LipSync ? lists.Take(1) : lists;
        var initial = editableLists
            .Select((list, index) => (IReadOnlyList<BlendShapeWeightAnimation>)
                ShapeListSerialization.Read(list, mode != ShapesEditorMode.LipSync && index == 2))
            .ToArray();
        var lipSync = mode == ShapesEditorMode.LipSync
            ? ((ISettingProvider<LipSyncSettings>)component).Setting.Value.Clone()
            : null;
        var eyeBlink = mode is ShapesEditorMode.EyeBlinkSimple or ShapesEditorMode.EyeBlinkCustom
            ? ((ISettingProvider<EyeBlinkSettings>)component).Setting.Value.Clone()
            : null;
        var builtInEyeBlink = eyeBlink != null
            ? MetaversePlatformSupport.GetForAvatar(avatar.Root.transform)
                .Select(support => support.GetBuiltInEyeBlinkAnimations(avatar.FaceRenderer))
                .FirstOrDefault(value => value != null)
            : null;
        var builtInLipSync = mode == ShapesEditorMode.LipSync
            ? MetaversePlatformSupport.GetForAvatar(avatar.Root.transform)
                .Select(support => support.GetBuiltInLipSyncShapes(avatar.FaceRenderer))
                .FirstOrDefault(value => value != null)
            : null;

        var unavailable = GetUnavailableNames(avatar, mode, lists.Length);
        var facial = SelectedPreviewResolver.ResolveFacial(
            component,
            avatar,
            ComputeContext.NullContext);
        var ignoredNames = AvatarContext.GetExplicitlyExcludedBlendShapeNames(avatar.Root);

        window.StartSettingsContext(
            avatar.FaceRenderer,
            component,
            settings.propertyPath,
            mode,
            initial,
            unavailable,
            facial?.Animations,
            facial?.DefaultWeight,
            ignoredNames,
            activeListIndex,
            lipSync,
            builtInLipSync,
            eyeBlink,
            builtInEyeBlink);
        return window;
    }

    private void StartSettingsContext(
        SkinnedMeshRenderer renderer,
        Component target,
        string settingsPropertyPath,
        ShapesEditorMode mode,
        IReadOnlyList<BlendShapeWeightAnimation>[] initialLists,
        ISet<string>[] unavailableNames,
        IReadOnlyList<BlendShapeWeightAnimation>? background,
        float? backgroundDefaultValue,
        ImmutableHashSet<string> ignoredNames,
        int activeListIndex,
        LipSyncSettings? lipSync,
        VrcVisemeLipSyncShapes? builtInLipSync,
        EyeBlinkSettings? eyeBlink,
        IReadOnlyList<BlendShapeWeightAnimation>? builtInEyeBlink)
    {
        using var sample = new Utils.ProfilingSampleScope("ShapesEditor.StartSettingsContext");
        EndContext();
        var managerCount = mode == ShapesEditorMode.LipSync ? 1 : initialLists.Length;
        activeListIndex = mode == ShapesEditorMode.EyeBlinkCustom ? 2 : 0;
        _lipSyncDraft = lipSync ?? new LipSyncSettings();
        _eyeBlinkDraft = eyeBlink ?? new EyeBlinkSettings();

        _dataManagers = new BlendShapeOverrideManager[managerCount];
        var serializedObject = new SerializedObject(this);
        serializedObject.Update();
        var managerProperties = serializedObject.FindProperty(nameof(_dataManagers));
        var lipSyncEditing = mode == ShapesEditorMode.LipSync
            ? new LipSyncEditing(
                serializedObject,
                _lipSyncDraft,
                builtInLipSync,
                unavailableNames[1])
            : null;
        for (var index = 0; index < managerCount; index++)
        {
            var manager = new BlendShapeOverrideManager(
                serializedObject,
                managerProperties.GetArrayElementAtIndex(index),
                mode != ShapesEditorMode.LipSync && index == 2);
            manager.OnAnyDataChange += SyncUnsavedChangesFromData;
            _dataManagers[index] = manager;
        }

        void InitializeList(int index)
        {
            var manager = _dataManagers[index];
            if (manager.IsInitialized) return;
            var initial = initialLists[index];
            manager.SetInitialState(
                renderer,
                null,
                null,
                ToFirstFrameSet(initial),
                unavailableNames[index],
                mode != ShapesEditorMode.LipSync && index == 2 ? GetInitialCurves(initial) : null);
        }

        InitializeList(activeListIndex);
        if (mode == ShapesEditorMode.EyeBlinkSimple)
            InitializeList(1);

        ShapesEditorModeSession modeSession = mode switch
        {
            ShapesEditorMode.EyeBlinkSimple or ShapesEditorMode.EyeBlinkCustom =>
                new EyeBlinkModeSession(
                    _dataManagers, _eyeBlinkDraft, builtInEyeBlink, serializedObject, InitializeList),
            ShapesEditorMode.LipSync => new LipSyncModeSession(
                _dataManagers[0],
                lipSyncEditing ?? throw new InvalidOperationException()),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
        _context = new FacialShapesEditorContext(
            serializedObject,
            _dataManagers,
            modeSession,
            rootVisualElement,
            renderer,
            target,
            settingsPropertyPath,
            background,
            backgroundDefaultValue,
            ignoredNames,
            _dataManagers.Length,
            InitializeList,
            TryChangeRenderer,
            SaveChanges);
        _context.ModeSession.Changed += SyncUnsavedChangesFromData;
        _context.SetActiveList(activeListIndex);
        titleContent = (mode == ShapesEditorMode.LipSync
            ? "lipSync.section.label"
            : "eyeBlink.section.label").LG();

        _unsavedStateSyncPending = false;
        hasUnsavedChanges = false;
        Undo.SetCurrentGroupName($"Facial Shapes Editor: StartContext: {renderer.name}");
    }

    private static void SaveSettings(FacialShapesEditorContext context)
    {
        if (context.Target is not Component component
            || context.AnimationPropertyPath == null)
            throw new Exception("Settings target is invalid");

        using var serialized = new SerializedObject(component);
        serialized.Update();
        var settings = serialized.FindProperty(context.AnimationPropertyPath);
        context.ModeSession.SaveSettings(settings);
        serialized.ApplyModifiedProperties();
    }

    private static SerializedProperty[] GetShapeLists(
        SerializedProperty property,
        ShapesEditorMode mode)
        => mode switch
        {
            ShapesEditorMode.EyeBlinkSimple or ShapesEditorMode.EyeBlinkCustom => new[]
            {
                property.FindPropertyRelative(nameof(EyeBlinkSettings.SimpleBlinkBlendShapes)),
                property.FindPropertyRelative(nameof(EyeBlinkSettings.SimpleConflictPreventionBlendShapes)),
                property.FindPropertyRelative(nameof(EyeBlinkSettings.Animations))
            },
            ShapesEditorMode.LipSync => GetLipSyncLists(property),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

    private static SerializedProperty[] GetLipSyncLists(SerializedProperty property)
    {
        var result = new SerializedProperty[VrcVisemeLipSyncShapes.Count + 1];
        result[0] = property.FindPropertyRelative(nameof(LipSyncSettings.CancellerBlendShapes));
        var shapes = property.FindPropertyRelative(nameof(LipSyncSettings.Shapes));
        for (var index = 0; index < VrcVisemeLipSyncShapes.Count; index++)
            result[index + 1] = shapes.FindPropertyRelative(
                VrcVisemeLipSyncShapes.PropertyNames[index]);
        return result;
    }

    private static ISet<string>[] GetUnavailableNames(
        AvatarContext avatar,
        ShapesEditorMode mode,
        int count)
    {
        var animationKind = mode == ShapesEditorMode.LipSync
            ? FaceTuneWriteKind.LipSyncAnimation
            : FaceTuneWriteKind.EyeBlinkAnimation;
        var animation = AvatarContext.GetUnavailableBlendShapeNames(
            avatar.Root,
            animationKind);
        if (count == 1) return new[] { animation };

        var facial = AvatarContext.GetUnavailableBlendShapeNames(
            avatar.Root,
            FaceTuneWriteKind.FacialData);
        var result = Enumerable.Repeat(animation, count).ToArray();
        if (mode == ShapesEditorMode.LipSync)
            result[0] = facial;
        else
            result[1] = facial;
        return result;
    }
}
