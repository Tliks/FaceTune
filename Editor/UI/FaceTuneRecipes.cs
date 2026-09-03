namespace Aoyon.FaceTune;

internal enum FaceTunePatternKey
{
    LeftHandPriority,
    RightHandPriority,
    LeftHand,
    RightHand,
    BothHands,
    Blending,
    Expression
}

internal static class FaceTunePatternPrefabCatalog
{
    public static string? GetPrefabGuid(FaceTunePatternKey pattern)
        => pattern switch
        {
            FaceTunePatternKey.LeftHandPriority => "376099cca4d264b4fbfbeeb7901dc770",
            FaceTunePatternKey.RightHandPriority => "c259edc6efd4aaa4bba3b1636557cc3b",
            FaceTunePatternKey.LeftHand => "9f044864c335d38499244290e12697d3",
            FaceTunePatternKey.RightHand => "73a1d844c3155444696e054ae47b9f7c",
            FaceTunePatternKey.BothHands => "e7a261d8cf051454ea0c41e427463276",
            FaceTunePatternKey.Blending => "9eb5bf9eeb8dc81488fb9453d21f3510",
            FaceTunePatternKey.Expression => null,
            _ => throw new ArgumentOutOfRangeException(nameof(pattern), pattern, null)
        };

    public static bool ShouldFlatten(FaceTunePatternKey pattern)
        => pattern switch
        {
            FaceTunePatternKey.LeftHandPriority
                or FaceTunePatternKey.RightHandPriority
                or FaceTunePatternKey.BothHands
                or FaceTunePatternKey.Blending => true,
            _ => false
        };
}

internal static class FaceTuneRecipes
{
    private const string TemplateGuid = "e643b160cc0f24a4fa8e33fb4df1fe7e";

    public static GameObject AddStandardSetup(
        GameObject avatarRoot,
        bool enableDefaultExpression,
        bool applyFaceRendererSettings = true)
    {
        var root = Instantiate(TemplateGuid, avatarRoot, false);
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.UserAction);

        if (applyFaceRendererSettings
            && AvatarContext.TryGet(avatarRoot, out var context, out _))
        {
            ApplyFaceRendererSettings(context, root);
        }

        if (!enableDefaultExpression)
        {
            var defaultExpression = root.GetComponentsInChildren<ExpressionComponent>(true)
                .FirstOrDefault(expression => expression.transform.parent == root.transform
                                           && expression.gameObject.name == "Default");
            if (defaultExpression != null)
            {
                Undo.RecordObject(defaultExpression.gameObject, "Disable Default Expression");
                defaultExpression.gameObject.SetActive(false);
                defaultExpression.gameObject.tag = "EditorOnly";
            }
        }
        return root;
    }

    private static void ApplyFaceRendererSettings(AvatarContext context, GameObject destination)
    {
        var settings = destination.GetComponent<SettingsComponent>();
        if (settings == null)
            settings = Undo.AddComponent<SettingsComponent>(destination);
        else
            Undo.RecordObject(settings, "Apply FaceRenderer Settings");

        settings.HasFacialBlendShapes = true;
        settings.FacialBlendShapes = new FacialBlendShapeData
        {
            BlendShapeAnimations = context.FaceRenderer
                .GetNonZeroBlendShapeAnimations(context.FaceMesh)
                .ToList()
        };
        settings.ApplyToRenderer = true;
    }

    public static ExpressionComponent AddExpression(GameObject parent)
    {
        var expressionObject = new GameObject("Expression");
        Undo.RegisterCreatedObjectUndo(expressionObject, "Add FaceTune Expression");
        Undo.SetTransformParent(expressionObject.transform, parent.transform, "Place FaceTune Expression");
        return Undo.AddComponent<ExpressionComponent>(expressionObject);
    }

    public static ExpressionDataComponent AddExpressionData(Transform? parent)
    {
        var expressionDataObject = new GameObject("Expression Data");
        Undo.RegisterCreatedObjectUndo(expressionDataObject, "Add FaceTune Expression Data");
        if (parent != null)
            Undo.SetTransformParent(expressionDataObject.transform, parent, "Place FaceTune Expression Data");
        return Undo.AddComponent<ExpressionDataComponent>(expressionDataObject);
    }

    public static GameObject AddPattern(
        GameObject avatarRoot,
        FaceTunePatternKey pattern,
        GameObject? standardRoot = null)
    {
        var parent = standardRoot ?? FindStandardRoot(avatarRoot) ?? avatarRoot;
        var prefabGuid = FaceTunePatternPrefabCatalog.GetPrefabGuid(pattern);
        if (prefabGuid != null)
        {
            if (FaceTunePatternPrefabCatalog.ShouldFlatten(pattern))
                return InstantiatePatternContents(prefabGuid, parent);

            var instance = Instantiate(prefabGuid, parent, true);
            return FindLastExpression(instance) ?? instance;
        }

        return AddExpression(parent).gameObject;
    }

    public static GameObject? FindStandardRoot(GameObject avatarRoot)
        => avatarRoot.GetComponentsInChildren<SettingsComponent>(true)
            .Where(settings => settings.HasFacialBlendShapes)
            .Select(settings => settings.gameObject)
            .LastOrDefault();

    private static GameObject InstantiatePatternContents(string guid, GameObject parent)
    {
        var instance = Instantiate(guid, parent, true, false);
        var contents = instance.transform.Cast<Transform>().ToArray();
        foreach (var content in contents)
        {
            content.SetParent(parent.transform, false);
            Undo.RegisterCreatedObjectUndo(content.gameObject, $"Add {content.name}");
        }

        var lastExpression = contents
            .SelectMany(content => content.GetComponentsInChildren<ExpressionComponent>(true))
            .LastOrDefault();
        Object.DestroyImmediate(instance);
        return lastExpression?.gameObject
               ?? contents.LastOrDefault()?.gameObject
               ?? parent;
    }

    private static GameObject? FindLastExpression(GameObject root)
        => root.GetComponentsInChildren<ExpressionComponent>(true)
            .LastOrDefault()?.gameObject;

    private static GameObject Instantiate(
        string guid,
        GameObject parent,
        bool unpack,
        bool registerUndo = true)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException($"FaceTune prefab not found: {guid}");

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
        if (registerUndo)
            Undo.RegisterCreatedObjectUndo(instance, $"Add {prefab.name}");
        if (unpack)
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.UserAction);
        return instance;
    }
}
