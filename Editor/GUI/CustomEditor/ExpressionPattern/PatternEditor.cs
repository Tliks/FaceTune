namespace com.aoyon.facetune.ui;

[CanEditMultipleObjects]
[CustomEditor(typeof(PatternComponent))]
internal class PatternEditor : FaceTuneCustomEditorBase<PatternComponent>
{
    private const string Description = "このコンポーネントはアタッチされたGameObject以下のExpressionを同じ優先度と扱い排他制御にします。\n" +
        "なお各Expressionに紐づいたConditionが排他ではない場合、どのExpressionが適用されるかは不定です。\n" +
        "このコンポーネントを使用した上で排他のConditionを設定することを推奨します。";

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        EditorGUILayout.HelpBox(Description, MessageType.Info);
    }
}
