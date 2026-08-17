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
    {
        if (string.IsNullOrEmpty(referencePath)) return null;
        var avatarRoot = RuntimeUtil.FindAvatarInParents(owner.transform);
        if (avatarRoot == null) return null;
        if (IsValidTarget(targetObject, avatarRoot)) return targetObject;
        if (referencePath == AvatarRootPath) return avatarRoot.gameObject;
        var resolved = avatarRoot.Find(referencePath).DestroyedAsNull();
        return resolved == null ? null : resolved.gameObject.DestroyedAsNull();
    }


#if UNITY_EDITOR
    internal static GameObject? Get(UnityEditor.SerializedProperty property)
    {
        var path = property.FindPropertyRelative(nameof(referencePath)).stringValue;
        if (string.IsNullOrEmpty(path)) return null;

        if (property.serializedObject.targetObject.DestroyedAsNull() is not Component owner) return null;
        var avatarRoot = RuntimeUtil.FindAvatarInParents(owner.transform);
        if (avatarRoot == null) return null;

        var target = property.FindPropertyRelative(nameof(targetObject)).objectReferenceValue as GameObject;
        if (IsValidTarget(target, avatarRoot)) return target;
        if (path == AvatarRootPath) return avatarRoot.gameObject;
        var resolved = avatarRoot.Find(path).DestroyedAsNull();
        return resolved == null ? null : resolved.gameObject.DestroyedAsNull();
    }


    internal static bool IsNull(UnityEditor.SerializedProperty property)
        => Get(property) == null;
#endif

    public void Set(GameObject? target)
    {
        target = target.DestroyedAsNull();
        targetObject = target;
        if (target == null)
        {
            referencePath = string.Empty;
            return;
        }

        var avatarRoot = RuntimeUtil.FindAvatarInParents(target.transform);
        if (avatarRoot == null)
        {
            referencePath = string.Empty;
            return;
        }

        referencePath = target.transform == avatarRoot
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
