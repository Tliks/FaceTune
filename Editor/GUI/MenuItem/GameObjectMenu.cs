using M = UnityEditor.MenuItem;

namespace com.aoyon.facetune.ui;

internal static class GameObjectMenu
{
    private const string BasePath = "GameObject/FaceTune/";
    private const int PRIORITY = 21;

    private static void IP(string guid, 
        bool unpackRoot = false,
        GameObject? parent = null, 
        bool isFirstSibling = false
    )
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
        if (prefab == null)
        {
            Debug.LogError("Prefab not found");
            return;
        }

        Undo.IncrementCurrentGroup();
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (parent == null) parent = Selection.activeGameObject;
        
        Undo.RegisterCreatedObjectUndo(instance, "Create " + instance.name);
        instance.transform.SetParent(parent.transform, false);
        
        if (isFirstSibling)
        {
            instance.transform.SetAsFirstSibling();
        }
        
        if (unpackRoot)
        {
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);
        }
        
        Selection.activeObject = instance;
        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
    }
    
    // Sample
    /*
    private const string SamplePath = BasePath + "Sample/";
    [M(SamplePath + "Chiffon/FT Chiffon Default")] 
    static void SampleChiffonDefault() => IP("cee5d5eb715bcd746ae09fcfaf49acaf");
    */

    // Template
    /*
    private const string TemplatePath = BasePath + "Template/";
    [M(TemplatePath + "FT Basic", false, 1)] 
    static void TemplateBasic() => IP("be74fc2f75a4f344ba57325ed2eed5dd");
    [M(TemplatePath + "FT HandSign", false, 2)] 
    static void TemplateHandSign() => IP("b471a875cba6f294d89c7ae0b433315b");
    [M(TemplatePath + "FT Blending", false, 3)] 
    static void TemplateBlending() => IP("e1de823adfddea84c821e0354a2e5e32");

    [M(TemplatePath + "FT FaceMorph First", false, 4)] 
    static void TemplateFaceMorphFirst() => IP("d3334de8933d1a44da234910c8e7455d");

    [M(TemplatePath + "FT Preset", false, 5)] 
    static void TemplatePreset() => IP("e2c7cd827b519e24b940460ac2760740");
    */
    [M(BasePath + "Template Base", false, PRIORITY)] 
    static void TemplateBase() => IP("e643b160cc0f24a4fa8e33fb4df1fe7e", true);

    [M(BasePath + "Condition", false, PRIORITY + 1)] 
    static void Condition() => IP("20aca02f84d174940bb4ca676555589a", true);
    
    private const string MenuPath = BasePath + "Menu/";
    [M(MenuPath + "single", false, PRIORITY + 2)] 
    static void MenuSingle() => IP("a045ae2cad411ae43b4c008ff814957e", true);

    [M(MenuPath + "exclusive", false, PRIORITY + 3)] 
    static void MenuExclusive() => IP("9e1741e66ac069742976cf8c7e785a35", true);

    [M(MenuPath + "blending", false, PRIORITY + 4)] 
    static void MenuBlending() => IP("557c13125870f764bb20173aa14b004f", true);

    // Additional
    /*
    private const string AdditionalPath = BasePath + "Additional/";
    [M(AdditionalPath + "Condition (single)", false, PRIORITY + 10)] 
    static void ConditionSingle() => IP("20aca02f84d174940bb4ca676555589a");
    [M(AdditionalPath + "Condition (multiple)", false, PRIORITY + 11)] 
    static void ConditionMultiple() => IP("e3414e27e554caa41929c86d9f263a7c");
    [M(AdditionalPath + "Menu (single)", false, PRIORITY + 12)] 
    static void MenuSingle() => IP("a045ae2cad411ae43b4c008ff814957e");
    [M(AdditionalPath + "Menu (multiple)", false, PRIORITY + 13)] 
    static void MenuMultiple() => IP("557c13125870f764bb20173aa14b004f");
    */

    // etc
    /* 
    private const string EtcPath = BasePath + "etc/";
    [M(EtcPath + "Lipsync Override", false, PRIORITY + 20)] 
    static void EtcLipsyncOverride() => IP("48b0b3096f2029640a1f79b1bcd39a00");
    [M(EtcPath + "Morph Override", false, PRIORITY + 21)] 
    static void EtcMorphOverride() => IP("45cd2ee49c64b0a448d56589bfff1c7e");
    [M(EtcPath + "Option", false, PRIORITY + 22)] 
    // static void EtcOption() => IP("552f6348a1639fd45bd202c3614c5c2a");
    [M(EtcPath + "Override Menu", false, PRIORITY + 23)] 
    static void EtcOverrideMenu() => IP("dbb95b5ca0abfd2478f07d702c2e48b6");
    */
}
