namespace Aoyon.FaceTune.Gui;

internal sealed class FoldoutState
{
    public bool Expanded;

    public FoldoutState(bool expanded)
        => Expanded = expanded;
}

internal static class GUIState
{
    private static readonly Dictionary<string, object> PropertyStates = new();

    public static T Get<T>(SerializedProperty property, string scope, Func<T> create)
        where T : class
    {
        var targets = string.Join(",", property.serializedObject.targetObjects.Select(target => target.GetInstanceID()));
        var key = $"{typeof(T).FullName}:{scope}:{targets}:{property.propertyPath}";
        return (T)PropertyStates.GetOrAdd(key, _ => create());
    }

    public static FoldoutState Foldout(SerializedProperty property, string scope)
        => Get(property, scope, () => new FoldoutState(false));
}
