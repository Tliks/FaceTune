using aoyon.facetune.gui.shapes_editor;

namespace aoyon.facetune.gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(FacialDataComponent))]
internal class FacialDataEditor : FaceTuneCustomEditorBase<FacialDataComponent>
{
    private SerializedProperty _sourceModeProperty = null!;
    private SerializedProperty _blendShapeAnimationsProperty = null!;
    private SerializedProperty _clipProperty = null!;
    private SerializedProperty _clipOptionProperty = null!;

    public override void OnEnable()
    {
        base.OnEnable();
        _sourceModeProperty = serializedObject.FindProperty(nameof(FacialDataComponent.SourceMode));
        _blendShapeAnimationsProperty = serializedObject.FindProperty(nameof(FacialDataComponent.BlendShapeAnimations));
        _clipProperty = serializedObject.FindProperty(nameof(FacialDataComponent.Clip));
        _clipOptionProperty = serializedObject.FindProperty(nameof(FacialDataComponent.ClipOption));
    }

    private static readonly string[] SourceModeNames = {nameof(AnimationSourceMode.Manual), nameof(AnimationSourceMode.AnimationClip) };
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawSourceModeGUI();

        EditorGUILayout.Space();

        GUI.enabled = _sourceModeProperty.enumValueIndex == (int)AnimationSourceMode.Manual;
        DrawManualModeGUI();
        GUI.enabled = true;

        EditorGUILayout.Space();

        GUI.enabled = _sourceModeProperty.enumValueIndex == (int)AnimationSourceMode.AnimationClip;
        DrawFromAnimationClipModeGUI();
        GUI.enabled = true;

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSourceModeGUI()
    {
        EditorGUI.BeginChangeCheck();
        var newSourceMode = GUILayout.Toolbar(_sourceModeProperty.enumValueIndex, SourceModeNames);
        if (EditorGUI.EndChangeCheck())
        {
            _sourceModeProperty.enumValueIndex = newSourceMode;
        }
    }

    private void DrawManualModeGUI()
    {
        EditorGUILayout.PropertyField(_blendShapeAnimationsProperty);

        EditorGUILayout.Space();
        if (GUILayout.Button("Open Editor"))
        {
            OpenEditor();
        }
    }

    private void DrawFromAnimationClipModeGUI()
    {
        EditorGUILayout.PropertyField(_clipProperty);
        EditorGUILayout.PropertyField(_clipOptionProperty);

        EditorGUILayout.Space();
        if (GUILayout.Button("Convert to Manual"))
        {
            var components = targets.Select(t => t as FacialDataComponent).OfType<FacialDataComponent>().ToArray();
            ConvertToManual(components);
        }
    }

    private void OpenEditor()
    {
        var facialStyleAnimations = new List<BlendShapeAnimation>();
        FacialStyleContext.TryGetFacialStyleAnimations(Component.gameObject, facialStyleAnimations);
        var defaultOverride = new BlendShapeSet();
        Component.GetBlendShapes(defaultOverride, facialStyleAnimations);
        var firstFrameBlendShapes = facialStyleAnimations.ToFirstFrameBlendShapes();
        CustomEditorUtility.OpenEditor(Component.gameObject, new FacialDataTargeting(){ Target = Component }, defaultOverride, new BlendShapeSet(firstFrameBlendShapes));
    }

    internal static void ConvertToManual(FacialDataComponent[] components)
    {
        foreach (var component in components)
        {
            ConvertToManual(component);
        }
    }

    internal static bool ConvertToManual(FacialDataComponent component)
    {
        var animations = new List<BlendShapeAnimation>();
        component.ClipToManual(animations);
        if (animations.Count == 0)
        {
            return false;
        }

        var so = new SerializedObject(component);
        so.Update();
        CustomEditorUtility.AddBlendShapeAnimations(so.FindProperty(nameof(FacialDataComponent.BlendShapeAnimations)), animations);
        so.FindProperty(nameof(FacialDataComponent.SourceMode)).enumValueIndex = (int)AnimationSourceMode.Manual;
        so.ApplyModifiedProperties();
        return true;
    }

    [MenuItem($"CONTEXT/{nameof(FacialDataComponent)}/Export as Clip")]
    private static void ExportAsClip(MenuCommand command)
    {
        var component = (command.context as FacialDataComponent)!;
        ExportFacialDataWindow.OpenWindow(component);
    }
}

internal class ExportFacialDataWindow : EditorWindow
{
    private FacialDataComponent _component = null!;

    private bool _addZeroWeight = false;
    private bool _addFacialStyle = false;
    private bool _excludeTrackedShapes = true;

    private const int WindowWidth = 300;
    private const int WindowHeight = 100;

    public static void OpenWindow(FacialDataComponent component)
    {
        var window = GetWindow<ExportFacialDataWindow>();
        window._component = component;
        window.maxSize = new Vector2(WindowWidth, WindowHeight);
        window.Show();
    }

    private void OnGUI()
    {
        _addZeroWeight = EditorGUILayout.Toggle("Add Zero Weight", _addZeroWeight);
        _addFacialStyle = EditorGUILayout.Toggle("Add Facial Style", _addFacialStyle);
        _excludeTrackedShapes = EditorGUILayout.Toggle("Exclude Tracked Shapes", _excludeTrackedShapes);

        if (GUILayout.Button("Export"))
        {
            Export();
            Close();
        }
    }

    private void Export()
    {
        var animations = new AnimationIndex();
        if (!SessionContextBuilder.TryBuild(_component.gameObject, out var context, out var result))
        {
            Debug.LogError($"Failed to build session context: {result}");
            return;
        }
        if (_addZeroWeight)
        {
            animations.AddRange(context.ZeroBlendShapes.ToGenericAnimations(context.BodyPath));
        }
        if (_addFacialStyle)
        {
            var facialStyleAnimations = new List<BlendShapeAnimation>();
            if (FacialStyleContext.TryGetFacialStyleAnimations(_component.gameObject, facialStyleAnimations))
            {
                animations.AddRange(facialStyleAnimations.ToGenericAnimations(context.BodyPath));
            }
        }
        animations.AddRange(_component.GetAnimations(context));
        if (_excludeTrackedShapes)
        {
            animations.RemoveBlendShapes(context.TrackedBlendShapes);
        }
        CustomEditorUtility.SaveAsClip(clip =>
        {
            clip.SetGenericAnimations(animations);
        });
    }
}

/*
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

        _underMerge = Component.GetComponentInParent<MergeExpressionComponent>(true) != null;
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
        if (!CustomEditorUtility.TryGetContextWithDEC(Component.gameObject, out var context, out var dec)) return;
        var defaultBlendShapes = dec.GetDefaultBlendShapeSet(Component.gameObject);
        var shapes = new List<BlendShape>();
        Component.GetMergedBlendShapeSet(dec, shapes);
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
        if (!CustomEditorUtility.TryGetContextWithDEC(component.gameObject, out var context, out var dec)) return;
        var shapes = new List<BlendShape>();
        component.GetMergedBlendShapeSet(dec, shapes);
        CustomEditorUtility.ToClip(context.BodyPath, shapes);
    }

    private void ConvertToManual()
    {
        var components = targets.Select(t => t as FacialExpressionComponent).OfType<FacialExpressionComponent>().ToArray();
        foreach (var component in components)
        {
            if (component.Clip == null) continue;
            if (!CustomEditorUtility.TryGetContextWithDEC(component.gameObject, out var context, out var dec)) continue;
            var defaultSet = dec.GetDefaultBlendShapeSet(component.gameObject);
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

*/