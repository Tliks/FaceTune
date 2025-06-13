using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace com.aoyon.facetune.ui
{
    [CustomPropertyDrawer(typeof(SerializableCurveBinding))]
    public class SerializableCurveBindingDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();

            container.Add(new PropertyField(property.FindPropertyRelative("_path")));
            container.Add(new PropertyField(property.FindPropertyRelative("_type")));
            container.Add(new PropertyField(property.FindPropertyRelative("_propertyName")));
            container.Add(new PropertyField(property.FindPropertyRelative("_isPPtrCurve")));
            container.Add(new PropertyField(property.FindPropertyRelative("_isDiscreteCurve")));
            
            return container;
        }
    }
} 