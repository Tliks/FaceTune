using Aoyon.FaceTune.Gui.ShapesEditor;
using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Gui;

/// <summary>Resolves the current setting's source shapes, then replaces its reset list.</summary>
internal static class ConflictShapeAutoSetup
{
    internal static void SetupBlink(SerializedProperty target)
    {
        var settings = Parent(target);
        if (settings.serializedObject.targetObjects.Length != 1
            || SerializedObjectGUIContext.GetComponent(settings.serializedObject) is not { } component
            || !AvatarContext.TryGet(component.gameObject, out var avatar, out _)) return;
        var unavailable = AvatarContext.GetUnavailableBlendShapeNames(
            avatar.Root, FaceTuneWriteKind.EyeBlinkAnimation);
        var source = ShapeListSerialization.Read(
                settings.FindPropertyRelative(nameof(EyeBlinkSettings.SimpleBlinkBlendShapes)), false)
            .Where(shape => !unavailable.Contains(shape.Name)
                            && !Mathf.Approximately(shape.Weight(0f), 0f))
            .Select(shape => new BlendShapeWeight(shape.Name, shape.Weight(0f)));
        if (TryDetectBlink(avatar, source, out var names)) ReplaceSerialized(target, names);
    }

    internal static void SetupLipSync(SerializedProperty target)
    {
        var settings = Parent(target);
        if (settings.serializedObject.targetObjects.Length != 1
            || SerializedObjectGUIContext.GetComponent(settings.serializedObject) is not { } component
            || !AvatarContext.TryGet(component.gameObject, out var avatar, out _)) return;
        var mode = (LipSyncSettings.Kind)settings.FindPropertyRelative(nameof(LipSyncSettings.Mode)).intValue;
        var unavailable = AvatarContext.GetUnavailableBlendShapeNames(
            avatar.Root, FaceTuneWriteKind.LipSyncAnimation);
        IEnumerable<string> source;
        if (mode == LipSyncSettings.Kind.Custom)
        {
            var shapes = settings.FindPropertyRelative(nameof(LipSyncSettings.Shapes));
            source = VrcVisemeLipSyncShapes.PropertyNames.SelectMany(propertyName =>
                    ShapeListSerialization.Read(shapes.FindPropertyRelative(propertyName), false))
                .Where(shape => !unavailable.Contains(shape.Name)
                                && !Mathf.Approximately(shape.Weight(0f), 0f))
                .Select(shape => shape.Name).ToArray();
        }
        else
        {
            var builtIn = MetaversePlatformSupport.GetForAvatar(avatar.Root.transform)
                .Select(support => support.GetBuiltInLipSyncShapes(avatar.FaceRenderer))
                .FirstOrDefault(value => value != null);
            if (builtIn == null) return;
            source = builtIn.GetOrderedShapes().SelectMany(shapes => shapes)
                .Where(shape => !unavailable.Contains(shape.Name)
                                && !Mathf.Approximately(shape.Weight, 0f))
                .Select(shape => shape.Name);
        }
        if (TryDetect(avatar, source, out var names)) ReplaceSerialized(target, names);
    }

    internal static void SetupBlink(FacialShapesEditorContext context)
    {
        if (context.ModeSession is not EyeBlinkModeSession
            { Mode: EyeBlinkSettings.Kind.SimpleAnimation }) return;
        var sourceManager = context.DataManagers[0];
        var source = sourceManager.GetTargetIndices(index =>
                !sourceManager.IsUnavailable(index)
                && !Mathf.Approximately(sourceManager.GetShapeWeight(index), 0f))
            .Select(index => new BlendShapeWeight(
                sourceManager.AllKeys[index], sourceManager.GetShapeWeight(index)));
        ReplaceEditor(context, context.DataManagers[1], source);
    }

    internal static void SetupLipSync(FacialShapesEditorContext context, LipSyncEditing editing)
    {
        var shapes = editing.PreviewShapes;
        if (shapes == null) return;
        var source = shapes.GetOrderedShapes().SelectMany(values => values)
            .Where(shape => !Mathf.Approximately(shape.Weight, 0f)
                            && !editing.UnavailableNames.Contains(shape.Name))
            .Select(shape => shape.Name);
        ReplaceEditor(context, context.DataManagers[0], source);
    }

    private static bool TryDetectBlink(
        AvatarContext avatar, IEnumerable<BlendShapeWeight> source, out IReadOnlyList<string> result)
    {
        result = Array.Empty<string>();
        var mesh = avatar.FaceRenderer.sharedMesh;
        if (mesh == null || !mesh.isReadable) return false;
        var shapes = source.Where(shape => !string.IsNullOrEmpty(shape.Name)
                                           && mesh.GetBlendShapeIndex(shape.Name) >= 0)
            .GroupBy(shape => shape.Name, StringComparer.Ordinal)
            .Select(group => group.First()).ToArray();
        if (shapes.Length == 0) return false;
        var detected = ConflictShapeDetector.DetectBlink(mesh, shapes,
            AvatarContext.GetUnavailableBlendShapeNames(avatar.Root, FaceTuneWriteKind.FacialData));
        if (detected == null) return false;
        result = detected;
        return true;
    }

    private static SerializedProperty Parent(SerializedProperty property)
    {
        var path = property.propertyPath;
        return property.serializedObject.FindProperty(path.Substring(0, path.LastIndexOf('.')));
    }

    private static bool TryDetect(AvatarContext avatar, IEnumerable<string> source, out IReadOnlyList<string> result)
    {
        result = Array.Empty<string>();
        var mesh = avatar.FaceRenderer.sharedMesh;
        if (mesh == null || !mesh.isReadable) return false;
        var names = source.Where(name => !string.IsNullOrEmpty(name)
                                         && mesh.GetBlendShapeIndex(name) >= 0)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (names.Length == 0) return false;
        var detected = ConflictShapeDetector.Detect(mesh, names,
            AvatarContext.GetUnavailableBlendShapeNames(avatar.Root, FaceTuneWriteKind.FacialData));
        if (detected == null) return false;
        result = detected;
        return true;
    }

    private static void ReplaceEditor(
        FacialShapesEditorContext context, BlendShapeOverrideManager target, IEnumerable<string> source)
    {
        if (context.Target is not Component component
            || !AvatarContext.TryGet(component.gameObject, out var avatar, out _)
            || !TryDetect(avatar, source, out var names)) return;
        ReplaceEditorTarget(target, names);
    }

    private static void ReplaceEditor(
        FacialShapesEditorContext context, BlendShapeOverrideManager target,
        IEnumerable<BlendShapeWeight> source)
    {
        if (context.Target is not Component component
            || !AvatarContext.TryGet(component.gameObject, out var avatar, out _)
            || !TryDetectBlink(avatar, source, out var names)) return;
        ReplaceEditorTarget(target, names);
    }

    private static void ReplaceEditorTarget(
        BlendShapeOverrideManager target, IReadOnlyList<string> names)
    {
        target.ReplaceTargetValues(names.Select(target.GetIndexForShape)
            .Where(index => index >= 0)
            .Select(index => (index, 0f)));
    }

    private static void ReplaceSerialized(SerializedProperty target, IReadOnlyList<string> names)
    {
        // An empty result from a valid reference is a valid replacement.
        target.arraySize = names.Count;
        for (var index = 0; index < names.Count; index++)
            target.GetArrayElementAtIndex(index).CopyFrom(new BlendShapeWeight(names[index], 0f));
    }
}
