using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace com.aoyon.facetune.ui
{
    [CustomPropertyDrawer(typeof(SerializableCurveBinding))]
    public class SerializableCurveBindingDrawer : PropertyDrawer
    {
        private const string PathPropName = "_path";
        private const string TypePropName = "_type";
        private const string PropertyNamePropName = "_propertyName";
        private const string IsPPtrCurvePropName = "_isPPtrCurve";
        private const string IsDiscreteCurvePropName = "_isDiscreteCurve";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();

            container.Add(new PropertyField(property.FindPropertyRelative(PathPropName)));
            container.Add(new PropertyField(property.FindPropertyRelative(TypePropName)));
            container.Add(new PropertyField(property.FindPropertyRelative(PropertyNamePropName)));
            container.Add(new PropertyField(property.FindPropertyRelative(IsPPtrCurvePropName)));
            container.Add(new PropertyField(property.FindPropertyRelative("_isDiscreteCurve")));
            
            return container;
        }
    }
} 