using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui;

[CustomPropertyDrawer(typeof(SerializableCurveBinding))]
public class SerializableCurveBindingDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        var container = new VisualElement();

        container.Add(new PropertyField(property.FindPropertyRelative(SerializableCurveBinding.PathPropName)));
        container.Add(new PropertyField(property.FindPropertyRelative(SerializableCurveBinding.TypePropName)));
        container.Add(new PropertyField(property.FindPropertyRelative(SerializableCurveBinding.PropertyNamePropName)));
        container.Add(new PropertyField(property.FindPropertyRelative(SerializableCurveBinding.IsPPtrCurvePropName)));
        container.Add(new PropertyField(property.FindPropertyRelative(SerializableCurveBinding.IsDiscreteCurvePropName)));
        
        return container;
    }
}