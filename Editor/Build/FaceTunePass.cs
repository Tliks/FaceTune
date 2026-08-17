using nadena.dev.ndmf;
using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Build;

internal class BuildEnabledState
{
    public bool Enabled { get; }

    private BuildEnabledState(bool enabled)
    {
        Enabled = enabled;
    }

    public static BuildEnabledState Create(BuildContext buildContext)
    {
        var root = buildContext.AvatarRootObject;
        var platformSupport = MetabasePlatformSupport.GetForBuild(buildContext);
        var canBuild = AvatarContext.TryGet(root, platformSupport, out _, out _);
        var anyComponents = root.GetComponentsInChildren<FaceTuneTagComponent>(true).Length > 0;
        return new BuildEnabledState(canBuild && anyComponents);
    }
}

internal class FaceTuneContext
{
    public BuildContext BuildContext { get; }
    public AvatarContext AvatarContext { get; private set; }
    public IMetabasePlatformSupport PlatformSupport { get; }

    private BuildSettings? Settings { get; set; }
    private AvatarControlSettings? AvatarControlSettingsState { get; set; }
    private ExpressionPlan? ExpressionPlan { get; set; }
    private ParameterPlan? ParameterPlan { get; set; }
    private MenuPlan? MenuPlan { get; set; }

    private FaceTuneContext(
        BuildContext buildContext,
        AvatarContext avatarContext,
        IMetabasePlatformSupport platformSupport)
    {
        BuildContext = buildContext;
        AvatarContext = avatarContext;
        PlatformSupport = platformSupport;
    }

    public static FaceTuneContext Create(BuildContext buildContext)
    {
        var root = buildContext.AvatarRootObject;
        var platformSupport = MetabasePlatformSupport.GetForBuild(buildContext);
        if (!AvatarContext.TryGet(root, platformSupport, out var avatarContext, out _))
        {
            throw new InvalidOperationException("FaceTuneContext cannot be created for this avatar.");
        }

        return new FaceTuneContext(buildContext, avatarContext, platformSupport);
    }

    public void SetSettings(BuildSettings settings)
    {
        Settings = settings;
    }

    public BuildSettings RequireSettings()
    {
        if (Settings is { } settings) return settings;
        throw new InvalidOperationException("BuildSettings has not been created.");
    }

    public void ReplaceFaceMesh(Mesh mesh)
    {
        AvatarContext = AvatarContext with { FaceMesh = mesh };
        if (Settings is { } settings)
        {
            Settings = settings with { AvatarContext = AvatarContext };
        }
    }

    public void SetAvatarControlSettings(AvatarControlSettings avatarControlSettings)
    {
        AvatarControlSettingsState = avatarControlSettings;
    }

    public AvatarControlSettings RequireAvatarControlSettings()
    {
        if (AvatarControlSettingsState is { } avatarControlSettings) return avatarControlSettings;
        throw new InvalidOperationException("AvatarControlSettings has not been created.");
    }

    public void SetExpressionPlan(ExpressionPlan expressionPlan)
    {
        ExpressionPlan = expressionPlan;
    }

    public ExpressionPlan RequireExpressionPlan()
    {
        return ExpressionPlan ?? throw new InvalidOperationException("ExpressionPlan has not been created.");
    }

    public void SetParameterPlan(ParameterPlan parameterPlan)
    {
        ParameterPlan = parameterPlan;
    }

    public ParameterPlan RequireParameterPlan()
    {
        return ParameterPlan ?? throw new InvalidOperationException("ParameterPlan has not been created.");
    }

    public void SetMenuPlan(MenuPlan menuPlan)
    {
        MenuPlan = menuPlan;
    }

    public MenuPlan RequireMenuPlan()
    {
        return MenuPlan ?? throw new InvalidOperationException("MenuPlan has not been created.");
    }
}

internal abstract class FaceTunePass<TPass> : Pass<TPass> where TPass : Pass<TPass>, new()
{
    protected sealed override void Execute(BuildContext context)
    {
        if (!context.GetState<BuildEnabledState>(BuildEnabledState.Create).Enabled) return;
        var faceTuneContext = context.GetState<FaceTuneContext>(FaceTuneContext.Create);
        using var _ = new Utils.ProfilingSampleScope(typeof(TPass).Name);
        Execute(faceTuneContext);
    }

    protected abstract void Execute(FaceTuneContext context);
}
