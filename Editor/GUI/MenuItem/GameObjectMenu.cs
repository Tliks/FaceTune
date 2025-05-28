using M = UnityEditor.MenuItem;

namespace com.aoyon.facetune.ui;

internal static class GameObjectMenu
{
    private const string BasePath = "GameObject/FaceTune/";

    private static void IP(string guid, GameObject? parent = null, bool isFirstSibling = true)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
        if (prefab == null)
        {
            Debug.LogError("Prefab not found");
        }
        else
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (parent == null) parent = Selection.activeGameObject;
            instance.transform.SetParent(parent.transform, false);
            if (isFirstSibling)
            {
                instance.transform.SetAsFirstSibling();
            }
            Selection.activeObject = instance;
        }
    }

    /*
    GameObject/FaceTune/
    ├── Common/
    │   ├── One Hand Gesture
    │   └── Two Hand Gesture
    ├── Condition/
    │   ├── Basic
    │   ├── Blending
    │   ├── FaceMorph First
    │   ├── HandSign
    │   ├── One Hand Gesture Condition Left
    │   └── One Hand Gesture Condition Right
    ├── Sample/
    │   └── FaceTune Chiffon base
    ├── Template/
    │   ├── Template Base
    │   ├── Template Blending
    │   ├── Template FaceMorph First
    │   ├── Template HandSign
    │   └── Template Preset
    ├── etc/
    │   ├── Lipsync Override
    │   ├── Morph Override
    │   ├── Option
    │   └── Override Menu
    └── Additional/
        ├── Condition (multiple)
        ├── Condition (single)
        ├── Menu (multiple)
        └── Menu (single)
    */

    // Common
    private const string CommonPath = BasePath + "Common/";
    [M(CommonPath + "One Hand Gesture")] 
    private static void OneHandGesture() => IP("3fed0a63141d68f4991a7a4c2ec7aae6");

    [M(CommonPath + "Two Hand Gesture")] 
    private static void TwoHandGesture() => IP("c345a3d2b1af0ef45b86390b30e40051");

    // Condition
    private const string ConditionPath = BasePath + "Condition/";
    [M(ConditionPath + "Basic")] 
    private static void ConditionBasic() => IP("c259edc6efd4aaa4bba3b1636557cc3b");

    [M(ConditionPath + "Blending")] 
    private static void ConditionBlending() => IP("9eb5bf9eeb8dc81488fb9453d21f3510");

    [M(ConditionPath + "FaceMorph First")] 
    private static void ConditionFaceMorphFirst() => IP("618bf06062904004f99355468c34ac7c");

    [M(ConditionPath + "HandSign")] 
    private static void ConditionHandSign() => IP("e7a261d8cf051454ea0c41e427463276");

    [M(ConditionPath + "One Hand Gesture Condition Left")] 
    private static void ConditionOneHandGestureLeft() => IP("9f044864c335d38499244290e12697d3");

    [M(ConditionPath + "One Hand Gesture Condition Right")] 
    private static void ConditionOneHandGestureRight() => IP("73a1d844c3155444696e054ae47b9f7c");

    // Sample
    private const string SamplePath = BasePath + "Sample/";
    [M(SamplePath + "FaceTune Chiffon base")] 
    private static void SampleFaceTuneChiffonBase() => IP("7771562b4cf54b949ab72930bff1bdc8");

    // Template
    private const string TemplatePath = BasePath + "Template/";
    [M(TemplatePath + "Template Base")] 
    private static void TemplateBase() => IP("e643b160cc0f24a4fa8e33fb4df1fe7e");

    [M(TemplatePath + "Template Blending")] 
    private static void TemplateBlending() => IP("e1de823adfddea84c821e0354a2e5e32");

    [M(TemplatePath + "Template FaceMorph First")] 
    private static void TemplateFaceMorphFirst() => IP("d3334de8933d1a44da234910c8e7455d");

    [M(TemplatePath + "Template HandSign")] 
    private static void TemplateHandSign() => IP("b471a875cba6f294d89c7ae0b433315b");

    [M(TemplatePath + "Template Preset")] 
    private static void TemplatePreset() => IP("e2c7cd827b519e24b940460ac2760740");

    // etc
    private const string EtcPath = BasePath + "etc/";
    [M(EtcPath + "Lipsync Override")] 
    private static void EtcLipsyncOverride() => IP("48b0b3096f2029640a1f79b1bcd39a00");

    [M(EtcPath + "Morph Override")] 
    private static void EtcMorphOverride() => IP("45cd2ee49c64b0a448d56589bfff1c7e");

    [M(EtcPath + "Option")] 
    private static void EtcOption() => IP("552f6348a1639fd45bd202c3614c5c2a");

    [M(EtcPath + "Override Menu")] 
    private static void EtcOverrideMenu() => IP("dbb95b5ca0abfd2478f07d702c2e48b6");

    // Additional
    private const string AdditionalPath = BasePath + "Additional/";
    [M(AdditionalPath + "Condition (multiple)")] 
    private static void ConditionMultiple() => IP("e3414e27e554caa41929c86d9f263a7c");

    [M(AdditionalPath + "Condition (single)")] 
    private static void ConditionSingle() => IP("20aca02f84d174940bb4ca676555589a");

    [M(AdditionalPath + "Menu (multiple)")] 
    private static void Additional() => IP("557c13125870f764bb20173aa14b004f");

    [M(AdditionalPath + "Menu (single)")] 
    private static void MenuSingle() => IP("a045ae2cad411ae43b4c008ff814957e");
}
