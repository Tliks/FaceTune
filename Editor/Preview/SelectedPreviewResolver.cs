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
                isLooping);
            avatars.Add(new AvatarPreviewData(
                avatar.Root,
                avatar.FaceRenderer,
                null,
                ignoredNames,
                facial,
                TrackingPermission.Keep,
                TrackingPermission.Keep,
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
        FaceTuneTagComponent? source = GetFirst<ExpressionComponent>(gameObject, context);
        source ??= GetFirst<SettingsComponent>(gameObject, context);
        source ??= GetFirst<ExpressionDataComponent>(gameObject, context);
        return source == null ? null : ResolveComponent(source, preview, context);
    }

    private static SelectedPreviewData? ResolveComponent(
        FaceTuneTagComponent source,
        DirectBlendShapePreviewLayer preview,
        ComputeContext context)
    {
        var avatar = preview.GetTargets(context)
            .FirstOrDefault(value => source.transform.IsChildOf(value.Root.transform));
        if (avatar == null) return null;

        var ignoredNames = AvatarContext.GetExplicitlyExcludedBlendShapeNames(
            avatar.Root,
            context);
        AvatarPreviewData? resolved = source switch
        {
            ExpressionComponent expression => ResolveExpression(
                expression,
                avatar,
                ignoredNames,
                context),
            SettingsComponent settings => ResolveSettings(
                settings,
                avatar,
                ignoredNames,
                context),
            ExpressionDataComponent data => ResolveData(
                data,
                avatar,
                ignoredNames,
                context),
            _ => null
        };
        return resolved == null ? null : new SelectedPreviewData(new[] { resolved });
    }

    private static T? GetFirst<T>(GameObject gameObject, ComputeContext context)
        where T : Component
    {
        using var _ = ListPool<T>.Get(out var components);
        context.GetComponents<T>(gameObject, components);
        return components.FirstOrDefault();
    }

    private static AvatarPreviewData ResolveExpression(
        ExpressionComponent expression,
        AvatarContext avatar,
        ImmutableHashSet<string> ignoredNames,
        ComputeContext context)
    {
        var facialResolver = new FacialAnimationResolver(avatar.Root, context);
        var animations = facialResolver.ResolveIncoming(expression.transform);
        if (facialResolver.TryResolve(expression, out var expressionAnimations))
            animations.AddRange(expressionAnimations);

        var multiFrame = new MultiFrameResolver(context).Resolve(expression);
        var facial = new FacialPreviewData(
            animations,
            0f,
            multiFrame.MultiFrameMode == MultiFrameSettings.Kind.Loop);
        var behavior = new ExpressionBehaviorResolver(context).Resolve(expression);
        var eyeBlinkSettings = new EyeBlinkResolver(avatar.Root, context).Resolve(expression);
        var lipSyncSettings = new LipSyncResolver(avatar.Root, context).Resolve(expression);
        return new AvatarPreviewData(
            avatar.Root,
            avatar.FaceRenderer,
            expression,
            ignoredNames,
            facial,
            behavior.AllowEyeBlink,
            behavior.AllowLipSync,
            CreateEyeBlink(eyeBlinkSettings, avatar),
            CreateLipSync(lipSyncSettings, avatar));
    }

    private static AvatarPreviewData? ResolveSettings(
        SettingsComponent settings,
        AvatarContext avatar,
        ImmutableHashSet<string> ignoredNames,
        ComputeContext context)
    {
        var facialResolver = new FacialAnimationResolver(avatar.Root, context);
        FacialPreviewData? facial = null;
        if (facialResolver.TryResolve(settings, out var animations))
            facial = new FacialPreviewData(animations, null, false);

        var eyeBlinkResolver = new EyeBlinkResolver(avatar.Root, context);
        var eyeBlinkSettings = eyeBlinkResolver.ResolveProvider(settings);
        var lipSyncResolver = new LipSyncResolver(avatar.Root, context);
        var lipSyncSettings = lipSyncResolver.ResolveProvider(settings);
        var eyeBlink = eyeBlinkSettings == null
            ? null
            : CreateEyeBlink(eyeBlinkSettings, avatar);
        var lipSync = lipSyncSettings == null
            ? null
            : CreateLipSync(lipSyncSettings, avatar);
        if (facial == null && eyeBlink == null && lipSync == null) return null;

        return new AvatarPreviewData(
            avatar.Root,
            avatar.FaceRenderer,
            settings,
            ignoredNames,
            facial,
            TrackingPermission.Keep,
            TrackingPermission.Keep,
            eyeBlink,
            lipSync);
    }

    private static AvatarPreviewData ResolveData(
        ExpressionDataComponent data,
        AvatarContext avatar,
        ImmutableHashSet<string> ignoredNames,
        ComputeContext context)
    {
        var facialResolver = new FacialAnimationResolver(avatar.Root, context);
        var animations = facialResolver.ResolveIncoming(data.transform);
        var hasFacial = facialResolver.TryResolve(data, out var dataAnimations);
        if (hasFacial) animations.AddRange(dataAnimations);

        var multiFrameResolver = new MultiFrameResolver(context);
        var multiFrame = multiFrameResolver.ResolveProvider(data) ?? new MultiFrameSettings();
        var facial = hasFacial || animations.Count > 0
            ? new FacialPreviewData(
                animations,
                0f,
                multiFrame.MultiFrameMode == MultiFrameSettings.Kind.Loop)
            : null;

        var eyeBlinkResolver = new EyeBlinkResolver(avatar.Root, context);
        var eyeBlinkSettings = eyeBlinkResolver.ResolveProvider(data);
        eyeBlinkSettings ??= eyeBlinkResolver.ResolveIncoming(data).Value;
        var lipSyncResolver = new LipSyncResolver(avatar.Root, context);
        var lipSyncSettings = lipSyncResolver.ResolveProvider(data);
        lipSyncSettings ??= lipSyncResolver.ResolveIncoming(data).Value;
        return new AvatarPreviewData(
            avatar.Root,
            avatar.FaceRenderer,
            data,
            ignoredNames,
            facial,
            data.HasFacialBehavior ? data.AllowEyeBlink : TrackingPermission.Keep,
            data.HasFacialBehavior ? data.AllowLipSync : TrackingPermission.Keep,
            CreateEyeBlink(eyeBlinkSettings, avatar),
            CreateLipSync(lipSyncSettings, avatar));
    }

    private static EyeBlinkPreviewData? CreateEyeBlink(
        EyeBlinkSettings settings,
        AvatarContext avatar)
    {
        return settings.EyeBlinkMode switch
        {
            EyeBlinkSettings.Kind.SimpleAnimation => EyeBlinkPreviewData.FromSimple(settings),
            EyeBlinkSettings.Kind.CustomAnimation => EyeBlinkPreviewData.FromAnimation(settings.Animations),
            EyeBlinkSettings.Kind.BuiltIn => ResolveBuiltInEyeBlink(avatar),
            _ => throw new ArgumentOutOfRangeException()
        };
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

    private static LipSyncPreviewData? CreateLipSync(
        LipSyncSettings settings,
        AvatarContext avatar)
    {
        var shapes = settings.Mode switch
        {
            LipSyncSettings.Kind.Custom => settings.Shapes,
            LipSyncSettings.Kind.BuiltIn => GetBuiltInLipSyncShapes(avatar),
            _ => throw new ArgumentOutOfRangeException()
        };
        return shapes == null
            ? null
            : new LipSyncPreviewData(settings.CancellerBlendShapes, shapes);
    }

    private static VrcVisemeLipSyncShapes? GetBuiltInLipSyncShapes(AvatarContext avatar)
    {
        var supports = MetabasePlatformSupport.GetForAvatar(avatar.Root.transform);
        return supports
            .Select(support => support.GetBuiltInLipSyncShapes(avatar.FaceRenderer))
            .FirstOrDefault(value => value != null);
    }
}
