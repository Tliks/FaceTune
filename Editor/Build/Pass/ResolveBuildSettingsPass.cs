namespace Aoyon.FaceTune.Build;

internal class ResolveBuildSettingsPass : FaceTunePass<ResolveBuildSettingsPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.resolve-build-settings";
    public override string DisplayName => "Resolve Build Settings";

    protected override void Execute(FaceTuneContext context)
    {
        var authoring = context.RequireAuthoringSettings();
        var root = context.AvatarContext.Root;
        var compiler = new ConditionCompiler(context.PlatformSupport, authoring.ParameterDomains);

        var controls = root.GetComponentsInChildren<AvatarControlComponent>(true);
        var mmdSupport = FindSingle(controls, AvatarControlComponent.Kind.SupportMMD);
        var eyeBlink = FindSingle(controls, AvatarControlComponent.Kind.DisableEyeBlink);
        var lipSync = FindSingle(controls, AvatarControlComponent.Kind.DisableLipSync);
        var lockFacial = FindSingle(controls, AvatarControlComponent.Kind.LockFacial);

        context.SetSettings(new BuildSettings(
            authoring.AvatarContext,
            authoring.ExcludedBlendShapeNames,
            authoring.AvoidEyeBlinkConflicts,
            authoring.AvoidLipSyncConflicts,
            authoring.ParameterDomains,
            context.PlatformSupport.ResolveMmdPlaybackSettings(mmdSupport?.MMD, compiler.Resolve(mmdSupport?.Condition.Condition)),
            compiler.Resolve(eyeBlink?.Condition.Condition),
            compiler.Resolve(lipSync?.Condition.Condition),
            compiler.Resolve(lockFacial?.Condition.Condition)));
    }

    private static AvatarControlComponent? FindSingle(
        IEnumerable<AvatarControlComponent> controls,
        AvatarControlComponent.Kind kind)
    {
        var matches = controls.Where(control => control.ControlKind == kind).ToArray();
        if (matches.Length > 1) Debug.LogWarning($"Multiple FaceTune Avatar Controls of kind '{kind}' were found. Using the first.", matches[0]);
        return matches.FirstOrDefault();
    }
}
