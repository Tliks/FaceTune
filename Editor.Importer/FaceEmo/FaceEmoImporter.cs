#if FACETUNE_FACE_EMO


using System.IO;
using Aoyon.FaceTune.Importing;
using Suzuryg.FaceEmo.Components;
using Suzuryg.FaceEmo.Components.Data;
using Suzuryg.FaceEmo.Domain;
using FaceEmoHand = Suzuryg.FaceEmo.Domain.Hand;
using FaceEmoGesture = Suzuryg.FaceEmo.Domain.HandGesture;
using FaceTuneHand = Aoyon.FaceTune.Hand;
using FaceTuneGesture = Aoyon.FaceTune.HandGesture;

namespace Aoyon.FaceTune.Importers.FaceEmo;

internal sealed class FaceEmoImporter
{
    private const string OptionPrefabGuid = "bd8741136293c7c43b955a2d1b5f4a37";

    private readonly AvatarContext _context;
    private readonly FaceEmoLauncherComponent _source;
    private readonly SerializableMenu _menu;
    private readonly float _transitionSeconds;
    private readonly string _outputFolder;
    private readonly Dictionary<AnimationClip, AnimationClip?> _nonFacialClips = new();

    public FaceEmoImporter(
        AvatarContext context,
        FaceEmoLauncherComponent source,
        SerializableMenu menu,
        float transitionSeconds,
        string outputFolder)
    {
        _context = context;
        _source = source;
        _menu = menu;
        _transitionSeconds = transitionSeconds;
        _outputFolder = outputFolder;
    }

    public void Import(GameObject root)
    {
        CreateFoundation(root);
        AddOption(root);
        ImportItems(_menu.Registered, root);

        if (_menu.Unregistered.Types.Count > 0)
        {
            var archive = CreateObject("Archive", root);
            archive.tag = "EditorOnly";
            ImportItems(_menu.Unregistered, archive);
        }
    }

    private void CreateFoundation(GameObject root)
    {
        var settings = root.AddComponent<SettingsComponent>();
        settings.HasFacialBlendShapes = true;

        var folder = root.AddComponent<MenuComponent>();
        folder.MenuKind = MenuComponent.Kind.Folder;
        folder.Menu.MenuName = "FaceTune";

        var excludedBlendShapes = _source.AV3Setting.ExcludedBlendShapes
            .Where(shape => shape != null)
            .Select(shape => shape.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (excludedBlendShapes.Count > 0 || !_source.AV3Setting.DisableTrackingControls)
        {
            var avatarSettings = root.AddComponent<AvatarSettingsComponent>();
            avatarSettings.ExcludedBlendShapeNames = excludedBlendShapes;
            avatarSettings.AvoidEyeBlinkConflicts = _source.AV3Setting.DisableTrackingControls;
            avatarSettings.AvoidLipSyncConflicts = _source.AV3Setting.DisableTrackingControls;
        }

        if (_source.AV3Setting.UseBlinkClip && _source.AV3Setting.BlinkClip != null)
        {
            settings.HasEyeBlink = true;
            settings.EyeBlink.EyeBlinkMode = EyeBlinkSettings.Kind.CustomAnimation;
            settings.EyeBlink.Animations.Clear();
            _source.AV3Setting.BlinkClip.GetBlendShapeAnimations(
                ClipImportOption.All,
                settings.EyeBlink.Animations,
                _context.BodyPath);
        }

        if (_source.AV3Setting.MouthMorphs.Count > 0)
        {
            settings.HasLipSync = true;
            settings.LipSync.CancellerBlendShapes = _source.AV3Setting.MouthMorphs
                .Where(shape => shape != null)
                .Select(shape => new BlendShapeWeight(shape.Name, 0f))
                .Distinct()
                .ToList();
        }
    }

    private void AddOption(GameObject root)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(OptionPrefabGuid));
        var option = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
        Undo.RegisterCreatedObjectUndo(option, "Import FaceEmo Option");
        PrefabUtility.UnpackPrefabInstance(option, PrefabUnpackMode.Completely, InteractionMode.UserAction);
    }

    private void ImportItems(SerializableMenuItemListBase items, GameObject parent)
    {
        var modeIndex = 0;
        var groupIndex = 0;
        for (var index = 0; index < items.Types.Count; index++)
        {
            if (items.Types[index] == MenuItemType.Mode)
                ImportMode(items.Modes[modeIndex++], items.Ids[index], parent);
            else
                ImportGroup(items.Groups[groupIndex++], items.Ids[index], parent);
        }
    }

    private void ImportGroup(SerializableGroup group, string id, GameObject parent)
    {
        var name = string.IsNullOrWhiteSpace(group.DisplayName) ? id : group.DisplayName;
        var obj = CreateMenuFolder(name, parent);
        ImportItems(group, obj);
    }

    private void ImportMode(SerializableMode mode, string id, GameObject parent)
    {
        var name = GetModeName(mode, id);
        var obj = CreateMenuFolder(name, parent);
        var settings = obj.AddComponent<SettingsComponent>();
        settings.ExpressionSetEnabled = true;
        settings.ExpressionSet.DefaultSelected = id == _menu.DefaultSelection;
        if (!Mathf.Approximately(_transitionSeconds, settings.Transition.DurationSeconds))
        {
            settings.HasTransition = true;
            settings.Transition.DurationSeconds = _transitionSeconds;
        }

        var expressions = new List<GameObject>();
        if (mode.ChangeDefaultFace && LoadClip(mode.Animation) is { } defaultClip)
            expressions.Add(CreateExpression(
                obj,
                defaultClip.name,
                defaultClip,
                null,
                mode.BlinkEnabled,
                mode.EyeTrackingControl,
                mode.MouthTrackingControl,
                mode.MouthMorphCancelerEnabled,
                true).gameObject);

        for (var index = 0; index < mode.Branches.Count; index++)
        {
            var expression = ImportBranch(mode.Branches[index], index, obj);
            if (expression != null) expressions.Add(expression.gameObject);
        }
        ExpressionHierarchyOrganizer.Organize(obj, expressions);
    }

    private ExpressionComponent? ImportBranch(
        SerializableBranch branch,
        int index,
        GameObject parent)
    {
        var baseClip = LoadClip(branch.BaseAnimation);
        if (baseClip == null) return null;

        var name = GetBranchName(baseClip, index);
        var hasTrigger = branch.IsLeftTriggerUsed && LoadClip(branch.LeftHandAnimation) != null
                         || branch.IsRightTriggerUsed && LoadClip(branch.RightHandAnimation) != null;
        var expression = CreateExpression(
            parent,
            name,
            baseClip,
            ToCondition(branch.Conditions),
            branch.BlinkEnabled,
            branch.EyeTrackingControl,
            branch.MouthTrackingControl,
            branch.MouthMorphCancelerEnabled,
            !hasTrigger);
        ApplyTriggerAnimation(expression, branch, baseClip);
        return expression;
    }

    private static string GetBranchName(AnimationClip clip, int index)
    {
        var name = RemoveFaceEmoRoleSuffix(clip.name);
        return string.IsNullOrWhiteSpace(name) ? $"Branch {index + 1}" : name;
    }

    private static string RemoveFaceEmoRoleSuffix(string name)
    {
        var suffix = new[] { "_Base", "_Left", "_Right", "_Both" }
            .FirstOrDefault(candidate => name.EndsWith(candidate, StringComparison.Ordinal));
        return suffix == null ? name : name.Substring(0, name.Length - suffix.Length);
    }

    private static string GetModeName(SerializableMode mode, string id)
    {
        if (mode.ChangeDefaultFace && mode.UseAnimationNameAsDisplayName && LoadClip(mode.Animation) is { } clip)
            return clip.name;
        return string.IsNullOrWhiteSpace(mode.DisplayName) ? id : mode.DisplayName;
    }

    private ExpressionComponent CreateExpression(
        GameObject parent,
        string name,
        AnimationClip clip,
        Condition? condition,
        bool blinkEnabled,
        EyeTrackingControl eyeTracking,
        MouthTrackingControl mouthTracking,
        bool mouthMorphCancelerEnabled,
        bool importNonFacial)
    {
        var expression = CreateObject(name, parent).AddComponent<ExpressionComponent>();
        ImportAnimation(expression, clip, importNonFacial);
        expression.AllowEyeBlink = blinkEnabled && eyeTracking == EyeTrackingControl.Tracking
            ? TrackingPermission.Allow
            : TrackingPermission.Disallow;
        expression.AllowLipSync = mouthTracking == MouthTrackingControl.Tracking
            ? TrackingPermission.Allow
            : TrackingPermission.Disallow;
        if (!mouthMorphCancelerEnabled)
        {
            expression.HasLipSync = true;
            expression.LipSync.CancellerBlendShapes.Clear();
        }
        expression.DirectMenuEnabled = _source.AV3Setting.AddConfig_EmoteSelect;
        expression.HasCondition = true;
        expression.Condition.Mode = condition == null
            ? ConditionSelection.Kind.Always
            : ConditionSelection.Kind.Conditional;
        if (condition != null) expression.Condition.Condition = condition;
        if (clip.isLooping) expression.MultiFrame.MultiFrameMode = MultiFrameSettings.Kind.Loop;
        return expression;
    }

    private void ImportAnimation(ExpressionComponent expression, AnimationClip source, bool importNonFacial)
    {
        source.GetBlendShapeAnimations(
            ClipImportOption.All,
            expression.FacialBlendShapes.BlendShapeAnimations,
            _context.BodyPath);
        if (!importNonFacial) return;

        var generated = GetOrCreateNonFacialClip(source);
        if (generated != null) expression.NonFacialAnimations.AnimationClips.Add(generated);

        foreach (var binding in AnimationUtility.GetCurveBindings(source)
                     .Where(binding => binding.type == typeof(GameObject)
                                       && binding.propertyName == "m_IsActive"))
        {
            var target = ResolveTarget(binding.path);
            var curve = AnimationUtility.GetEditorCurve(source, binding);
            if (target == null || curve == null) continue;
            expression.NonFacialAnimations.TransformAnimations.Add(new TransformAnimation
            {
                Target = new AvatarObjectReference(target),
                Curve = new AnimationCurve(curve.keys)
            });
        }
    }

    private AnimationClip? GetOrCreateNonFacialClip(AnimationClip source)
    {
        if (_nonFacialClips.TryGetValue(source, out var cached)) return cached;

        var result = new AnimationClip { name = RemoveFaceEmoRoleSuffix(source.name) + "_NonFacial" };
        foreach (var binding in AnimationUtility.GetCurveBindings(source))
        {
            if (IsFacial(binding) || binding.type == typeof(GameObject) && binding.propertyName == "m_IsActive")
                continue;
            AnimationUtility.SetEditorCurve(result, binding, AnimationUtility.GetEditorCurve(source, binding));
        }
        foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
            AnimationUtility.SetObjectReferenceCurve(
                result,
                binding,
                AnimationUtility.GetObjectReferenceCurve(source, binding));

        if (AnimationUtility.GetCurveBindings(result).Length == 0
            && AnimationUtility.GetObjectReferenceCurveBindings(result).Length == 0)
        {
            UnityEngine.Object.DestroyImmediate(result);
            _nonFacialClips[source] = null;
            return null;
        }

        var settings = AnimationUtility.GetAnimationClipSettings(source);
        AnimationUtility.SetAnimationClipSettings(result, settings);
        var path = AssetDatabase.GenerateUniqueAssetPath($"{_outputFolder}/{SanitizeFileName(result.name)}.anim");
        AssetDatabase.CreateAsset(result, path);
        _nonFacialClips[source] = result;
        return result;
    }

    private bool IsFacial(EditorCurveBinding binding)
        => binding.path == _context.BodyPath
           && binding.type == typeof(SkinnedMeshRenderer)
           && binding.propertyName.StartsWith("blendShape.", StringComparison.Ordinal);

    private GameObject? ResolveTarget(string path)
        => string.IsNullOrEmpty(path)
            ? _context.Root
            : _context.Root.transform.Find(path)?.gameObject;

    private static string SanitizeFileName(string name)
        => string.Concat(name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));

    private void ApplyTriggerAnimation(ExpressionComponent expression, SerializableBranch branch, AnimationClip baseClip)
    {
        var leftClip = branch.IsLeftTriggerUsed ? LoadClip(branch.LeftHandAnimation) : null;
        var rightClip = branch.IsRightTriggerUsed ? LoadClip(branch.RightHandAnimation) : null;
        // FaceEmoの両手Triggerは、右手側をFaceTuneの1軸Triggerとして取り込む。
        var targetClip = rightClip ?? leftClip;
        if (targetClip == null) return;

        expression.FacialBlendShapes.BlendShapeAnimations = CreateTriggerCurves(baseClip, targetClip);
        expression.NonFacialAnimations.AnimationClips.Clear();
        expression.NonFacialAnimations.TransformAnimations.Clear();
        ImportTriggerNonFacial(expression, baseClip, targetClip);
        expression.MultiFrame.MultiFrameMode = MultiFrameSettings.Kind.Trigger;
        expression.MultiFrame.TriggerHand = rightClip != null ? FaceTuneHand.Right : FaceTuneHand.Left;
    }

    private void ImportTriggerNonFacial(
        ExpressionComponent expression,
        AnimationClip baseClip,
        AnimationClip targetClip)
    {
        var baseCurves = AnimationUtility.GetCurveBindings(baseClip)
            .Where(binding => !IsFacial(binding))
            .ToDictionary(binding => binding, binding => AnimationUtility.GetEditorCurve(baseClip, binding));
        var targetCurves = AnimationUtility.GetCurveBindings(targetClip)
            .Where(binding => !IsFacial(binding))
            .ToDictionary(binding => binding, binding => AnimationUtility.GetEditorCurve(targetClip, binding));
        var generated = new AnimationClip
        {
            name = RemoveFaceEmoRoleSuffix(baseClip.name) + "_Trigger"
        };

        foreach (var binding in baseCurves.Keys.Concat(targetCurves.Keys).Distinct())
        {
            baseCurves.TryGetValue(binding, out var baseCurve);
            targetCurves.TryGetValue(binding, out var targetCurve);
            var start = baseCurve?.Evaluate(0f) ?? targetCurve!.Evaluate(0f);
            var end = targetCurve?.Evaluate(0f) ?? start;
            var curve = LinearCurve(start, end);
            if (binding.type == typeof(GameObject) && binding.propertyName == "m_IsActive")
            {
                var target = ResolveTarget(binding.path);
                if (target != null)
                    expression.NonFacialAnimations.TransformAnimations.Add(new TransformAnimation
                    {
                        Target = new AvatarObjectReference(target),
                        Curve = curve
                    });
            }
            else
            {
                AnimationUtility.SetEditorCurve(generated, binding, curve);
            }
        }

        var baseObjects = AnimationUtility.GetObjectReferenceCurveBindings(baseClip)
            .ToDictionary(binding => binding, binding => AnimationUtility.GetObjectReferenceCurve(baseClip, binding));
        var targetObjects = AnimationUtility.GetObjectReferenceCurveBindings(targetClip)
            .ToDictionary(binding => binding, binding => AnimationUtility.GetObjectReferenceCurve(targetClip, binding));
        foreach (var binding in baseObjects.Keys.Concat(targetObjects.Keys).Distinct())
        {
            baseObjects.TryGetValue(binding, out var baseKeys);
            targetObjects.TryGetValue(binding, out var targetKeys);
            var start = baseKeys is { Length: > 0 }
                ? baseKeys[0].value
                : targetKeys is { Length: > 0 } ? targetKeys[0].value : null;
            var end = targetKeys is { Length: > 0 } ? targetKeys[0].value : start;
            AnimationUtility.SetObjectReferenceCurve(generated, binding, new[]
            {
                new ObjectReferenceKeyframe { time = 0f, value = start },
                new ObjectReferenceKeyframe { time = 1f, value = end }
            });
        }

        if (AnimationUtility.GetCurveBindings(generated).Length == 0
            && AnimationUtility.GetObjectReferenceCurveBindings(generated).Length == 0)
        {
            UnityEngine.Object.DestroyImmediate(generated);
            return;
        }

        var path = AssetDatabase.GenerateUniqueAssetPath($"{_outputFolder}/{SanitizeFileName(generated.name)}.anim");
        AssetDatabase.CreateAsset(generated, path);
        expression.NonFacialAnimations.AnimationClips.Add(generated);
    }

    private static AnimationCurve LinearCurve(float start, float end)
    {
        var slope = end - start;
        return new AnimationCurve(
            new Keyframe(0f, start, slope, slope),
            new Keyframe(1f, end, slope, slope));
    }

    private List<BlendShapeWeightAnimation> CreateTriggerCurves(AnimationClip baseClip, AnimationClip targetClip)
    {
        var baseWeights = GetFirstFrame(baseClip);
        var targetWeights = GetFirstFrame(targetClip);
        return baseWeights.Keys.Concat(targetWeights.Keys)
            .Distinct(StringComparer.Ordinal)
            .Select(name =>
            {
                baseWeights.TryGetValue(name, out var start);
                targetWeights.TryGetValue(name, out var end);
                var slope = end - start;
                return new BlendShapeWeightAnimation(name, new AnimationCurve(
                    new Keyframe(0f, start, slope, slope),
                    new Keyframe(1f, end, slope, slope)));
            })
            .ToList();
    }

    private Dictionary<string, float> GetFirstFrame(AnimationClip clip)
    {
        var weights = new List<BlendShapeWeight>();
        clip.GetFirstFrameBlendShapes(ClipImportOption.All, weights, _context.BodyPath);
        return weights.ToDictionary(weight => weight.Name, weight => weight.Weight, StringComparer.Ordinal);
    }

    private static Condition ToCondition(IEnumerable<SerializableCondition> source)
    {
        var cases = new List<List<HandGestureCondition>> { new() };
        foreach (var condition in source)
        {
            var alternatives = ExpandCondition(condition);
            cases = cases
                .SelectMany(current => alternatives.Select(addition => current.Concat(addition).ToList()))
                .ToList();
        }
        return new Condition(cases.Select(conditions => ConditionCase.From(conditions.ToArray())).ToArray());
    }

    private static IReadOnlyList<HandGestureCondition[]> ExpandCondition(SerializableCondition source)
    {
        var gesture = ToGesture(source.HandGesture);
        var matches = source.ComparisonOperator == ComparisonOperator.Equals;
        HandGestureCondition Left(bool value) => GestureCondition(HandGestureHand.Left, gesture, value);
        HandGestureCondition Right(bool value) => GestureCondition(HandGestureHand.Right, gesture, value);

        return source.Hand switch
        {
            FaceEmoHand.Left => new[] { new[] { Left(matches) } },
            FaceEmoHand.Right => new[] { new[] { Right(matches) } },
            FaceEmoHand.OneSide => new[]
            {
                new[] { Left(true), Right(false) },
                new[] { Left(false), Right(true) }
            },
            FaceEmoHand.Either when matches => new[] { new[] { Left(true) }, new[] { Right(true) } },
            FaceEmoHand.Either => new[] { new[] { Left(false) }, new[] { Right(false) } },
            FaceEmoHand.Both when matches => new[] { new[] { Left(true), Right(true) } },
            FaceEmoHand.Both => new[] { new[] { Left(false), Right(false) } },
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static HandGestureCondition GestureCondition(
        HandGestureHand hand,
        FaceTuneGesture gesture,
        bool matches)
        => new() { Hand = hand, Gesture = gesture, Matches = matches };

    private static FaceTuneGesture ToGesture(FaceEmoGesture gesture)
        => gesture switch
        {
            FaceEmoGesture.Neutral => FaceTuneGesture.Neutral,
            FaceEmoGesture.Fist => FaceTuneGesture.Fist,
            FaceEmoGesture.HandOpen => FaceTuneGesture.HandOpen,
            FaceEmoGesture.Fingerpoint => FaceTuneGesture.FingerPoint,
            FaceEmoGesture.Victory => FaceTuneGesture.Victory,
            FaceEmoGesture.RockNRoll => FaceTuneGesture.RockNRoll,
            FaceEmoGesture.HandGun => FaceTuneGesture.HandGun,
            FaceEmoGesture.ThumbsUp => FaceTuneGesture.ThumbsUp,
            _ => throw new ArgumentOutOfRangeException(nameof(gesture), gesture, null)
        };

    private static AnimationClip? LoadClip(SerializableAnimation? animation)
        => animation == null || string.IsNullOrEmpty(animation.GUID)
            ? null
            : AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(animation.GUID));

    private static GameObject CreateMenuFolder(string name, GameObject parent)
    {
        var obj = CreateObject(name, parent);
        var folder = obj.AddComponent<MenuComponent>();
        folder.MenuKind = MenuComponent.Kind.Folder;
        return obj;
    }

    private static GameObject CreateObject(string name, GameObject parent)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent.transform, false);
        return obj;
    }
}

#endif