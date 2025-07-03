using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace aoyon.facetune.ui;

[CanEditMultipleObjects]
[CustomEditor(typeof(AnimationDataComponent))]
internal class AnimationDataEditor : FaceTuneCustomEditorBase<AnimationDataComponent>
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if (GUILayout.Button("Convert to Manual"))
        {
            ConvertToManual(targets);
        }
    }

    internal static void ConvertToManual(Object[] targets)
    {
        var components = targets.Select(t => t as AnimationDataComponent).OfType<AnimationDataComponent>().ToArray();
        foreach (var component in components)
        {
            var animations = new List<GenericAnimation>();
            component.ClipToManual(animations);

            var so = new SerializedObject(component);
            so.Update();
            CustomEditorUtility.AddGenericAnimations(so.FindProperty(nameof(AnimationDataComponent.Animations)), animations);
            so.FindProperty(nameof(AnimationDataComponent.SourceMode)).enumValueIndex = (int)AnimationSourceMode.Manual;
            so.ApplyModifiedProperties();
        }
    }
}

/*
// Todo: Refactor
[CanEditMultipleObjects]
[CustomEditor(typeof(AnimationExpressionComponent))]
internal class AnimationExpressionEditor : FaceTuneCustomEditorBase<AnimationExpressionComponent>
{
    private PropertyField? _curveBindingField;
    private PropertyField? _curveField;
    private PropertyField? _objectReferenceCurveField;
    private bool _showExpressionSettings = false;
    private bool _underMerge = false;

    public override void OnEnable()
    {
        base.OnEnable();
        _underMerge = Component.GetComponentInParent<MergeExpressionComponent>() != null;
    }
    
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        root.Bind(serializedObject);

        var sourceModeField = new PropertyField(serializedObject.FindProperty(nameof(AnimationExpressionComponent.SourceMode)));
        root.Add(sourceModeField);

        var manualContent = new VisualElement();
        var clipContent = new VisualElement();

        var genericAnimationsProp = serializedObject.FindProperty(nameof(AnimationExpressionComponent.GenericAnimations));
        var genericAnimationsListView = new ListView
        {
            bindingPath = genericAnimationsProp.propertyPath,
            headerTitle = "Generic Animations",
            showBorder = true,
            showAddRemoveFooter = true,
            showBoundCollectionSize = false,
            reorderable = false,
            selectionType = SelectionType.Single,
            fixedItemHeight = EditorGUIUtility.singleLineHeight,
            style =
            {
                maxHeight = 200
            }
        };

        genericAnimationsListView.makeItem = () => new Label { style = { unityTextAlign = TextAnchor.MiddleLeft, paddingLeft = 5 }};

        genericAnimationsListView.bindItem = (element, i) =>
        {
            var label = (Label)element;
            var elementProp = genericAnimationsProp.GetArrayElementAtIndex(i);
            var curveBindingProp = elementProp.FindPropertyRelative(GenericAnimation.CurveBindingPropName);
            var propertyNameProp = curveBindingProp.FindPropertyRelative(SerializableCurveBinding.PropertyNamePropName);
            label.text = propertyNameProp.stringValue;
        };
        manualContent.Add(genericAnimationsListView);

        var selectedAnimationDetails = new VisualElement
        {
            style =
            {
                marginTop = 10,
                borderLeftColor = Color.grey,
                borderLeftWidth = 1,
                paddingLeft = 5
            }
        };

        _curveBindingField = new PropertyField();
        _curveField = new PropertyField();
        _objectReferenceCurveField = new PropertyField();

        selectedAnimationDetails.Add(_curveBindingField);
        selectedAnimationDetails.Add(_curveField);
        selectedAnimationDetails.Add(_objectReferenceCurveField);
        manualContent.Add(selectedAnimationDetails);

        var clipField = new PropertyField(serializedObject.FindProperty(nameof(AnimationExpressionComponent.Clip)));
        clipContent.Add(clipField);

        genericAnimationsListView.selectionChanged += _ =>
        {
            var selectedIndex = genericAnimationsListView.selectedIndex;
            if (selectedIndex >= 0 && selectedIndex < genericAnimationsProp.arraySize)
            {
                var selectedProp = genericAnimationsProp.GetArrayElementAtIndex(selectedIndex);
                UpdateSelectedAnimationDetails(selectedProp);
            }
            else
            {
                UpdateSelectedAnimationDetails(null);
            }
        };

        var toggleAnimationWindowButton = new Button(() =>
        {
            if (GenericAnimationEditor.IsEditing())
            {
                GenericAnimationEditor.StopEditing();
            }
            else
            {
                OpenGenericAnimationsEditor();
            }
        });
        
        toggleAnimationWindowButton.schedule.Execute(() =>
        {
            toggleAnimationWindowButton.text = GenericAnimationEditor.IsEditing() ? "Stop Animation Window" : "Start Animation Window";
        }).Every(200);

        var convertToManualButton = new Button(ConvertToManual) { text = "Convert to Manual" };
        clipContent.Add(convertToManualButton);

        void UpdateButtonStates()
        {
            var hasClip = Component.Clip != null;
            convertToManualButton.SetEnabled(hasClip);
            toggleAnimationWindowButton.SetEnabled(hasClip || Component.SourceMode == AnimationSourceMode.Manual);
        }

        clipField.RegisterValueChangeCallback(_ => UpdateButtonStates());
        UpdateButtonStates();

        root.Add(manualContent);
        root.Add(clipContent);
        root.Add(toggleAnimationWindowButton);

        var expressionSettingsProp = serializedObject.FindProperty(nameof(AnimationExpressionComponent.ExpressionSettings));
        root.Add(new IMGUIContainer(() =>
        {
            serializedObject.Update();
            _showExpressionSettings = EditorGUILayout.Foldout(_showExpressionSettings, "Advanced");
            if (_showExpressionSettings)
            {
                EditorGUI.indentLevel++;
                GUI.enabled = !_underMerge;
                var sourceMode = (AnimationSourceMode)serializedObject.FindProperty(nameof(AnimationExpressionComponent.SourceMode)).enumValueIndex;
                if (sourceMode == AnimationSourceMode.Manual)
                {
                    ExpressionSettingsDrawer.Draw(expressionSettingsProp);
                }
                else
                {
                    ExpressionSettingsDrawer.DrawMotionTimeParameterName(expressionSettingsProp);
                }
                GUI.enabled = true;
                EditorGUI.indentLevel--;
            }
            serializedObject.ApplyModifiedProperties();
        }));

        void UpdateVisibility(AnimationSourceMode mode)
        {
            manualContent.style.display = mode == AnimationSourceMode.Manual ? DisplayStyle.Flex : DisplayStyle.None;
            clipContent.style.display = mode != AnimationSourceMode.Manual ? DisplayStyle.Flex : DisplayStyle.None;
            
            if (mode == AnimationSourceMode.Manual)
            {
                if (genericAnimationsProp.arraySize > 0 && genericAnimationsListView.selectedIndex < 0)
                {
                    genericAnimationsListView.selectedIndex = 0;
                }
                else if (genericAnimationsListView.selectedIndex >= 0 && genericAnimationsListView.selectedIndex < genericAnimationsProp.arraySize)
                {
                    UpdateSelectedAnimationDetails(genericAnimationsProp.GetArrayElementAtIndex(genericAnimationsListView.selectedIndex));
                }
                else
                {
                    UpdateSelectedAnimationDetails(null);
                }
            }
            else
            {
                UpdateSelectedAnimationDetails(null);
            }
        }

        sourceModeField.RegisterValueChangeCallback(evt =>
        {
            var mode = (AnimationSourceMode)evt.changedProperty.enumValueIndex;
            UpdateVisibility(mode);
            UpdateButtonStates();
        });

        UpdateVisibility((AnimationSourceMode)serializedObject.FindProperty(nameof(AnimationExpressionComponent.SourceMode)).enumValueIndex);

        return root;
    }

    private void UpdateSelectedAnimationDetails(SerializedProperty? selectedProp)
    {
        var detailsContainer = _curveBindingField?.parent;
        if (detailsContainer == null) return;

        if (selectedProp == null)
        {
            detailsContainer.style.display = DisplayStyle.None;
            return;
        }

        detailsContainer.style.display = DisplayStyle.Flex;

        _curveBindingField.BindProperty(selectedProp.FindPropertyRelative(GenericAnimation.CurveBindingPropName));
        _curveField.BindProperty(selectedProp.FindPropertyRelative(GenericAnimation.CurvePropName));
        _objectReferenceCurveField.BindProperty(selectedProp.FindPropertyRelative(GenericAnimation.ObjectReferenceCurvePropName));
    }

    private void OpenGenericAnimationsEditor()
    {
        if (!CustomEditorUtility.TryGetContext(Component.gameObject, out var context)) return;
        var animator = context.Root.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("Animator component not found on the avatar root.");
            return;
        }

        if (Component.SourceMode == AnimationSourceMode.Manual)
        {
            Action<AnimationClip> onClipModified = clip =>
            {
                Undo.RecordObject(Component, "Update Animations from Editor");
                Component.GenericAnimations = GenericAnimation.FromAnimationClip(clip).ToList();
            };
            GenericAnimationEditor.StartEditingWithAnimations(animator, Component.GenericAnimations, onClipModified);
        }
        else
        {
            if (Component.Clip == null)
            {
                Debug.LogWarning("No Animation Clip assigned.");
                return;
            }

            GenericAnimationEditor.StartEditing(animator, Component.Clip);
        }
    }

    [MenuItem($"CONTEXT/{nameof(AnimationExpressionComponent)}/ToClip")]
    private static void ToClip(MenuCommand command)
    {
        var component = (command.context as AnimationExpressionComponent)!;
        CustomEditorUtility.ToClip(component.GenericAnimations);
    }

    // Todo: use SerializedObject
    private void ConvertToManual()
    {
        var component = (target as AnimationExpressionComponent)!;
        var clip = component.Clip;
        if (clip == null) return;

        Undo.RecordObject(component, "Convert To Manual");
        component.GenericAnimations = GenericAnimation.FromAnimationClip(clip).ToList();
        component.SourceMode = AnimationSourceMode.Manual;
        component.ExpressionSettings = ExpressionSettings.FromAnimationClip(clip);
        EditorUtility.SetDirty(component);
    }
}

*/