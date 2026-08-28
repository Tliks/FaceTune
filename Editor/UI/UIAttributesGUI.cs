namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(ToggleLeftAttribute))]
internal sealed class ToggleLeftDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        => GUIHelper.DrawToggleLeft(position, property, label);
}

