namespace aoyon.facetune.gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(PatternComponent))]
internal class PatternEditor : FaceTuneCustomEditorBase<PatternComponent>
{
    private const string Description = "このコンポーネントはアタッチされたGameObject以下のExpressionを排他制御にします。\n" +
        "各Expressionに紐づいたConditionが排他ではない場合、Hierarchy下のExpressionが優先されます。\n" +
        "このコンポーネントを使用した上で排他のConditionを設定することを推奨します。";

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        EditorGUILayout.HelpBox(Description, MessageType.Info);
    }
}
