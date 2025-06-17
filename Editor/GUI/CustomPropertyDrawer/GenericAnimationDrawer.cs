using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace com.aoyon.facetune.ui
{
    [CustomPropertyDrawer(typeof(GenericAnimation))]
    internal class GenericAnimationDrawer : PropertyDrawer
    {
        private const string CurveBindingPropName = "_curveBinding";
        private const string CurvePropName = "_curve";
        private const string ObjectReferenceCurvePropName = "_objectReferenceCurve";
        private const string PropertyNamePropName = "_propertyName";
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            // ルートとなるVisualElementを作成
            VisualElement container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row; // 横並びにする
            container.style.alignItems = Align.Center; // 中央揃え
            container.style.height = EditorGUIUtility.singleLineHeight; // 高さを固定

            SerializedProperty curveBindingProperty = property.FindPropertyRelative(CurveBindingPropName);
            SerializedProperty curveProperty = property.FindPropertyRelative(CurvePropName);
            SerializedProperty objectReferenceCurveProperty = property.FindPropertyRelative(ObjectReferenceCurvePropName);

            // GenericAnimationのサマリー表示
            // 例えば、CurveBindingのPropertyNameを表示
            PropertyField propertyNameField = new PropertyField(curveBindingProperty.FindPropertyRelative(PropertyNamePropName));
            propertyNameField.label = ""; // ラベルを非表示にして、フィールド値のみ表示
            propertyNameField.style.flexGrow = 1; // スペースを埋める
            propertyNameField.style.minWidth = 50; // 最小幅を設定
            container.Add(propertyNameField);

            // 必要であれば、カーブとオブジェクト参照キーフレームの存在を示すアイコンなどを追加
            // 例: カーブが存在するかどうかを示すアイコン
            if (curveProperty != null && curveProperty.animationCurveValue.length > 0)
            {
                Label curveIcon = new Label("C"); // 'C' for Curve
                curveIcon.style.unityFontStyleAndWeight = FontStyle.Bold;
                curveIcon.style.color = Color.cyan;
                curveIcon.tooltip = "Has Animation Curve";
                curveIcon.style.width = 20; // アイコンの幅
                curveIcon.style.unityTextAlign = TextAnchor.MiddleCenter; // 中央揃え
                container.Add(curveIcon);
            }
            
            // オブジェクト参照キーフレームが存在するかどうかを示すアイコン
            if (objectReferenceCurveProperty != null && objectReferenceCurveProperty.arraySize > 0)
            {
                Label objRefIcon = new Label("O"); // 'O' for Object Reference
                objRefIcon.style.unityFontStyleAndWeight = FontStyle.Bold;
                objRefIcon.style.color = Color.magenta;
                objRefIcon.tooltip = "Has Object Reference Curve";
                objRefIcon.style.width = 20; // アイコンの幅
                objRefIcon.style.unityTextAlign = TextAnchor.MiddleCenter; // 中央揃え
                container.Add(objRefIcon);
            }

            return container;
        }
    }
} 