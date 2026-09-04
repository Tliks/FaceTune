#pragma warning disable CS0618

using System.IO;
using nadena.dev.modular_avatar.core;

namespace Aoyon.FaceTune.Importers.Legacy;

internal sealed class LegacyFaceTuneImporter
{
    private readonly AvatarContext _context;
    private readonly GameObject _sourceRoot;
    private readonly Dictionary<Transform, GameObject> _scopeObjects = new();
    private readonly Dictionary<(AnimationClip Clip, bool AllBlendShapesAsFacial), AnimationClip?> _nonFacialClips = new();
    private readonly IReadOnlyList<LegacyExpressionComponent> _sourceExpressions;
    private readonly HashSet<Transform> _scopeTransforms;
    private readonly HashSet<LegacyPresetComponent> _defaultPresets;
    private readonly Transform? _templateScope;
    private GameObject _destination = null!;
    private string _outputFolder = string.Empty;

    public LegacyFaceTuneImporter(AvatarContext context)
    {
        _context = context;
        _sourceRoot = context.Root;
        _sourceExpressions = _sourceRoot.GetComponentsInChildren<LegacyExpressionComponent>(true);
        _scopeTransforms = CollectScopeTransforms();
        _defaultPresets = CollectPresets()
            .Where(preset => HasExpressionUnder(preset.transform))
            .Take(1)
            .ToHashSet();
        _templateScope = FindTemplateScope();
    }

    public GameObject Import(GameObject destination)
    {
        _destination = destination;
        _outputFolder = $"Assets/FaceTune/Legacy Import/{SanitizeFileName(_sourceRoot.name)}";

        if (_templateScope.DestroyedAsNull() is { } templateScope)
        {
            _scopeObjects[templateScope] = destination;
            ApplyScopeSettings(templateScope, destination);
            CopyEditorOnlyTag(templateScope, destination);
        }

        ImportOverrideFaceRenderer();
        foreach (var sourceTransform in _sourceRoot.GetComponentsInChildren<Transform>(true))
        {
            if (_scopeTransforms.Contains(sourceTransform))
                GetOrCreateScope(sourceTransform);

            if (sourceTransform.TryGetComponent<LegacyExpressionComponent>(out var expression))
                ImportExpression(expression);
        }

        AssetDatabase.SaveAssets();
        return destination;
    }

    private static void CopyEditorOnlyTag(Transform source, GameObject target)
    {
        if (source.CompareTag("EditorOnly")) target.tag = "EditorOnly";
    }

    private HashSet<Transform> CollectScopeTransforms()
    {
        var result = new HashSet<Transform>();
        foreach (var transform in _sourceRoot.GetComponentsInChildren<Transform>(true))
        {
            var hasExpression = transform.TryGetComponent<LegacyExpressionComponent>(out _);
            var hasScopeComponent = transform.GetComponents<LegacyFaceTuneTagComponent>()
                .Any(component => component switch
                {
                    LegacyPatternComponent => true,
                    LegacyPresetComponent => true,
                    LegacyFacialStyleComponent => true,
                    LegacyAdvancedEyeBlinkComponent => true,
                    LegacyAdvancedLipSyncComponent => true,
                    LegacyConditionComponent when !hasExpression => true,
                    _ => false
                });
            if (!hasScopeComponent) continue;

            var appliesToRenderer = transform.TryGetComponent<LegacyFacialStyleComponent>(out var style)
                && style.ApplyToRenderer;
            if (HasExpressionUnder(transform) || appliesToRenderer)
                result.Add(transform);
        }

        return result;
    }

    private IEnumerable<LegacyPresetComponent> CollectPresets()
        => _sourceRoot.GetComponentsInChildren<LegacyPresetComponent>(true);

    private Transform? FindTemplateScope()
    {
        var rootScopes = _scopeTransforms
            .Where(scope => FindParentScope(scope.parent) == null)
            .ToArray();
        var rendererScopes = rootScopes
            .Where(scope => scope.TryGetComponent<LegacyFacialStyleComponent>(out var style)
                            && style.ApplyToRenderer)
            .ToArray();
        return rendererScopes.Length == 1
            ? rendererScopes[0]
            : rootScopes.Length == 1
                ? rootScopes[0]
                : null;
    }

    private Transform? FindParentScope(Transform? source)
    {
        var current = source.DestroyedAsNull();
        while (current is not null)
        {
            if (_scopeTransforms.Contains(current)) return current;
            if (current == _sourceRoot.transform) break;
            current = current.parent.DestroyedAsNull();
        }
        return null;
    }

    private bool HasExpressionUnder(Transform parent)
        => _sourceExpressions.Any(expression => expression.transform == parent
                                                 || expression.transform.IsChildOf(parent));

    private GameObject GetOrCreateScope(Transform source)
    {
        if (_scopeObjects.TryGetValue(source, out var existing))
            return existing;

        var parentSource = FindParentScope(source.parent);
        var parent = parentSource.DestroyedAsNull() is { } scope
            ? GetOrCreateScope(scope)
            : _destination;
        var result = new GameObject(source.name);
        result.transform.SetParent(parent.transform, false);
        Undo.RegisterCreatedObjectUndo(result, "Import Legacy FaceTune Scope");
        CopyEditorOnlyTag(source, result);
        _scopeObjects[source] = result;
        ApplyScopeSettings(source, result);
        return result;
    }

    private void ApplyScopeSettings(Transform source, GameObject target)
    {
        var hasExpression = source.TryGetComponent<LegacyExpressionComponent>(out _);
        var created = !target.TryGetComponent<SettingsComponent>(out var settings);
        if (created) settings = Undo.AddComponent<SettingsComponent>(target);

        var presets = source.GetComponents<LegacyPresetComponent>();
        if (presets.Length > 0)
        {
            settings.ExpressionSetEnabled = true;
            settings.ExpressionSet.DefaultSelected = presets.Any(_defaultPresets.Contains);
        }

        if (!hasExpression)
        {
            var conditionComponents = source.GetComponents<LegacyConditionComponent>();
            ApplyScopeConditions(settings, conditionComponents);
        }

        if (source.TryGetComponent<LegacyFacialStyleComponent>(out var style))
        {
            settings.HasFacialBlendShapes = true;
            settings.FacialBlendShapes.BlendShapeAnimations = CloneAnimations(style.BlendShapeAnimations);
            settings.ApplyToRenderer = style.ApplyToRenderer;
        }

        if (source.TryGetComponent<LegacyAdvancedEyeBlinkComponent>(out var eyeBlink))
        {
            settings.HasEyeBlink = true;
            settings.EyeBlink = ConvertEyeBlinkSettings(eyeBlink.AdvancedEyeBlinkSettings);
        }

        if (source.TryGetComponent<LegacyAdvancedLipSyncComponent>(out var lipSync))
        {
            settings.HasLipSync = true;
            settings.LipSync = ConvertLipSyncSettings(lipSync.AdvancedLipSyncSettings);
        }

        if (created
            && presets.Length == 0
            && !settings.HasFacialBlendShapes
            && !settings.HasEyeBlink
            && !settings.HasLipSync
            && !settings.HasCondition)
        {
            Object.DestroyImmediate(settings);
        }
    }

    private static void ApplyScopeConditions(
        SettingsComponent target,
        IReadOnlyList<LegacyConditionComponent> source)
    {
        if (source.Count == 0) return;

        var cases = source.Select(ToConditionCase).ToArray();
        if (cases.Any(conditionCase => conditionCase.IsEmpty)) return;
        if (cases.Length == 0) return;

        target.HasCondition = true;
        target.Condition.Cases = cases.ToList();
    }

    private void ImportOverrideFaceRenderer()
    {
        var source = _sourceRoot
            .GetComponentsInChildren<LegacyOverrideFaceRendererComponent>(true)
            .LastOrDefault()
            .DestroyedAsNull();
        if (source is null) return;

        if (!_destination.TryGetComponent<AvatarSettingsComponent>(out var target))
            target = Undo.AddComponent<AvatarSettingsComponent>(_destination);
        target.FaceObjectReference.Set(source.m_faceObjectReference.Get(source));
    }

    private void ImportExpression(LegacyExpressionComponent source)
    {
        if (IsAlwaysPlaying(source) && !HasExpressionData(source)) return;

        var parentSource = FindNearestScope(source.transform);
        var parent = parentSource.DestroyedAsNull() is { } scope
            ? GetOrCreateScope(scope)
            : _destination;
        var targetObject = new GameObject(source.gameObject.name);
        targetObject.transform.SetParent(parent.transform, false);
        Undo.RegisterCreatedObjectUndo(targetObject, "Import Legacy FaceTune Expression");
        CopyEditorOnlyTag(source.transform, targetObject);

        var target = targetObject.AddComponent<ExpressionComponent>();
        ConfigureExpression(source, target);
        ImportExpressionData(source, target);
    }

    private Transform? FindNearestScope(Transform source)
        => _scopeTransforms.Contains(source)
            ? source
            : FindParentScope(source.parent);

    private bool IsAlwaysPlaying(LegacyExpressionComponent expression)
    {
        var current = expression.transform;
        while (current is not null)
        {
            if (current.TryGetComponent<LegacyPresetComponent>(out _)) return false;

            var conditions = current.GetComponents<LegacyConditionComponent>();
            if (conditions.Length > 0 && conditions.All(condition => !IsEmpty(condition)))
                return false;

            if (current == _sourceRoot.transform) break;
            current = current.parent.DestroyedAsNull();
        }
        return true;
    }

    private bool HasExpressionData(LegacyExpressionComponent expression)
    {
        return expression.GetComponentsInChildren<LegacyExpressionDataComponent>(true)
            .Any(data => data.Clip.DestroyedAsNull() is not null
                         || data.BlendShapeAnimations.Count > 0);
    }

    private static void ConfigureExpression(
        LegacyExpressionComponent source,
        ExpressionComponent target)
    {
        var expressionSettings = source.ExpressionSettings ?? new ExpressionSettings();
        var facialSettings = source.FacialSettings ?? new FacialSettings();

        target.AllowEyeBlink = ConvertTrackingPermission(facialSettings.AllowEyeBlink);
        target.AllowLipSync = ConvertTrackingPermission(facialSettings.AllowLipSync);
        target.WriteMode = facialSettings.EnableBlending
            ? ExpressionWriteMode.Blend
            : ExpressionWriteMode.Replace;
        target.MultiFrame = expressionSettings.LoopTime
            ? new MultiFrameSettings { MultiFrameMode = MultiFrameSettings.Kind.Loop }
            : string.IsNullOrEmpty(expressionSettings.MotionTimeParameterName)
                ? new MultiFrameSettings()
                : new MultiFrameSettings
                {
                    MultiFrameMode = MultiFrameSettings.Kind.Parameter,
                    ParameterName = expressionSettings.MotionTimeParameterName
                };
        target.AlwaysOnPreviewEnabled = source.EnableRealTimePreview;

        var localConditions = source.GetComponents<LegacyConditionComponent>()
            .Where(condition => !IsEmpty(condition))
            .ToArray();
        var hasLocalConditions = localConditions.Length > 0;

        var isMenuItemImported = TryImportLegacyMenuItem(source, target);

        if (hasLocalConditions)
        {
            target.HasCondition = true;
            target.Condition.Mode = ConditionSelection.Kind.Conditional;
            target.Condition.Condition = new Condition(
                localConditions.Select(ToConditionCase).ToArray());
        }

        if (!hasLocalConditions && !isMenuItemImported)
        {
            target.HasCondition = true;
            target.Condition.Mode = ConditionSelection.Kind.Always;
            target.Condition.Condition = new Condition();
        }
    }
    private static bool TryImportLegacyMenuItem(LegacyExpressionComponent source, ExpressionComponent target)
    {
        if (!source.TryGetComponent<ModularAvatarMenuItem>(out var legacyMenuItem)) return false;
        if (legacyMenuItem.PortableControl.Type != PortableControlType.Toggle) return false;

        target.DirectMenuEnabled = true;
        if (legacyMenuItem.PortableControl.Icon != null)
        {
            target.DirectMenuSettings.Menu.Icon.Mode = MenuIconSettings.Kind.Manual;
            target.DirectMenuSettings.Menu.Icon.ManualIcon = legacyMenuItem.PortableControl.Icon;
        }

        return true;
    }

    private void ImportExpressionData(
        LegacyExpressionComponent source,
        ExpressionComponent target)
    {
        foreach (var sourceData in source.GetComponentsInChildren<LegacyExpressionDataComponent>(true))
        {
            if (sourceData.transform == source.transform)
            {
                ImportExpressionData(
                    sourceData,
                    target.FacialBlendShapes,
                    target.NonFacialAnimations);
                continue;
            }

            var data = target.gameObject.AddComponent<ExpressionDataComponent>();
            ImportExpressionData(
                sourceData,
                data.FacialBlendShapes,
                data.NonFacialAnimations);
        }
    }

    private void ImportExpressionData(
        LegacyExpressionDataComponent source,
        FacialBlendShapeData facialData,
        NonFacialAnimationData nonFacialData)
    {
        if (source.Clip.DestroyedAsNull() is { } clip)
        {
            if (CanKeepClip(source))
            {
                facialData.Clip = clip;
                facialData.ClipOption = ConvertClipImportOption(source.ClipOption);
            }
            else
            {
                facialData.BlendShapeAnimations.AddRange(
                    ExtractFacialAnimations(source));
            }

            var nonFacialClip = GetOrCreateNonFacialClip(
                clip,
                source.AllBlendShapeAnimationAsFacial);
            if (nonFacialClip.DestroyedAsNull() is { } animationClip)
                nonFacialData.AnimationClips.Add(animationClip);
        }

        facialData.BlendShapeAnimations.AddRange(
            CloneAnimations(source.BlendShapeAnimations));
    }

    private bool CanKeepClip(LegacyExpressionDataComponent source)
    {
        if (source.Clip == null
            || source.ClipOption is not (LegacyClipImportOption.All or LegacyClipImportOption.NonZero))
            return false;
        if (!source.AllBlendShapeAnimationAsFacial) return true;

        return !AnimationUtility.GetCurveBindings(source.Clip)
            .Any(binding => IsBlendShapeBinding(binding)
                            && !string.Equals(
                                binding.path,
                                _context.BodyPath,
                                StringComparison.OrdinalIgnoreCase));
    }

    private List<BlendShapeWeightAnimation> ExtractFacialAnimations(
        LegacyExpressionDataComponent source)
    {
        if (source.Clip == null) return new List<BlendShapeWeightAnimation>();

        var style = FindFacialStyle(source.transform);
        var result = new List<BlendShapeWeightAnimation>();
        foreach (var binding in AnimationUtility.GetCurveBindings(source.Clip))
        {
            if (!IsFacialBinding(binding, source.AllBlendShapeAnimationAsFacial)) continue;
            var curve = AnimationUtility.GetEditorCurve(source.Clip, binding);
            if (curve == null || curve.keys.Length == 0) continue;
            if (!ShouldImportFacialCurve(source.ClipOption, binding, curve, style)) continue;

            var name = binding.propertyName.Substring(FaceTuneConstants.BlendShapePropertyPrefix.Length);
            result.Add(new BlendShapeWeightAnimation(name, curve));
        }

        return result;
    }

    private static bool ShouldImportFacialCurve(
        LegacyClipImportOption option,
        EditorCurveBinding binding,
        AnimationCurve curve,
        LegacyFacialStyleComponent? style)
    {
        var isZero = curve.keys.All(key => key.value == 0f);
        return option switch
        {
            LegacyClipImportOption.All => true,
            LegacyClipImportOption.NonZero => !isZero,
            LegacyClipImportOption.FacialStyleOverridesOrNonZero =>
                ShouldImportStyleOverride(binding, curve, style, isZero),
            _ => throw new ArgumentOutOfRangeException(nameof(option), option, null)
        };
    }

    private static bool ShouldImportStyleOverride(
        EditorCurveBinding binding,
        AnimationCurve curve,
        LegacyFacialStyleComponent? style,
        bool isZero)
    {
        if (style == null) return !isZero;
        var name = binding.propertyName.Substring(FaceTuneConstants.BlendShapePropertyPrefix.Length);
        var styleAnimation = style.BlendShapeAnimations
            .FirstOrDefault(animation => animation != null && animation.Name == name);
        return styleAnimation == null ? !isZero : !styleAnimation.Curve.Equals(curve);
    }

    private LegacyFacialStyleComponent? FindFacialStyle(Transform source)
    {
        for (var current = source; current != null; current = current.parent)
        {
            if (current.TryGetComponent<LegacyFacialStyleComponent>(out var style))
                return style;
            if (current == _sourceRoot.transform) break;
        }

        return null;
    }

    private AnimationClip? GetOrCreateNonFacialClip(
        AnimationClip source,
        bool allBlendShapesAsFacial)
    {
        var key = (source, allBlendShapesAsFacial);
        if (_nonFacialClips.TryGetValue(key, out var cached)) return cached;

        var result = new AnimationClip
        {
            name = SanitizeFileName(source.name) + "_NonFacial"
        };
        var hasCurve = false;
        foreach (var binding in AnimationUtility.GetCurveBindings(source))
        {
            var curve = AnimationUtility.GetEditorCurve(source, binding);
            if (curve == null || curve.keys.Length == 0
                || IsFacialBinding(binding, allBlendShapesAsFacial)) continue;

            AnimationUtility.SetEditorCurve(result, binding, curve);
            hasCurve = true;
        }

        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
        {
            var curve = AnimationUtility.GetObjectReferenceCurve(source, binding);
            if (curve == null || curve.Length == 0) continue;
            AnimationUtility.SetObjectReferenceCurve(result, binding, curve);
            hasCurve = true;
        }

        if (!hasCurve)
        {
            Object.DestroyImmediate(result);
            _nonFacialClips[key] = null;
            return null;
        }

        var settings = AnimationUtility.GetAnimationClipSettings(source);
        AnimationUtility.SetAnimationClipSettings(result, settings);
        EnsureFolder(_outputFolder);
        var path = AssetDatabase.GenerateUniqueAssetPath(
            $"{_outputFolder}/{SanitizeFileName(result.name)}.anim");
        AssetDatabase.CreateAsset(result, path);
        _nonFacialClips[key] = result;
        return result;
    }

    private bool IsFacialBinding(
        EditorCurveBinding binding,
        bool allBlendShapesAsFacial)
        => IsBlendShapeBinding(binding)
           && (allBlendShapesAsFacial
               || string.Equals(binding.path, _context.BodyPath, StringComparison.OrdinalIgnoreCase));

    private static bool IsBlendShapeBinding(EditorCurveBinding binding)
        => binding.type == typeof(SkinnedMeshRenderer)
           && binding.propertyName.StartsWith(
               FaceTuneConstants.BlendShapePropertyPrefix,
               StringComparison.Ordinal);

    private static List<BlendShapeWeightAnimation> CloneAnimations(
        IEnumerable<BlendShapeWeightAnimation>? source)
        => source?.OfType<BlendShapeWeightAnimation>()
            .Select(animation => new BlendShapeWeightAnimation(animation.Name, animation.Curve))
            .ToList()
           ?? new List<BlendShapeWeightAnimation>();

    private static bool IsEmpty(LegacyConditionComponent source)
        => (source.HandGestureConditions == null || source.HandGestureConditions.Count == 0)
           && (source.ParameterConditions == null || source.ParameterConditions.Count == 0);

    private static ConditionCase ToConditionCase(LegacyConditionComponent source)
        => new()
        {
            HandGestureConditions = source.HandGestureConditions?
                .Select(ConvertHandGestureCondition)
                .ToList() ?? new List<HandGestureCondition>(),
            ParameterConditions = source.ParameterConditions?
                .Select(ConvertParameterCondition)
                .ToList() ?? new List<ParameterCondition>()
        };

    private static HandGestureCondition ConvertHandGestureCondition(
        LegacyHandGestureCondition source)
        => new()
        {
            Hand = source.Hand switch
            {
                LegacyHand.Left => HandGestureHand.Left,
                LegacyHand.Right => HandGestureHand.Right,
                _ => throw new ArgumentOutOfRangeException(nameof(source.Hand), source.Hand, null)
            },
            Gesture = source.HandGesture switch
            {
                LegacyHandGesture.Neutral => HandGesture.Neutral,
                LegacyHandGesture.Fist => HandGesture.Fist,
                LegacyHandGesture.HandOpen => HandGesture.HandOpen,
                LegacyHandGesture.FingerPoint => HandGesture.FingerPoint,
                LegacyHandGesture.Victory => HandGesture.Victory,
                LegacyHandGesture.RockNRoll => HandGesture.RockNRoll,
                LegacyHandGesture.HandGun => HandGesture.HandGun,
                LegacyHandGesture.ThumbsUp => HandGesture.ThumbsUp,
                _ => throw new ArgumentOutOfRangeException(nameof(source.HandGesture), source.HandGesture, null)
            },
            Matches = source.EqualityComparison switch
            {
                LegacyEqualityComparison.Equal => true,
                LegacyEqualityComparison.NotEqual => false,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(source.EqualityComparison), source.EqualityComparison, null)
            }
        };

    private static ParameterCondition ConvertParameterCondition(
        LegacyParameterCondition source)
        => new()
        {
            ParameterName = source.ParameterName,
            ParameterType = source.ParameterType switch
            {
                LegacyParameterType.Int => ParameterType.Int,
                LegacyParameterType.Float => ParameterType.Float,
                LegacyParameterType.Bool => ParameterType.Bool,
                _ => throw new ArgumentOutOfRangeException(nameof(source.ParameterType), source.ParameterType, null)
            },
            ComparisonType = source.ComparisonType switch
            {
                LegacyComparisonType.Equal => ComparisonType.Equal,
                LegacyComparisonType.NotEqual => ComparisonType.NotEqual,
                LegacyComparisonType.GreaterThan => ComparisonType.GreaterThan,
                LegacyComparisonType.LessThan => ComparisonType.LessThan,
                _ => throw new ArgumentOutOfRangeException(nameof(source.ComparisonType), source.ComparisonType, null)
            },
            FloatValue = source.FloatValue,
            IntValue = source.IntValue,
            BoolValue = source.BoolValue
        };

    private static TrackingPermission ConvertTrackingPermission(
        LegacyTrackingPermission source)
        => source switch
        {
            LegacyTrackingPermission.Allow => TrackingPermission.Allow,
            LegacyTrackingPermission.Disallow => TrackingPermission.Disallow,
            LegacyTrackingPermission.Keep => TrackingPermission.Keep,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
        };

    private static ClipImportOption ConvertClipImportOption(
        LegacyClipImportOption source)
        => source switch
        {
            LegacyClipImportOption.All => ClipImportOption.All,
            LegacyClipImportOption.NonZero => ClipImportOption.NonZero,
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
        };

    private static EyeBlinkSettings ConvertEyeBlinkSettings(
        AdvancedEyeBlinkSettings source)
    {
        var result = new EyeBlinkSettings
        {
            EyeBlinkMode = source.UseAdvancedEyeBlink && source.UseAnimation
                ? EyeBlinkSettings.Kind.SimpleAnimation
                : EyeBlinkSettings.Kind.BuiltIn,
            IntervalSeconds = source.UseRandomInterval
                ? new Vector2(source.RandomIntervalMinSeconds, source.RandomIntervalMaxSeconds)
                : new Vector2(source.IntervalSeconds, source.IntervalSeconds),
            SimpleDurationsSeconds = new Vector3(
                source.ClosingDurationSeconds,
                source.HoldDurationSeconds,
                source.OpeningDurationSeconds),
            SimpleBlinkBlendShapes = source.BlinkBlendShapeNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => new BlendShapeWeight(name, 100f))
                .ToList(),
            SimpleConflictPreventionBlendShapes = source.UseAdvancedEyeBlink
                && source.UseAnimation
                && source.UseCanceler
                ? source.CancelerBlendShapeNames
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => new BlendShapeWeight(name, 0f))
                    .ToList()
                : new List<BlendShapeWeight>()
        };
        return result;
    }

    private static LipSyncSettings ConvertLipSyncSettings(
        AdvancedLipSyncSettings source)
        => new()
        {
            CancellerBlendShapes = source.UseAdvancedLipSync && source.UseCanceler
                ? source.CancelerBlendShapeNames
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => new BlendShapeWeight(name, 0f))
                    .ToList()
                : new List<BlendShapeWeight>()
        };

    private static string SanitizeFileName(string name)
        => string.Concat(name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));

    private static void EnsureFolder(string path)
    {
        var current = "Assets";
        foreach (var part in path.Substring("Assets".Length)
                     .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var next = current + "/" + part;
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, part);
            current = next;
        }
    }
}

#pragma warning restore CS0618
