using UnityEditor.Animations;
using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Importer;

internal class AnimatorControllerImporter
{
    private readonly SessionContext _context;
    private readonly int _allFacialBlendshapeCount;
    private readonly AnimatorController _animatorController;
    private readonly IMetabasePlatformSupport _platformSupport;
    private readonly Dictionary<string, AnimatorControllerParameterType> _parameterTypes;
    private readonly Dictionary<AnimationSet, AnimationClip> _clipMap;

    public AnimatorControllerImporter(SessionContext context, AnimatorController animatorController)
    {
        _context = context;
        _animatorController = animatorController;
        _platformSupport = MetabasePlatformSupport.GetSupportInParents(context.Root.transform);
        _parameterTypes = animatorController.parameters.ToDictionary(p => p.name, p => p.type);
        _clipMap = new Dictionary<AnimationSet, AnimationClip>();
        _allFacialBlendshapeCount = context.FaceRenderer.GetBlendShapes(context.FaceMesh).Count();
    }

    public void Import(GameObject root)
    {
        Debug.Log($"Importing {_animatorController.name}");
        AssetDatabase.StartAssetEditing();
        try
        {
        var expressionCount = 0;
        var layers = _animatorController.layers;
        GameObject? firstLayerObj = null;
        for (int i = 0; i < layers.Length; i++)
        {
            var layer = layers[i];
            var stateMachine = layer.stateMachine;
            if (stateMachine == null) continue;

            var conditionStateList = new List<(AnimatorCondition[] conditions, AnimatorState state)>();
            CollectConditionsAndStates(stateMachine, conditionStateList);
            Debug.Log($"Layer {i} collected: {conditionStateList.Count} condition-state pairs");
            
            if (conditionStateList.Count > 0)
            {
                var layerObj = new GameObject(layer.name);
                firstLayerObj ??= layerObj;
                layerObj.transform.parent = root.transform;
                layerObj.AddComponent<PatternComponent>();

                var validExpressionPerLayer = 0;

                foreach (var (conditions, state) in conditionStateList)
                {
                    var clip = state.motion as AnimationClip;
                    if (clip == null) continue;

                    var (facialAnimations, nonFacialAnimations) = AnalyzeAnimationClip(clip);

                    if (facialAnimations.Count > 0)
                    {
                        var isfullAnimation = facialAnimations.Count >= _allFacialBlendshapeCount * 0.9 || ((_allFacialBlendshapeCount - facialAnimations.Count) < 100);
                        var isBlending = !isfullAnimation;
                        var obj = CreateConditionAndExpression(state, conditions, isBlending);
                        obj.transform.parent = layerObj.transform;

                        if (!isBlending)
                        {
                            facialAnimations = facialAnimations
                                .Where(fa => !fa.Curve.keys.All(k => k.value == 0))
                                .ToList();
                        }

                        AddFacialData(obj, facialAnimations);
                        if (nonFacialAnimations.Count > 0)
                        {
                            AddAnimationData(obj, nonFacialAnimations);
                        }

                        validExpressionPerLayer++;
                        expressionCount++;
                    }
                }

                if (validExpressionPerLayer == 0)
                {
                    Object.DestroyImmediate(layerObj);
                }
            }
        }

        Undo.RegisterCreatedObjectUndo(root, "Import FX");
        if (firstLayerObj != null)
        {
            Selection.activeObject = firstLayerObj;
            EditorGUIUtility.PingObject(firstLayerObj);
        }

        Debug.Log($"Finished to import {_animatorController.name}: {expressionCount} expressions");
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }
    }

    private static void CollectConditionsAndStates(AnimatorStateMachine stateMachine, List<(AnimatorCondition[] conditions, AnimatorState state)> conditionStateList)
    {
        if (stateMachine.defaultState is { } defaultState)
        {
            if (defaultState.motion is AnimationClip)
            {
                conditionStateList.Add((new AnimatorCondition[0], defaultState));
            }
        }
        
        foreach (var transition in stateMachine.entryTransitions)
        {
            if (IsValidTransition(transition, out var state))
            {
                conditionStateList.Add((transition.conditions, state));
            }
        }
        
        foreach (var transition in stateMachine.anyStateTransitions)
        {
            if (IsValidTransition(transition, out var state))
            {
                conditionStateList.Add((transition.conditions, state));
            }
        }

        foreach (var stateInfo in stateMachine.states)
        {
            foreach (var transition in stateInfo.state.transitions)
            {
                if (IsValidTransition(transition, out var state))
                {
                    conditionStateList.Add((transition.conditions, state));
                }
            }
        }

        foreach (var subStateMachine in stateMachine.stateMachines)
        {
            CollectConditionsAndStates(subStateMachine.stateMachine, conditionStateList);
        }

        static bool IsValidTransition(AnimatorTransitionBase transition, [NotNullWhen(true)] out AnimatorState? state)
        {
            state = null;
            
            if (transition.destinationState is not { } destinationState)
            {
                return false;
            }
                
            if (destinationState.motion is not AnimationClip)
            {
                return false;
            }

            // Debug.Log($"Valid transition found: state '{destinationState.name}' with {transition.conditions.Length} conditions");
            state = destinationState;
            return true;
        }
    }

    private GameObject CreateConditionAndExpression(AnimatorState state, AnimatorCondition[] conditions, bool isBlending)
    {
        var obj = new GameObject(state.name);

        if (conditions.Length > 0)
        {
            var condition = obj.AddComponent<ConditionComponent>();
            foreach (var animCondition in conditions)
            {
                var (handGestureCondition, parameterCondition) = animCondition.ToCondition(_parameterTypes, (parameter) => Debug.LogWarning($"failed to find parameter: {parameter}"));
                if (handGestureCondition != null)
                {
                    condition.HandGestureConditions.Add(handGestureCondition);
                }
                if (parameterCondition != null)
                {
                    condition.ParameterConditions.Add(parameterCondition);
                }
            }
        }

        var expression = obj.AddComponent<ExpressionComponent>();
        var (eye, mouth) = _platformSupport.GetTrackingPermission(state) ?? (TrackingPermission.Disallow, TrackingPermission.Allow);
        expression.FacialSettings = new FacialSettings()
        {
            AllowEyeBlink = eye,
            AllowLipSync = mouth,
            EnableBlending = isBlending,
            AdvancedEyBlinkSettings = AdvancedEyeBlinkSettings.Disabled()
        };

        return obj;
    }

    private (List<BlendShapeWeightAnimation> facialAnimations, List<GenericAnimation> nonFacialAnimations) AnalyzeAnimationClip(AnimationClip clip)
    {
        var facialAnimations = new List<BlendShapeWeightAnimation>();
        var nonFacialAnimations = new List<GenericAnimation>();

        var curveBindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
        foreach (var binding in curveBindings)
        {
            var serializableCurveBinding = SerializableCurveBinding.FromEditorCurveBinding(binding);
            var curve = UnityEditor.AnimationUtility.GetEditorCurve(clip, binding);
            if (binding.type == typeof(SkinnedMeshRenderer) &&
                binding.path.ToLower() == _context.BodyPath.ToLower() &&
                binding.propertyName.StartsWith(FaceTuneConstants.AnimatedBlendShapePrefix))
            {
                var name = binding.propertyName.Replace(FaceTuneConstants.AnimatedBlendShapePrefix, string.Empty);
                var animation = new BlendShapeWeightAnimation(name, curve);
                facialAnimations.Add(animation);
            }
            else
            {
                nonFacialAnimations.Add(new GenericAnimation(serializableCurveBinding, curve));
            }
        }
        var objectReferenceBindings = UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(clip);
        foreach (var binding in objectReferenceBindings)
        {
            var serializableCurveBinding = SerializableCurveBinding.FromEditorCurveBinding(binding);
            var objectReferenceCurve = UnityEditor.AnimationUtility.GetObjectReferenceCurve(clip, binding);
            var serializableObjectReferenceCurve = objectReferenceCurve.Select(SerializableObjectReferenceKeyframe.FromEditorObjectReferenceKeyframe);
            nonFacialAnimations.Add(new GenericAnimation(serializableCurveBinding, serializableObjectReferenceCurve.ToList()));
        }
        return (facialAnimations, nonFacialAnimations);
    }

    private void AddFacialData(GameObject obj, List<BlendShapeWeightAnimation> facialAnimations)
    {
        var facialData = obj.AddComponent<FacialDataComponent>();
        facialData.SourceMode = AnimationSourceMode.Manual;
        facialData.BlendShapeAnimations = facialAnimations;
    }

    private void AddAnimationData(GameObject obj, List<GenericAnimation> nonFacialAnimations)
    {
        var animationData = obj.AddComponent<AnimationDataComponent>();
        animationData.Clip = CreateClip(new AnimationSet(nonFacialAnimations));
    }

    private AnimationClip CreateClip(AnimationSet animations)
    {
        if (_clipMap.TryGetValue(animations, out var clip))
        {
            return clip;
        }
        var newClip = new AnimationClip();
        newClip.name = GetClipName(animations);
        newClip.AddGenericAnimations(animations.Animations);
        _clipMap.Add(animations, newClip);
		var folderPath = GetClippath();
		var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{newClip.name}.anim");
		AssetDatabase.CreateAsset(newClip, assetPath);
        return newClip;
    }

    private static string GetClipName(AnimationSet animations)
    {
        var type = animations.Animations
            .GroupBy(a => a.CurveBinding.Type?.Name ?? "UnknownType")
            .OrderByDescending(g => g.Count())
            .First().Key;

        var obj = animations.Animations
            .GroupBy(a => !string.IsNullOrEmpty(a.CurveBinding.Path) ? a.CurveBinding.Path.Split('/').Last() : "Root")
            .OrderByDescending(g => g.Count())
            .First().Key;

        return $"{type}_{obj}";
    }

	private string _clipFolderPath = "";
	private string GetClippath()
	{
		if (string.IsNullOrEmpty(_clipFolderPath))
		{
			var absolutePath = EditorUtility.OpenFolderPanel("表情以外のアニメーションが検知されました。アニメーションクリップの保存先を選択してください", "Assets", "");
			if (string.IsNullOrEmpty(absolutePath))
			{
				throw new Exception("アニメーションクリップの保存先が選択されていません。");
			}
			var relativePath = FileUtil.GetProjectRelativePath(absolutePath);
			if (string.IsNullOrEmpty(relativePath) || !relativePath.StartsWith("Assets"))
			{
				throw new Exception("プロジェクト内のフォルダ（Assets配下）を選択してください。");
			}
			_clipFolderPath = relativePath.Replace("\\", "/");
		}
		return _clipFolderPath;
	}
}