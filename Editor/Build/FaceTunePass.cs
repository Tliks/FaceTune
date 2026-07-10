using nadena.dev.ndmf;
using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Build;

internal class BuildPassState
{
    public bool Enabled { get; }

    private BuildPassState(bool enabled)
    {
        Enabled = enabled;
    }

    public static BuildPassState Create(BuildContext buildContext)
    {
        var root = buildContext.AvatarRootObject;
        var platformSupport = MetabasePlatformSupport.GetSupport(buildContext);
        var canBuild = AvatarContext.TryGet(root, platformSupport, out _, out _);
        var anyComponents = root.GetComponentsInChildren<FaceTuneTagComponent>(true).Length > 0;
        return new BuildPassState(canBuild && anyComponents);
    }
}

internal class FaceTuneContext
{
    public BuildContext BuildContext { get; }
    public AvatarContext AvatarContext { get; }
    public IMetabasePlatformSupport PlatformSupport { get; }

    private BuildSettings? Settings { get; set; }
    private ExpressionProgram? ExpressionProgram { get; set; }
    private MenuProgram? MenuProgram { get; set; }

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
        var platformSupport = MetabasePlatformSupport.GetSupport(buildContext);
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
        throw new InvalidOperationException("BuildSettings has not been collected.");
    }

    public void SetExpressionProgram(ExpressionProgram expressionProgram)
    {
        ExpressionProgram = expressionProgram;
    }

    public ExpressionProgram RequireExpressionProgram()
    {
        return ExpressionProgram ?? throw new InvalidOperationException("ExpressionProgram has not been compiled.");
    }

    public void SetMenuProgram(MenuProgram menuProgram)
    {
        MenuProgram = menuProgram;
    }

    public MenuProgram RequireMenuProgram()
    {
        return MenuProgram ?? throw new InvalidOperationException("MenuProgram has not been compiled.");
    }
}

internal abstract class FaceTunePass<TPass> : Pass<TPass> where TPass : Pass<TPass>, new()
{
    protected sealed override void Execute(BuildContext context)
    {
        if (!context.GetState<BuildPassState>(BuildPassState.Create).Enabled) return;
        var faceTuneContext = context.GetState<FaceTuneContext>(FaceTuneContext.Create);
        using var _ = new Utils.ProfilingSampleScope(typeof(TPass).Name);
        Execute(faceTuneContext);
    }

    protected abstract void Execute(FaceTuneContext context);
}
