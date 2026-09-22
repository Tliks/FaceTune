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
            if (animations.Count == 0) continue;

            var facial = new FacialPreviewData(
                animations,
                null,
                isLooping);
            avatars.Add(new AvatarPreviewData(
                avatar.Root,
                avatar.FaceRenderer,
                clip,
                ignoredNames,
                facial,
                TrackingBehaviorDisplay.NotApplicable,
                TrackingSettingDisplay.Hidden,
                TrackingBehaviorDisplay.NotApplicable,
                TrackingSettingDisplay.Hidden,
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
        var targets = preview.GetTargets(context);
        IEnumerable<AvatarContext> avatars = EditorUtility.IsPersistent(source)
            ? targets
            : targets
                .Where(value => source.transform.IsChildOf(value.Root.transform))
                .Take(1);
        var resolved = new List<AvatarPreviewData>();
        foreach (var avatar in avatars)
        {
            var ignoredNames = AvatarContext.GetExplicitlyExcludedBlendShapeNames(
                avatar.Root,
                context);
            AvatarPreviewData? data = source switch
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
                ExpressionDataComponent expressionData => ResolveData(
                    expressionData,
                    avatar,
                    ignoredNames,
                    context),
                _ => null
            };
            if (data != null) resolved.Add(data);
        }
        return resolved.Count == 0 ? null : new SelectedPreviewData(resolved);
    }

    private static T? GetFirst<T>(GameObject gameObject, ComputeContext context)
        where T : Component
    {
        using var _ = ListPool<T>.Get(out var components);
        context.GetComponents<T>(gameObject, components);
        return components.FirstOrDefault();
    }

    internal static FacialPreviewData? ResolveFacial(
        FaceTuneTagComponent source,
        AvatarContext avatar,
        ComputeContext context)
    {
        var resolver = new FacialAnimationResolver(avatar.Root, context);
        switch (source)
        {
            case ExpressionComponent expression:
            {
                var animations = resolver.ResolveIncoming(expression.transform);
                if (resolver.TryResolve(expression, out var local)) animations.AddRange(local);
                var multiFrame = new MultiFrameResolver(context).Resolve(expression);
                return new FacialPreviewData(
                    animations,
                    0f,
                    multiFrame.MultiFrameMode == MultiFrameSettings.Kind.Loop);
            }
            case SettingsComponent settings:
            {
                return resolver.TryResolve(settings, out var animations)
                    ? new FacialPreviewData(animations, 0f, false)
                    : null;
            }
            case ExpressionDataComponent data:
            {
                var animations = resolver.ResolveIncoming(data.transform);
                var hasFacial = resolver.TryResolve(data, out var local);
                if (hasFacial) animations.AddRange(local);
                if (!hasFacial && animations.Count == 0) return null;
                var multiFrame = new MultiFrameResolver(context).ResolveProvider(data)
                                 ?? new MultiFrameSettings();
                return new FacialPreviewData(
                    animations,
                    0f,
                    multiFrame.MultiFrameMode == MultiFrameSettings.Kind.Loop);
            }
            default:
                return null;
        }
    }

    private static AvatarPreviewData ResolveExpression(
        ExpressionComponent expression,
        AvatarContext avatar,
        ImmutableHashSet<string> ignoredNames,
        ComputeContext context)
    {
        var facial = ResolveFacial(expression, avatar, context)!;
        var behavior = new ExpressionBehaviorResolver(context).Resolve(expression);
        var eyeBlinkSettings = new EyeBlinkResolver(avatar.Root, context).Resolve(expression);
        var lipSyncSettings = new LipSyncResolver(avatar.Root, context).Resolve(expression);
        return new AvatarPreviewData(
            avatar.Root,
            avatar.FaceRenderer,
            expression,
            ignoredNames,
            facial,
            ToDisplay(behavior.AllowEyeBlink),
            TrackingSettingDisplay.Defined,
            ToDisplay(behavior.AllowLipSync),
            TrackingSettingDisplay.Defined,
            CreateEyeBlink(eyeBlinkSettings, avatar),
            CreateLipSync(lipSyncSettings, avatar));
    }

    private static AvatarPreviewData? ResolveSettings(
        SettingsComponent settings,
        AvatarContext avatar,
        ImmutableHashSet<string> ignoredNames,
        ComputeContext context)
    {
        var facial = ResolveFacial(settings, avatar, context);

        var enabled = context.Observe(
            settings,
            value => (value.HasEyeBlink, value.HasLipSync),
            (left, right) => left == right);
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
        if (facial == null && !enabled.HasEyeBlink && !enabled.HasLipSync) return null;

        return new AvatarPreviewData(
            avatar.Root,
            avatar.FaceRenderer,
            settings,
            ignoredNames,
            facial,
            TrackingBehaviorDisplay.NotApplicable,
            enabled.HasEyeBlink
                ? TrackingSettingDisplay.Defined
                : TrackingSettingDisplay.Hidden,
            TrackingBehaviorDisplay.NotApplicable,
            enabled.HasLipSync
                ? TrackingSettingDisplay.Defined
                : TrackingSettingDisplay.Hidden,
            eyeBlink,
            lipSync);
    }

    private static AvatarPreviewData ResolveData(
        ExpressionDataComponent data,
        AvatarContext avatar,
        ImmutableHashSet<string> ignoredNames,
        ComputeContext context)
    {
        var facial = ResolveFacial(data, avatar, context);

        var options = context.Observe(
            data,
            value => (
                value.HasFacialBehavior,
                value.AllowEyeBlink,
                value.AllowLipSync,
                value.HasEyeBlink,
                value.HasLipSync),
            (left, right) => left == right);
        var eyeBlinkResolver = new EyeBlinkResolver(avatar.Root, context);
        var eyeBlinkSettings = eyeBlinkResolver.ResolveProvider(data);
        eyeBlinkSettings ??= eyeBlinkResolver.ResolveIncoming(data).Value;
        var eyeBlink = CreateEyeBlink(eyeBlinkSettings, avatar);
        var lipSyncResolver = new LipSyncResolver(avatar.Root, context);
        var lipSyncSettings = lipSyncResolver.ResolveProvider(data);
        lipSyncSettings ??= lipSyncResolver.ResolveIncoming(data).Value;
        var lipSync = CreateLipSync(lipSyncSettings, avatar);
        return new AvatarPreviewData(
            avatar.Root,
            avatar.FaceRenderer,
            data,
            ignoredNames,
            facial,
            options.HasFacialBehavior
                ? ToDisplay(options.AllowEyeBlink)
                : TrackingBehaviorDisplay.Unset,
            options.HasEyeBlink
                ? TrackingSettingDisplay.Defined
                : TrackingSettingDisplay.Estimated,
            options.HasFacialBehavior
                ? ToDisplay(options.AllowLipSync)
                : TrackingBehaviorDisplay.Unset,
            options.HasLipSync
                ? TrackingSettingDisplay.Defined
                : TrackingSettingDisplay.Estimated,
            eyeBlink,
            lipSync);
    }

    private static TrackingBehaviorDisplay ToDisplay(TrackingPermission permission)
        => permission switch
        {
            TrackingPermission.Allow => TrackingBehaviorDisplay.Enabled,
            TrackingPermission.Disallow => TrackingBehaviorDisplay.Disabled,
            TrackingPermission.Keep => TrackingBehaviorDisplay.Keep,
            _ => throw new ArgumentOutOfRangeException(nameof(permission), permission, null)
        };

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
        var supports = MetaversePlatformSupport.GetForAvatar(avatar.Root.transform);
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
        var supports = MetaversePlatformSupport.GetForAvatar(avatar.Root.transform);
        return supports
            .Select(support => support.GetBuiltInLipSyncShapes(avatar.FaceRenderer))
            .FirstOrDefault(value => value != null);
    }
}
