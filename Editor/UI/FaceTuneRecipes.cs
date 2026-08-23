namespace Aoyon.FaceTune.Gui;

internal enum FaceTuneRecipeCategory
{
    Expression,
    Pattern,
    Control
}

internal sealed record FaceTuneRecipe(
    string Id,
    FaceTuneRecipeCategory Category,
    string TitleKey,
    string DescriptionKey,
    string GuideKey,
    string? PrefabGuid = null);

internal static class FaceTuneRecipes
{
    public static readonly FaceTuneRecipe Expression = new(
        "expression",
        FaceTuneRecipeCategory.Expression,
        "window.recipe.expression.title",
        "window.recipe.expression.description",
        "window.result.expression.guide");

    public static readonly IReadOnlyList<FaceTuneRecipe> All = new[]
    {
        Expression,
        new FaceTuneRecipe("hand-sign", FaceTuneRecipeCategory.Pattern,
            "window.recipe.handSign.title", "window.recipe.handSign.description",
            "window.result.pattern.guide", "e7a261d8cf051454ea0c41e427463276"),
        new FaceTuneRecipe("left-hand", FaceTuneRecipeCategory.Pattern,
            "window.recipe.leftHand.title", "window.recipe.leftHand.description",
            "window.result.pattern.guide", "9f044864c335d38499244290e12697d3"),
        new FaceTuneRecipe("right-hand", FaceTuneRecipeCategory.Pattern,
            "window.recipe.rightHand.title", "window.recipe.rightHand.description",
            "window.result.pattern.guide", "73a1d844c3155444696e054ae47b9f7c"),
        new FaceTuneRecipe("left-hand-priority", FaceTuneRecipeCategory.Pattern,
            "window.recipe.leftHandPriority.title", "window.recipe.leftHandPriority.description",
            "window.result.pattern.guide", "376099cca4d264b4fbfbeeb7901dc770"),
        new FaceTuneRecipe("right-hand-priority", FaceTuneRecipeCategory.Pattern,
            "window.recipe.rightHandPriority.title", "window.recipe.rightHandPriority.description",
            "window.result.pattern.guide", "c259edc6efd4aaa4bba3b1636557cc3b"),
        new FaceTuneRecipe("blending", FaceTuneRecipeCategory.Pattern,
            "window.recipe.blending.title", "window.recipe.blending.description",
            "window.result.pattern.guide", "9eb5bf9eeb8dc81488fb9453d21f3510"),
        new FaceTuneRecipe("lock-facial", FaceTuneRecipeCategory.Control,
            "window.recipe.lockFacial.title", "window.recipe.lockFacial.description",
            "window.result.control.guide", "e64bb9b02902322459b7dd874b354d70"),
        new FaceTuneRecipe("disable-eye-blink", FaceTuneRecipeCategory.Control,
            "window.recipe.disableEyeBlink.title", "window.recipe.disableEyeBlink.description",
            "window.result.control.guide", "b70a485c5d127c340938c0b130d49916"),
        new FaceTuneRecipe("disable-lip-sync", FaceTuneRecipeCategory.Control,
            "window.recipe.disableLipSync.title", "window.recipe.disableLipSync.description",
            "window.result.control.guide", "0a2f5a9d8bc9b1142bb9afbd9034767c"),
        new FaceTuneRecipe("mmd-support", FaceTuneRecipeCategory.Control,
            "window.recipe.mmdSupport.title", "window.recipe.mmdSupport.description",
            "window.result.control.guide", "c145b0d2d80d01c4288e436dfbdc26b9")
    };
}

internal static class FaceTuneRecipeOperations
{
    private const string TemplateGuid = "e643b160cc0f24a4fa8e33fb4df1fe7e";

    public static GameObject AddStandardSetup(GameObject avatarRoot, bool includeDefaultExpression)
    {
        var root = Instantiate(TemplateGuid, avatarRoot, false);
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);
        foreach (Transform child in root.transform)
        {
            if (child.name == "Option") continue;
            if (PrefabUtility.IsPartOfPrefabInstance(child))
                PrefabUtility.UnpackPrefabInstance(child.gameObject, PrefabUnpackMode.Completely, InteractionMode.UserAction);
        }

        if (!includeDefaultExpression)
        {
            var defaultExpression = root.GetComponentsInChildren<ExpressionComponent>(true)
                .FirstOrDefault(expression => expression.transform.parent == root.transform
                                           && expression.gameObject.name == "Default");
            if (defaultExpression != null)
                Undo.DestroyObjectImmediate(defaultExpression.gameObject);
        }
        return root;
    }

    public static GameObject AddRecipe(GameObject avatarRoot, FaceTuneRecipe recipe)
    {
        var parent = FindDestination(avatarRoot, recipe.Category);
        if (recipe.PrefabGuid != null)
            return Instantiate(recipe.PrefabGuid, parent, recipe.Category == FaceTuneRecipeCategory.Pattern);

        var expressionObject = new GameObject("Expression");
        Undo.RegisterCreatedObjectUndo(expressionObject, "Add FaceTune Expression");
        Undo.SetTransformParent(expressionObject.transform, parent.transform, "Place FaceTune Expression");
        Undo.AddComponent<ExpressionComponent>(expressionObject);
        return expressionObject;
    }

    public static GameObject? FindStandardRoot(GameObject avatarRoot)
        => avatarRoot.GetComponentsInChildren<SettingsComponent>(true)
            .Where(settings => settings.HasFacialBlendShapes)
            .Select(settings => settings.gameObject)
            .FirstOrDefault(candidate => candidate.GetComponents<MenuComponent>()
                .Any(menu => menu.MenuKind == MenuComponent.Kind.Folder));

    private static GameObject FindDestination(GameObject avatarRoot, FaceTuneRecipeCategory category)
    {
        var standardRoot = FindStandardRoot(avatarRoot);
        if (standardRoot == null) return avatarRoot;
        if (category != FaceTuneRecipeCategory.Control) return standardRoot;

        return standardRoot.GetComponentsInChildren<MenuComponent>(true)
            .Where(menu => menu.MenuKind == MenuComponent.Kind.Folder && menu.gameObject != standardRoot)
            .Select(menu => menu.gameObject)
            .FirstOrDefault(candidate => candidate.name == "Option")
            ?? standardRoot;
    }

    private static GameObject Instantiate(string guid, GameObject parent, bool unpack)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) throw new InvalidOperationException($"FaceTune prefab not found: {guid}");

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
        Undo.RegisterCreatedObjectUndo(instance, $"Add {prefab.name}");
        if (unpack)
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.UserAction);
        return instance;
    }
}
