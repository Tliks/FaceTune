using nadena.dev.ndmf.runtime;

namespace Aoyon.FaceTune;

/// <summary>Serialized avatar reference shared by current and legacy components.</summary>
[Serializable]
internal sealed class AvatarObjectReference : IEquatable<AvatarObjectReference>
{
    internal const string AvatarRootPath = "$$$AVATAR_ROOT$$$";

    [SerializeField] private string referencePath = string.Empty;
    [SerializeField] private GameObject? targetObject;

    public AvatarObjectReference()
    {
    }

    public AvatarObjectReference(GameObject? target)
    {
        Set(target);
    }

    public GameObject? Get(Component owner)
        => ResolveTarget(owner.transform, referencePath, targetObject);


#if UNITY_EDITOR
    internal static GameObject? Get(UnityEditor.SerializedProperty property)
    {
        var path = property.FindPropertyRelative(nameof(referencePath)).stringValue;
        if (string.IsNullOrEmpty(path)) return null;
        if (property.serializedObject.targetObject.DestroyedAsNull() is not Component owner) return null;

        var target = property.FindPropertyRelative(nameof(targetObject)).objectReferenceValue as GameObject;
        return ResolveTarget(owner.transform, path, target);
    }


    internal static bool IsNull(UnityEditor.SerializedProperty property)
        => Get(property) == null;

    internal static void Set(UnityEditor.SerializedProperty property, GameObject? target)
    {
        var path = property.FindPropertyRelative(nameof(referencePath));
        var targetProperty = property.FindPropertyRelative(nameof(targetObject));
        target = target.DestroyedAsNull();
        targetProperty.objectReferenceValue = target;
        path.stringValue = GetAvatarRelativePath(target);
    }
#endif

    public void Set(GameObject? target)
    {
        target = target.DestroyedAsNull();
        targetObject = target;
        referencePath = GetAvatarRelativePath(target);
    }

    private static GameObject? ResolveTarget(
        Transform owner,
        string path,
        GameObject? target)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var avatarRoot = RuntimeUtil.FindAvatarInParents(owner);
        if (avatarRoot == null) return null;
        if (IsValidTarget(target, avatarRoot)) return target;
        if (path == AvatarRootPath) return avatarRoot.gameObject;
        var resolved = avatarRoot.Find(path);
        return resolved == null ? null : resolved.gameObject.DestroyedAsNull();
    }

    private static string GetAvatarRelativePath(GameObject? target)
    {
        if (target == null) return string.Empty;
        var avatarRoot = RuntimeUtil.FindAvatarInParents(target.transform);
        if (avatarRoot == null) return string.Empty;
        return target.transform == avatarRoot
            ? AvatarRootPath
            : RuntimeUtil.RelativePath(avatarRoot, target.transform) ?? string.Empty;
    }

    private static bool IsValidTarget(GameObject? target, Transform avatarRoot)
        => target != null
        && (target.transform == avatarRoot || target.transform.IsChildOf(avatarRoot));

    public bool Equals(AvatarObjectReference? other)
        => other != null && targetObject == other.targetObject && referencePath == other.referencePath;

    public override bool Equals(object? obj) => obj is AvatarObjectReference other && Equals(other);
    public override int GetHashCode() => referencePath?.GetHashCode() ?? 0;
}
