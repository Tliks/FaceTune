using Aoyon.FaceTune.Platforms;
using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune.Preview;

internal static class SelectedPreviewResolver
{
    internal static SelectedPreviewData? Resolve(
        Object selection,
        DirectBlendShapePreviewLayer preview,
        ComputeContext context)
    {
        return selection switch
        {
            AnimationClip clip => ResolveClip(clip, preview, context),
            GameObject gameObject => ResolveGameObject(gameObject, preview, context),
            _ => null
        };
    }

    private static SelectedPreviewData ResolveClip(
        AnimationClip clip,
        DirectBlendShapePreviewLayer preview,
        ComputeContext context)
    {
        var isLooping = context.Observe(
            clip,
            value => value.isLooping,
            (left, right) => left == right);
        var ignoredNames = ImmutableHashSet.Create<string>(StringComparer.Ordinal);
        var avatars = new List<AvatarPreviewData>();
        foreach (var avatar in preview.GetTargets(context))
        {
            var animations = new List<BlendShapeWeightAnimation>();
            clip.GetBlendShapeAnimations(
                ClipImportOption.NonZero,
                animations,
                avatar.BodyPath);
            var facial = new FacialPreviewData(
                animations,
                null,
                ignoredNames,
                isLooping);
            avatars.Add(new AvatarPreviewData(
                avatar.Root,
                avatar.FaceRenderer,
                null,
                facial,
                null,
                null));
        }
        return new SelectedPreviewData(avatars);
    }

    private static SelectedPreviewData? ResolveGameObject(
        GameObject gameObject,
        DirectBlendShapePreviewLayer preview,
        ComputeContext context)
    {
        var avatar = preview.GetTargets(context)
            .FirstOrDefault(value => gameObject.transform.IsChildOf(value.Root.transform));
        if (avatar == null) return null;

        using var _ = ListPool<ExpressionComponent>.Get(out var expressions);
        context.GetComponents<ExpressionComponent>(gameObject, expressions);
        if (expressions.Count > 1) return null;

        var animations = new List<BlendShapeWeightAnimation>();
        ExpressionComponent? expression = null;
        var isLooping = false;
        if (expressions.Count == 1)
        {
            expression = expressions[0];
            ResolveExpressionAnimations(expression, avatar.Root, context, animations);
            var multiFrame = new MultiFrameResolver(context).Resolve(expression);
            isLooping = multiFrame.MultiFrameMode == MultiFrameSettings.Kind.Loop;
        }
        else if (!ResolveDataAnimations(gameObject, avatar.Root, context, animations))
        {
            return null;
        }

        var ignoredNames = AvatarContext.GetExplicitlyExcludedBlendShapeNames(
            avatar.Root,
            context);
        var facial = new FacialPreviewData(
            animations,
            0f,
            ignoredNames,
            isLooping);
        var eyeBlink = expression == null
            ? null
            : ResolveEyeBlink(expression, avatar, context);
        var lipSync = expression == null
            ? null
            : ResolveLipSync(expression, avatar, context);
        var resolvedAvatar = new AvatarPreviewData(
            avatar.Root,
            avatar.FaceRenderer,
            expression,
            facial,
            eyeBlink,
            lipSync);
        return new SelectedPreviewData(new[] { resolvedAvatar });
    }

    private static void ResolveExpressionAnimations(
        ExpressionComponent expression,
        GameObject root,
        ComputeContext context,
        ICollection<BlendShapeWeightAnimation> result)
    {
        var facial = new FacialAnimationResolver(root, context);
        foreach (var animation in facial.ResolveIncoming(expression.transform))
            result.Add(animation);
        if (!facial.TryResolve(expression, out var definition)) return;
        foreach (var animation in definition)
            result.Add(animation);
    }

    private static bool ResolveDataAnimations(
        GameObject gameObject,
        GameObject root,
        ComputeContext context,
        ICollection<BlendShapeWeightAnimation> result)
    {
        using var _expressions = ListPool<ExpressionComponent>.Get(out var childExpressions);
        context.GetComponentsInChildren<ExpressionComponent>(gameObject, true, childExpressions);
        if (childExpressions.Count > 0) return false;

        using var _data = ListPool<ExpressionDataComponent>.Get(out var dataComponents);
        context.GetComponentsInChildren<ExpressionDataComponent>(gameObject, true, dataComponents);
        if (dataComponents.Count == 0) return false;

        var facial = new FacialAnimationResolver(root, context);
        var resolved = new BlendShapeWeightAnimationSet();
        foreach (var data in dataComponents)
        {
            if (facial.TryResolve(data, out var value))
                resolved.AddRange(value);
        }
        if (resolved.Count == 0) return false;

        foreach (var animation in facial.ResolveIncoming(gameObject.transform))
            result.Add(animation);
        foreach (var animation in resolved)
            result.Add(animation);
        return true;
    }

    private static EyeBlinkPreviewData? ResolveEyeBlink(
        ExpressionComponent expression,
        AvatarContext avatar,
        ComputeContext context)
    {
        var settings = new EyeBlinkResolver(avatar.Root, context).Resolve(expression);
        switch (settings.EyeBlinkMode)
        {
            case EyeBlinkSettings.Kind.SimpleAnimation:
                return EyeBlinkPreviewData.FromSimple(settings);
            case EyeBlinkSettings.Kind.CustomAnimation:
                return EyeBlinkPreviewData.FromAnimation(settings.Animations);
            case EyeBlinkSettings.Kind.BuiltIn:
                return ResolveBuiltInEyeBlink(avatar);
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static EyeBlinkPreviewData? ResolveBuiltInEyeBlink(AvatarContext avatar)
    {
        var supports = MetabasePlatformSupport.GetForAvatar(avatar.Root.transform);
        var animations = supports
            .Select(support => support.GetBuiltInEyeBlinkAnimations(avatar.FaceRenderer))
            .FirstOrDefault(value => value != null);
        if (animations == null) return null;
        return EyeBlinkPreviewData.FromAnimation(animations, new Vector2(0.5f, 0.5f));
    }

    private static LipSyncPreviewData? ResolveLipSync(
        ExpressionComponent expression,
        AvatarContext avatar,
        ComputeContext context)
    {
        var settings = new LipSyncResolver(avatar.Root, context).Resolve(expression);
        VrcVisemeLipSyncShapes? shapes;
        switch (settings.Mode)
        {
            case LipSyncSettings.Kind.Custom:
                shapes = settings.Shapes;
                break;
            case LipSyncSettings.Kind.BuiltIn:
                shapes = GetBuiltInLipSyncShapes(avatar);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        if (shapes == null) return null;
        return new LipSyncPreviewData(settings.CancellerBlendShapes, shapes);
    }

    private static VrcVisemeLipSyncShapes? GetBuiltInLipSyncShapes(AvatarContext avatar)
    {
        var supports = MetabasePlatformSupport.GetForAvatar(avatar.Root.transform);
        return supports
            .Select(support => support.GetBuiltInLipSyncShapes(avatar.FaceRenderer))
            .FirstOrDefault(value => value != null);
    }
}
