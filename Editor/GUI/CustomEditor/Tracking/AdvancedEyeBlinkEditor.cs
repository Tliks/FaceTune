using Aoyon.FaceTune.Gui.ShapesEditor;

namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(AdvancedEyeBlinkComponent))]
internal class AdvancedEyeBlinkEditor : FaceTuneIMGUIEditorBase<AdvancedEyeBlinkComponent>
{
    protected override void OnInnerInspectorGUI()
    {
        DrawDefaultInspector(false);

        if (GUILayout.Button("Open Editor"))
        {
            var defaultOverride = new BlendShapeWeightSet(Component.AdvancedEyeBlinkSettings.CancelerBlendShapeNames.Select(x => new BlendShapeWeight(x, 0.0f)));
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