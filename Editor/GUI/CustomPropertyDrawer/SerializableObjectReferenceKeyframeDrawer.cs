using UnityEditor;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using com.aoyon.facetune;

namespace com.aoyon.facetune.ui
{
    [CustomPropertyDrawer(typeof(SerializableObjectReferenceKeyframe))]
    internal class SerializableObjectReferenceKeyframeDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            // ルートとなるVisualElementを作成
            VisualElement container = new VisualElement();

            // _time プロパティのFloatFieldを作成
            PropertyField timeField = new PropertyField(property.FindPropertyRelative("_time"));
            timeField.label = "Time"; // ラベルを設定
            container.Add(timeField);

            // _value プロパティのObjectFieldを作成
            PropertyField valueField = new PropertyField(property.FindPropertyRelative("_value"));
            valueField.label = "Value"; // ラベルを設定
            container.Add(valueField);

            return container;
        }
    }
} 