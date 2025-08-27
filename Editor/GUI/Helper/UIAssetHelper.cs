using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui;

internal static class UIAssetHelper
{
    public static VisualTreeAsset EnsureUxmlWithGuid(ref VisualTreeAsset? uxml, string guid , bool forceUpdate = false)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path))
        {
            throw new Exception($"UIUtility: Failed to convert GUID to path: {guid}");
        }
        return EnsureUxmlWithPath(ref uxml, path, forceUpdate);
    }
    public static VisualTreeAsset EnsureUxmlWithPath(ref VisualTreeAsset? uxml, string path, bool forceUpdate = false)
    {
        if (uxml == null || forceUpdate)
        {
            uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            if (uxml == null)
            {
                throw new Exception($"UIUtility: Failed to load UXML from path: {path}");
            }
        }
        return uxml;
    }
    public static StyleSheet EnsureUssWithGuid(ref StyleSheet? uss, string guid, bool forceUpdate = false)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path))
        {
            throw new Exception($"UIUtility: Failed to convert GUID to path: {guid}");
        }
        return EnsureUssWithPath(ref uss, path, forceUpdate);
    }
    public static StyleSheet EnsureUssWithPath(ref StyleSheet? uss, string path, bool forceUpdate = false)
    {
        if (uss == null || forceUpdate)
        {
            uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (uss == null)
            {
                throw new Exception($"UIUtility: Failed to load USS from path: {path}");
            }
        }
        return uss;
    }
}