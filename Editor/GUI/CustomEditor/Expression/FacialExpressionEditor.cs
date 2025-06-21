namespace com.aoyon.facetune.ui;

using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(FacialExpressionComponent))]
internal class FacialExpressionEditor : FaceTuneCustomEditorBase<FacialExpressionComponent>
{
    private SerializedProperty _facialSettingsProperty = null!;
    private SerializedProperty _sourceModeProperty = null!;
    private SerializedProperty _isSingleFrameProperty = null!;
    private SerializedProperty _blendShapeAnimationsProperty = null!;
    private SerializedProperty _clipProperty = null!;
    private SerializedProperty _clipExcludeOptionProperty = null!;
    private SerializedProperty _expressionSettingsProperty = null!;

    private ReorderableList? _blendShapeAnimationList;

    private bool _underMerge = false;

    public override void OnEnable()
    {
        base.OnEnable();
        _facialSettingsProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.FacialSettings));
        _sourceModeProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.SourceMode));
        _isSingleFrameProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.IsSingleFrame));
        _blendShapeAnimationsProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.BlendShapeAnimations));
        _clipProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.Clip));
        _clipExcludeOptionProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.ClipExcludeOption));
        _expressionSettingsProperty = serializedObject.FindProperty(nameof(FacialExpressionComponent.ExpressionSettings));
        _blendShapeAnimationList = null;

        _underMerge = Component.GetComponentInParent<MergeExpressionComponent>() != null;
    }
    
    private void SetupReorderableList()
    {
        _blendShapeAnimationList = new ReorderableList(serializedObject, _blendShapeAnimationsProperty, false, true, true, true);

        _blendShapeAnimationList.drawHeaderCallback = rect =>
        {
            var headerText = _isSingleFrameProperty.boolValue ? "BlendShapes (Single Frame)" : "BlendShapes (Animation)";
            EditorGUI.LabelField(rect, headerText);
        };

        _blendShapeAnimationList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            var element = _blendShapeAnimationList.serializedProperty.GetArrayElementAtIndex(index);
            rect.y += 2;

            var nameProp = element.FindPropertyRelative(BlendShapeAnimation.NamePropName);
            var curveProp = element.FindPropertyRelative(BlendShapeAnimation.CurvePropName);

            var nameRect = new Rect(rect.x, rect.y, rect.width * 0.35f - 10, EditorGUIUtility.singleLineHeight);
            var valueRect = new Rect(rect.x + rect.width * 0.35f + 10, rect.y, rect.width * 0.65f - 20, EditorGUIUtility.singleLineHeight);

            EditorGUI.PropertyField(nameRect, nameProp, GUIContent.none);

            if (_isSingleFrameProperty.boolValue)
            {
                var curve = curveProp.animationCurveValue;
                var value = curve != null && curve.keys.Length > 0 ? curve.Evaluate(0) : 0;
                
                EditorGUI.BeginChangeCheck();
                var newValue = EditorGUI.Slider(valueRect, value, 0, 100);
                if (EditorGUI.EndChangeCheck())
                {
                    var newCurve = new AnimationCurve();
                    newCurve.AddKey(0, newValue);
                    curveProp.animationCurveValue = newCurve;
                }
            }
            else
            {
                EditorGUI.PropertyField(valueRect, curveProp, GUIContent.none);
            }
        };
    }

    private bool _showExpressionSettings = false;
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        GUI.enabled = !_underMerge;
        EditorGUILayout.PropertyField(_facialSettingsProperty);
        GUI.enabled = true;
        EditorGUILayout.PropertyField(_sourceModeProperty);
        var sourceMode = (AnimationSourceMode)_sourceModeProperty.enumValueIndex;
        switch (sourceMode)
        {
            case AnimationSourceMode.Manual:
                DrawManualModeGUI();
                break;
            case AnimationSourceMode.FromAnimationClip:
                DrawFromAnimationClipModeGUI();
                break;
        }
        serializedObject.ApplyModifiedProperties();
    }
    
    private void DrawManualModeGUI()
    {
        EditorGUILayout.PropertyField(_isSingleFrameProperty);

        if (_blendShapeAnimationList == null)
        {
            SetupReorderableList();
        }
        _blendShapeAnimationList!.DoLayoutList();
        
        if (_isSingleFrameProperty.boolValue)
        {
            if (GUILayout.Button("Open Editor"))
            {
                OpenFacialShapesEditor();
            }
        }
        else
        {
            var isEditing = GenericAnimationEditor.IsEditing();
            var label = isEditing ? "Stop Animation Window" : "Start Animation Window";
            if (GUILayout.Button(label))
            {
                if (isEditing)
                {
                    GenericAnimationEditor.StopEditing();
                }
                else
                {
                    StartAnimationWindow();
                }
            }
            _showExpressionSettings = EditorGUILayout.Foldout(_showExpressionSettings, "Advanced");
            if (_showExpressionSettings)
            {
                EditorGUI.indentLevel++;
                GUI.enabled = !_underMerge;
                ExpressionSettingsDrawer.Draw(_expressionSettingsProperty);
                GUI.enabled = true;
                EditorGUI.indentLevel--;
            }
        }
    }
    
    private void DrawFromAnimationClipModeGUI()
    {
        EditorGUILayout.PropertyField(_clipProperty);
        EditorGUILayout.PropertyField(_clipExcludeOptionProperty);
        
        if (GUILayout.Button("Convert to Manual"))
        {
            ConvertToManual();
        }
        _showExpressionSettings = EditorGUILayout.Foldout(_showExpressionSettings, "Advanced");
        if (_showExpressionSettings)
        {
            EditorGUI.indentLevel++;
            GUI.enabled = !_underMerge;
            ExpressionSettingsDrawer.DrawMotionTimeParameterName(_expressionSettingsProperty);
            GUI.enabled = true;
            EditorGUI.indentLevel--;
        }
    }

    private void OpenFacialShapesEditor()
    {
        if (!CustomEditorUtility.TryGetContext(Component.gameObject, out var context)) return;
        var defaultBlendShapes = context.DEC.GetDefaultBlendShapeSet(Component.gameObject);
        var shapes = new List<BlendShape>();
        Component.GetFirstFrameBlendShapeSet(context, shapes);
        var window = FacialShapesEditor.OpenEditor(context.FaceRenderer, context.FaceMesh, defaultBlendShapes, new(shapes));
        if (window == null) return;
        window.RegisterApplyCallback(RecieveEditorResult);
    }

    private void RecieveEditorResult(BlendShapeSet result)
    {
        var so = new SerializedObject(Component);
        so.Update();
        FacialExpressionEditorUtility.AddShapesAsSingleFrame(so, result.BlendShapes.ToList());
        so.ApplyModifiedProperties();
    }

    private void StartAnimationWindow()
    {
        if (!CustomEditorUtility.TryGetContext(Component.gameObject, out var context)) return;
        var animator = context.Root.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("Animator component not found on the avatar root.");
            return;
        }

        Action<AnimationClip> onClipModified = clip =>
        {
            Undo.RecordObject(Component, "Update Animations from Editor");
            Component.BlendShapeAnimations = GenericAnimation.FromAnimationClip(clip).Select(a =>
            {
                if (a.TryToBlendShapeAnimation(out var animation))
                {
                    return animation;
                }
                return null;
            }).OfType<BlendShapeAnimation>().ToList();
        };
        GenericAnimationEditor.StartEditingWithAnimations(animator, Component.BlendShapeAnimations.Select(a => a.ToGeneric(context.BodyPath)).ToList(), onClipModified);
    }


    [MenuItem($"CONTEXT/{nameof(FacialExpressionComponent)}/ToClip")]
    private static void ToClip(MenuCommand command)
    {
        var component = (command.context as FacialExpressionComponent)!;
        CustomEditorUtility.ToClip(component.gameObject, context =>
        {
            var shapes = new List<BlendShape>();
            component.GetFirstFrameBlendShapeSet(context, shapes);
            return shapes;
        });
    }

    private void ConvertToManual()
    {
        var components = targets.Select(t => t as FacialExpressionComponent).OfType<FacialExpressionComponent>().ToArray();
        foreach (var component in components)
        {
            if (component.Clip == null) continue;
            if (!CustomEditorUtility.TryGetContext(component.gameObject, out var context)) continue;
            var defaultSet = context.DEC.GetDefaultBlendShapeSet(component.gameObject);
            var shapes = new List<BlendShape>();
            component.GetBlendShapes(shapes, defaultSet);
            var so = new SerializedObject(component);
            so.Update();
            FacialExpressionEditorUtility.AddShapesAsSingleFrame(so, shapes, true);
            so.FindProperty(nameof(FacialExpressionComponent.SourceMode)).enumValueIndex = (int)AnimationSourceMode.Manual;
            so.FindProperty(nameof(FacialExpressionComponent.IsSingleFrame)).boolValue = true;
            var expressionSettingsProperty = so.FindProperty(nameof(FacialExpressionComponent.ExpressionSettings));
            var settings = ExpressionSettings.FromAnimationClip(component.Clip);
            expressionSettingsProperty.FindPropertyRelative(ExpressionSettings.LoopTimePropName).boolValue = settings.LoopTime;
            expressionSettingsProperty.FindPropertyRelative(ExpressionSettings.MotionTimeParameterNamePropName).stringValue = settings.MotionTimeParameterName;
            so.ApplyModifiedProperties();
        }
    }
}

public class FacialExpressionEditorUtility
{
    public static void ClearAnimations(SerializedObject so)
    {
        so.FindProperty(nameof(FacialExpressionComponent.BlendShapeAnimations)).arraySize = 0;
    }

    public static void AddShapesAsSingleFrame(SerializedObject so, IReadOnlyList<BlendShape> newShapes, bool clear = false)
    {
        var animations = newShapes.Select(shape => BlendShapeAnimation.SingleFrame(shape.Name, shape.Weight)).ToList();
        if (clear)
        {
            ClearAnimations(so);
        }
        var animationsProperty = so.FindProperty(nameof(FacialExpressionComponent.BlendShapeAnimations));
        animationsProperty.arraySize = animations.Count;
        for (var i = 0; i < animations.Count; i++)
        {
            var element = animationsProperty.GetArrayElementAtIndex(i);
            var nameProp = element.FindPropertyRelative(BlendShapeAnimation.NamePropName);
            var curveProp = element.FindPropertyRelative(BlendShapeAnimation.CurvePropName);
            nameProp.stringValue = animations[i].Name;
            curveProp.animationCurveValue = animations[i].Curve;
        }
    }

    public static void AddAnimations(SerializedObject so, IReadOnlyList<BlendShapeAnimation> newAnimations, bool clear = false)
    {
        if (clear)
        {
            ClearAnimations(so);
        }
        var animationsProperty = so.FindProperty(nameof(FacialExpressionComponent.BlendShapeAnimations));
        animationsProperty.arraySize = newAnimations.Count;
        for (var i = 0; i < newAnimations.Count; i++)
        {
            var element = animationsProperty.GetArrayElementAtIndex(i);
            element.FindPropertyRelative(BlendShapeAnimation.NamePropName).stringValue = newAnimations[i].Name;
            element.FindPropertyRelative(BlendShapeAnimation.CurvePropName).animationCurveValue = newAnimations[i].Curve;
        }
    }
}
