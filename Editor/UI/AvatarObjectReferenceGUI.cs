using nadena.dev.ndmf.runtime;

namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(AvatarObjectReference))]
internal sealed class AvatarObjectReferenceDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUIHelper.RegisterPropertyRegion(position, property);
        var path = property.FindPropertyRelative("referencePath");
        var target = property.FindPropertyRelative("targetObject");
        var avatarRoot = FindCommonAvatarRoot(property.serializedObject.targetObjects);
        if (avatarRoot == null)
        {
            EditorGUI.LabelField(position, label, new GUIContent(path.stringValue));
            return;
        }

        var current = Resolve(path.stringValue, target.objectReferenceValue as GameObject, avatarRoot);
        var field = EditorGUI.PrefixLabel(position, label);
        EditorGUI.BeginChangeCheck();
        var next = EditorGUI.ObjectField(field, current, typeof(Transform), true) as Transform;
        if (!EditorGUI.EndChangeCheck()) return;

        AvatarObjectReference.Set(property, next != null ? next.gameObject : null);
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
            if (target.DestroyedAsNull() is not Component component) return null;
            var root = RuntimeUtil.FindAvatarInParents(component.transform);
            if (root == null || result != null && result != root) return null;
            result = root;
        }
        return result;
    }
}
