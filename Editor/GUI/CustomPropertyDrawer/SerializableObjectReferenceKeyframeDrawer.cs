using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace com.aoyon.facetune.ui
{
    [CustomPropertyDrawer(typeof(SerializableObjectReferenceKeyframe))]
    internal class SerializableObjectReferenceKeyframeDrawer : PropertyDrawer
    {
        private const string TimePropName = "_time";
        private const string ValuePropName = "_value";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            // ルートとなるVisualElementを作成
            VisualElement container = new VisualElement();

            // _time プロパティのFloatFieldを作成
            PropertyField timeField = new PropertyField(property.FindPropertyRelative(TimePropName));
            timeField.label = "Time"; // ラベルを設定
            container.Add(timeField);

            // _value プロパティのObjectFieldを作成
            PropertyField valueField = new PropertyField(property.FindPropertyRelative(ValuePropName));
            valueField.label = "Value"; // ラベルを設定
            container.Add(valueField);

            return container;
        }
    }
} 