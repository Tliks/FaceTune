using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui;

internal static class UIAssetHelper
{
    public static void EnsureUxmlWithGuid(ref VisualTreeAsset uxml, string guid , bool forceUpdate = false)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path))
        {
            throw new Exception($"UIUtility: Failed to convert GUID to path: {guid}");
        }
        EnsureUxmlWithPath(ref uxml, path, forceUpdate);
    }
    public static void EnsureUxmlWithPath(ref VisualTreeAsset uxml, string path, bool forceUpdate = false)
    {
        if (uxml == null || forceUpdate)
        {
            uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            if (uxml == null)
            {
                throw new Exception($"UIUtility: Failed to load UXML from path: {path}");
            }
        }
    }
    public static void EnsureUssWithGuid(ref StyleSheet uss, string guid, bool forceUpdate = false)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path))
        {
            throw new Exception($"UIUtility: Failed to convert GUID to path: {guid}");
        }
        EnsureUssWithPath(ref uss, path, forceUpdate);
    }
    public static void EnsureUssWithPath(ref StyleSheet uss, string path, bool forceUpdate = false)
    {
        if (uss == null || forceUpdate)
        {
            uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (uss == null)
            {
                throw new Exception($"UIUtility: Failed to load USS from path: {path}");
            }
        }
    }
}