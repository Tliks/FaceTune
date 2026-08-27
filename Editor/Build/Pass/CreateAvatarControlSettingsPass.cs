namespace Aoyon.FaceTune.Build;

internal sealed class CreateAvatarControlSettingsPass : FaceTunePass<CreateAvatarControlSettingsPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.create-avatar-control-settings";
    public override string DisplayName => "Create Avatar Control Settings";

    protected override void Execute(FaceTuneContext context)
    {
        var settings = context.RequireSettings();
        var root = context.AvatarContext.Root;
        var conditionResolver = new ConditionResolver(root, context.PlatformSupport, settings.ParameterDomains);

        var controls = root.GetComponentsInChildren<AvatarControlComponent>(true);
        var mmdSupport = FindSingle(controls, AvatarControlComponent.Kind.SupportMMD);
        var eyeBlink = FindSingle(controls, AvatarControlComponent.Kind.DisableEyeBlink);
        var lipSync = FindSingle(controls, AvatarControlComponent.Kind.DisableLipSync);
        var lockFacial = FindSingle(controls, AvatarControlComponent.Kind.LockFacial);

        var mmdPlayback = mmdSupport == null
            ? MmdPlaybackSettings.Disabled
            : new MmdPlaybackSettings(
                true,
                mmdSupport.MMD.ExplicitBlendShapeNames.ToArray(),
                mmdSupport.Condition,
                mmdSupport.MMD.SupportMode);
        context.SetAvatarControlSettings(new AvatarControlSettings(
            mmdPlayback,
            conditionResolver.Resolve(eyeBlink?.Condition),
            conditionResolver.Resolve(lipSync?.Condition),
            conditionResolver.Resolve(lockFacial?.Condition)));
    }

    private static AvatarControlComponent? FindSingle(
        IEnumerable<AvatarControlComponent> controls,
        AvatarControlComponent.Kind kind)
    {
        var matches = controls.Where(control => control.ControlKind == kind).ToArray();
        if (matches.Length > 1)
        {
            Debug.LogWarning(
                $"Multiple FaceTune Avatar Controls of kind '{kind}' were found. Using the first.",
                matches[0]);
        }
        return matches.FirstOrDefault();
    }
}
