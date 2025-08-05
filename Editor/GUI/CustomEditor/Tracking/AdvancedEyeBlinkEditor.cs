using Aoyon.FaceTune.Gui.shapes_editor;

namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(AdvancedEyeBlinkComponent))]
internal class AdvancedEyeBlinkEditor : FaceTuneCustomEditorBase<AdvancedEyeBlinkComponent>
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Open Editor"))
        {
            var defaultOverride = new BlendShapeSet(Component.AdvancedEyeBlinkSettings.CancelerBlendShapeNames.Select(x => new BlendShape(x, 0.0f)));
            CustomEditorUtility.OpenEditor(Component.gameObject, new AdvancedEyeBlinkTargeting(){ Target = Component }, defaultOverride);
        }
        if (GUILayout.Button("Enable Eye Blink in All Child Expressions"))
        {
            EnableEyeBlinkInAllChildExpressions();
        }
    }

    private void EnableEyeBlinkInAllChildExpressions()
    {
        var expressions = Component.GetComponentsInChildren<ExpressionComponent>();
        if (expressions.Length == 0) return;

        Undo.RecordObjects(expressions, "Enable Eye Blink in All Child Expressions");
        foreach (var expression in expressions)
        {
            expression.FacialSettings = expression.FacialSettings with { AllowEyeBlink = TrackingPermission.Allow };
        }
        Debug.Log($"Enabled Eye Blink in {expressions.Length} child expressions");
    }
}