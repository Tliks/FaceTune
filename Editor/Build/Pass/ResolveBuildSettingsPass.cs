namespace Aoyon.FaceTune.Build;

internal class ResolveBuildSettingsPass : FaceTunePass<ResolveBuildSettingsPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.resolve-build-settings";
    public override string DisplayName => "Resolve Build Settings";

    protected override void Execute(FaceTuneContext context)
    {
        var authoring = context.RequireAuthoringSettings();
        var root = context.AvatarContext.Root;
        var compiler = new ConditionCompiler(root, context.PlatformSupport, authoring.ParameterDomains);

        var mmdSupport = FindSingle<MMDSupportComponent>(root, "Log:warning:CollectBuildSettingsPass:MultipleMMDSupportComponent").DestroyedAsNull();
        var eyeBlink = FindSingle<DisableEyeBlinkComponent>(root, "Log:warning:CollectBuildSettingsPass:MultipleDisableEyeBlinkComponent").DestroyedAsNull();
        var lipSync = FindSingle<DisableLipSyncComponent>(root, "Log:warning:CollectBuildSettingsPass:MultipleDisableLipSyncComponent").DestroyedAsNull();
        var lockFacial = FindSingle<LockFacialComponent>(root, "Log:warning:CollectBuildSettingsPass:MultipleLockFacialComponent").DestroyedAsNull();

        context.SetSettings(new BuildSettings(
            authoring.AvatarContext,
            authoring.ExcludedBlendShapeNames,
            authoring.AvoidEyeBlinkConflicts,
            authoring.AvoidLipSyncConflicts,
            authoring.ParameterDomains,
            context.PlatformSupport.ResolveMmdPlaybackSettings(compiler.Resolve(mmdSupport?.DisableWhen)),
            compiler.Resolve(eyeBlink?.DisableWhen),
            compiler.Resolve(lipSync?.DisableWhen),
            compiler.Resolve(lockFacial?.LockWhen)));
    }

    private static T? FindSingle<T>(GameObject root, string warningKey) where T : Component
    {
        var components = root.GetComponentsInChildren<T>(true);
        if (components.Length > 1) LocalizedLog.Warning(warningKey, null, components);
        return components.FirstOrDefault();
    }
}
