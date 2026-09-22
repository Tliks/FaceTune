using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal partial class FacialShapesEditor
{
    public static FacialShapesEditor? TryOpenSettingsEditor(
        SerializedProperty settings,
        ShapesEditorMode mode,
        int activeListIndex = 0)
    {
        if (settings.serializedObject.targetObjects.Length != 1
            || settings.serializedObject.targetObject is not Component component
            || !AvatarContext.TryGet(component.gameObject, out var avatar, out _)
            || TryOpenEditor() is not FacialShapesEditor window)
            return null;

        var lists = GetShapeLists(settings, mode);
        var usesAnimations = UsesAnimations(mode);
        var initial = lists
            .Select(list => (IReadOnlyList<BlendShapeWeightAnimation>)
                ShapeListSerialization.Read(list, usesAnimations))
            .ToArray();
        var readOnlyVisemes = mode == ShapesEditorMode.LipSync
            && (LipSyncSettings.Kind)settings
                .FindPropertyRelative(nameof(LipSyncSettings.Mode)).intValue
                == LipSyncSettings.Kind.BuiltIn;
        if (readOnlyVisemes)
        {
            var builtIn = MetaversePlatformSupport.GetForAvatar(avatar.Root.transform)
                .Select(support => support.GetBuiltInLipSyncShapes(avatar.FaceRenderer))
                .FirstOrDefault(value => value != null);
            if (builtIn != null)
            {
                var visemes = ReadVisemes(builtIn);
                for (var index = 0; index < visemes.Length; index++)
                    initial[index + 1] = visemes[index];
            }
        }

        var unavailable = GetUnavailableNames(avatar, mode, lists.Length);
        var resolver = new FacialAnimationResolver(avatar.Root);
        var background = resolver.ResolveIncoming(component.transform).ToList();
        if (resolver.TryResolve(component, out var local)) background.AddRange(local);

        window.StartSettingsContext(
            avatar.FaceRenderer,
            component,
            settings.propertyPath,
            mode,
            initial,
            unavailable,
            background,
            activeListIndex,
            readOnlyVisemes);
        return window;
    }

    private void StartSettingsContext(
        SkinnedMeshRenderer renderer,
        Component target,
        string settingsPropertyPath,
        ShapesEditorMode mode,
        IReadOnlyList<BlendShapeWeightAnimation>[] initialLists,
        ISet<string>[] unavailableNames,
        IReadOnlyList<BlendShapeWeightAnimation> background,
        int activeListIndex,
        bool readOnlyVisemes)
    {
        EndContext();
        activeListIndex = Mathf.Clamp(activeListIndex, 0, initialLists.Length - 1);

        _dataManagers = new BlendShapeOverrideManager[initialLists.Length];
        var serializedObject = new SerializedObject(this);
        serializedObject.Update();
        var managerProperties = serializedObject.FindProperty(nameof(_dataManagers));
        var usesAnimations = UsesAnimations(mode);
        for (var index = 0; index < initialLists.Length; index++)
        {
            var manager = new BlendShapeOverrideManager(
                serializedObject,
                managerProperties.GetArrayElementAtIndex(index),
                usesAnimations);
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
                usesAnimations ? GetInitialCurves(initial) : null);
        }

        InitializeList(0);
        if (mode == ShapesEditorMode.EyeBlinkSimple)
            InitializeList(1);
        else if (mode == ShapesEditorMode.LipSync && _dataManagers.Length > 1)
            InitializeList(Mathf.Max(1, activeListIndex));

        _context = new FacialShapesEditorContext(
            serializedObject,
            _dataManagers,
            mode,
            rootVisualElement,
            renderer,
            target,
            settingsPropertyPath,
            background,
            readOnlyVisemes ? 1 : _dataManagers.Length,
            InitializeList,
            TryChangeRenderer,
            SaveChanges);
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
        var lists = GetShapeLists(settings, context.Mode);
        var usesAnimations = UsesAnimations(context.Mode);
        for (var index = 0; index < lists.Length; index++)
        {
            if (!context.IsListEditable(index)
                || !context.DataManagers[index].IsInitialized) continue;
            ShapeListSerialization.Save(
                lists[index],
                context.DataManagers[index],
                usesAnimations);
        }
        serialized.ApplyModifiedProperties();
    }

    private static SerializedProperty[] GetShapeLists(
        SerializedProperty property,
        ShapesEditorMode mode)
        => mode switch
        {
            ShapesEditorMode.EyeBlinkSimple => new[]
            {
                property.FindPropertyRelative(nameof(EyeBlinkSettings.SimpleBlinkBlendShapes)),
                property.FindPropertyRelative(nameof(EyeBlinkSettings.SimpleConflictPreventionBlendShapes))
            },
            ShapesEditorMode.EyeBlinkCustom => new[]
            {
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

    private static bool UsesAnimations(ShapesEditorMode mode)
        => mode == ShapesEditorMode.EyeBlinkCustom;

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

    private static IReadOnlyList<BlendShapeWeightAnimation>[] ReadVisemes(
        VrcVisemeLipSyncShapes shapes)
        => shapes.GetOrderedShapes()
            .Select(list => (IReadOnlyList<BlendShapeWeightAnimation>)list
                .Select(shape => BlendShapeWeightAnimation.SingleFrame(shape.Name, shape.Weight))
                .ToArray())
            .ToArray();
}
