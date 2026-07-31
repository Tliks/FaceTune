using nadena.dev.ndmf.runtime;

namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(AvatarObjectReference))]
internal sealed class AvatarObjectReferenceDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using var scope = new EditorGUI.PropertyScope(position, label, property);
        var path = property.FindPropertyRelative("referencePath");
        var target = property.FindPropertyRelative("targetObject");
        var avatarRoot = FindCommonAvatarRoot(property.serializedObject.targetObjects);
        if (avatarRoot == null)
        {
            EditorGUI.LabelField(position, scope.content, new GUIContent(path.stringValue));
            return;
        }

        var current = Resolve(path.stringValue, target.objectReferenceValue as GameObject, avatarRoot);
        var field = EditorGUI.PrefixLabel(position, scope.content);
        EditorGUI.BeginChangeCheck();
        var next = EditorGUI.ObjectField(field, current, typeof(Transform), true) as Transform;
        if (!EditorGUI.EndChangeCheck()) return;

        target.objectReferenceValue = next != null ? next.gameObject : null;
        path.stringValue = next == null
            ? string.Empty
            : next == avatarRoot
                ? AvatarObjectReference.AvatarRootPath
                : RuntimeUtil.RelativePath(avatarRoot, next) ?? string.Empty;
    }

    private static Transform? Resolve(string path, GameObject? target, Transform avatarRoot)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (target != null && (target.transform == avatarRoot || target.transform.IsChildOf(avatarRoot)))
            return target.transform;
        if (path == AvatarObjectReference.AvatarRootPath) return avatarRoot;
        return avatarRoot.Find(path);
    }

    private static Transform? FindCommonAvatarRoot(UnityEngine.Object[] targets)
    {
        Transform? result = null;
        foreach (var target in targets)
        {
            if (target is not Component component) return null;
            var root = RuntimeUtil.FindAvatarInParents(component.transform);
            if (root == null || result != null && result != root) return null;
            result = root;
        }
        return result;
    }
}
