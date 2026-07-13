namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(EyeBlinkComponent))]
internal sealed class AdvancedEyeBlinkEditor : FaceTuneEditor<EyeBlinkComponent>
{
}

[CanEditMultipleObjects]
[CustomEditor(typeof(LipSyncComponent))]
internal sealed class AdvancedLipSyncEditor : FaceTuneEditor<LipSyncComponent>
{
}

[CanEditMultipleObjects]
[CustomEditor(typeof(ConditionComponent))]
internal sealed class ConditionEditor : FaceTuneEditor<ConditionComponent>
{
}
