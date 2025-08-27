    using Aoyon.FaceTune.Gui.ShapesEditor;

namespace Aoyon.FaceTune.Gui;

[CanEditMultipleObjects]
[CustomEditor(typeof(ExpressionDataComponent))]
internal class ExpressionDataEditor : FaceTuneIMGUIEditorBase<ExpressionDataComponent>
{
    private SessionContext? _context;

    private SerializedProperty _blendShapeAnimationsProperty = null!;
    private SerializedProperty _clipProperty = null!;
    private SerializedProperty _clipOptionProperty = null!;
    private SerializedProperty _allBlendShapeAnimationAsFacialProperty = null!;
    private LocalizedPopup _clipOptionPopup = null!;

    private int _facialClipAnimationCount = 0;
    private int _nonFacialClipAnimationCount = 0;
    private string[] _missingBlendShapeNames = null!;

    public override void OnEnable()
    {
        base.OnEnable();
        CustomEditorUtility.TryGetContext(Component.gameObject, out _context);
        _blendShapeAnimationsProperty = serializedObject.FindProperty(nameof(ExpressionDataComponent.BlendShapeAnimations));
        _clipProperty = serializedObject.FindProperty(nameof(ExpressionDataComponent.Clip));
        _clipOptionProperty = serializedObject.FindProperty(nameof(ExpressionDataComponent.ClipOption));
        _allBlendShapeAnimationAsFacialProperty = serializedObject.FindProperty(nameof(ExpressionDataComponent.AllBlendShapeAnimationAsFacial));
        UpdateInfo();
        _clipOptionPopup = new LocalizedPopup(typeof(ClipImportOption));
        Undo.undoRedoPerformed += UpdateInfo;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        Undo.undoRedoPerformed -= UpdateInfo;
    }

    protected override void OnInnerInspectorGUI()
    {   
        DrawMissingBlendShapeGUI();
        EditorGUILayout.Space();
        DrawAnimationClipGUI();
        EditorGUILayout.Space();
        DrawManualGUI();
        EditorGUILayout.Space();
        DrawAdvancedOptionsGUI();
    }

    private void DrawMissingBlendShapeGUI()
    {
        if (_missingBlendShapeNames.Length == 0) return;
        EditorGUILayout.HelpBox($"{Localization.S($"{ComponentName}:MissingBlendShapes")}: {string.Join(", ", _missingBlendShapeNames)}", MessageType.Warning);
    }

    private void DrawAnimationClipGUI()
    {
        EditorGUILayout.LabelField(Localization.G($"{ComponentName}:AnimationClipMode"), EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        
        EditorGUILayout.BeginHorizontal();
        LocalizedPropertyField(_clipProperty);
        if (GUILayout.Button(Localization.G($"{ComponentName}:Import"), GUILayout.Width(60)))
        {
            var components = targets.Select(t => t as ExpressionDataComponent).OfType<ExpressionDataComponent>().ToArray();
            var importer = new ExpressionDataClipImporter();
            importer.ImportClip(components);
        }
        EditorGUILayout.EndHorizontal();

        _clipOptionPopup.Field(_clipOptionProperty);

        if (EditorGUI.EndChangeCheck())
        {
            EditorApplication.delayCall += UpdateInfo;
        }

        var clipInfoText = $"{Localization.G($"{ComponentName}:Clip:FacialAnimationCount")}: {_facialClipAnimationCount}, {Localization.G($"{ComponentName}:Clip:NonFacialAnimationCount")}: {_nonFacialClipAnimationCount}";
        EditorGUILayout.HelpBox(clipInfoText, MessageType.Info);
    }

    private void DrawManualGUI()
    {        
        LocalizedPropertyField(_blendShapeAnimationsProperty);

        EditorGUILayout.Space();
        if (GUILayout.Button(Localization.G($"{ComponentName}:OpenEditor")))
        {
            OpenEditor();
        }
    }

    private bool _showAdvancedOptions = false;
    private void DrawAdvancedOptionsGUI()
    {
        _showAdvancedOptions = EditorGUILayout.Foldout(_showAdvancedOptions, Localization.G($"{ComponentName}:AdvancedOptions"));
        if (_showAdvancedOptions)
        {
            EditorGUI.indentLevel++;
            LocalizedPropertyField(_allBlendShapeAnimationAsFacialProperty);
            EditorGUI.indentLevel--;
        }
    }

    private void UpdateInfo()
    {
        if (_context == null)
        {
            _facialClipAnimationCount = 0;
            _nonFacialClipAnimationCount = 0;
            _missingBlendShapeNames = new string[0];
            return;
        }

        var (facialAnimations, nonFacialAnimations) = Component.ProcessClip(_context.BodyPath);

        _facialClipAnimationCount = facialAnimations.Count;
        _nonFacialClipAnimationCount = nonFacialAnimations.Count;

        var allBlendShapes = _context.ZeroBlendShapes
            .Select(x => x.Name)
            .ToHashSet();
        _missingBlendShapeNames = Component.BlendShapeAnimations.Concat(facialAnimations)
            .Distinct()
            .Select(x => x.Name)
            .Where(x => !string.IsNullOrEmpty(x) && !allBlendShapes.Contains(x))
            .ToArray();
    }

    private void OpenEditor()
    {
        if (!CustomEditorUtility.TryGetContext(Component.gameObject, out var context)) throw new InvalidOperationException("Context not found");
        var bodyPath = context.BodyPath;
        var facialStyleAnimations = new List<BlendShapeWeightAnimation>();
        FacialStyleContext.TryGetFacialStyleAnimations(Component.gameObject, facialStyleAnimations);

        var defaultOverride = new BlendShapeSet();
        defaultOverride.AddRange(Component.BlendShapeAnimations.ToFirstFrameBlendShapes());

        var baseSet = new BlendShapeSet();
        baseSet.AddRange(facialStyleAnimations.ToFirstFrameBlendShapes());
        baseSet.AddRange(Component.ProcessClip(bodyPath).facialAnimations.ToFirstFrameBlendShapes());

        CustomEditorUtility.OpenEditor(Component.gameObject, new ExpressionDataTargeting(){ Target = Component }, defaultOverride, baseSet);
    }
    

    [MenuItem($"CONTEXT/{nameof(ExpressionDataComponent)}/Export as Clip")]
    private static void ExportAsClip(MenuCommand command)
    {
        var component = (command.context as ExpressionDataComponent)!;
        ExpressionDataClipExporter.OpenWindow(component);
    }

}

internal class ExpressionDataClipImporter
{
    private bool? _skipCreateClip;
    private string? _clipFolderPath;

    public ExpressionDataClipImporter(bool? skipCreateClip = null, string? clipFolderPath = null)
    {
        _skipCreateClip = skipCreateClip;
        _clipFolderPath = clipFolderPath;
    }

    public void ImportClip(ExpressionDataComponent[] components)
    {
        CustomEditorUtility.TryGetContext(components[0].gameObject, out var context);
        if (context == null) throw new InvalidOperationException("Context not found");
        foreach (var component in components)
        {
            ImportClip(component, context.BodyPath);
        }
    }

    public bool ImportClip(ExpressionDataComponent component, string bodyPath)
    {
        var (facialAnimations, nonFacialAnimations) = component.ProcessClip(bodyPath);
        if (facialAnimations.Count == 0)
        {
            return false;
        }

        var so = new SerializedObject(component);
        so.Update();
        so.FindProperty(nameof(ExpressionDataComponent.Clip)).objectReferenceValue = nonFacialAnimations.Count == 0 ? null : CreateClip(new AnimationSet(nonFacialAnimations));
        CustomEditorUtility.AddBlendShapeAnimations(so.FindProperty(nameof(ExpressionDataComponent.BlendShapeAnimations)), facialAnimations, false); // manualにある方を優先
        so.ApplyModifiedProperties();
        return true;
    }

    private AnimationClip? CreateClip(AnimationSet animations)
    {
        if (!ConfirmCreateClip(out var clipFolderPath)) return null;
        var newClip = new AnimationClip();
        newClip.name = GetClipName(animations);
        newClip.AddGenericAnimations(animations.Animations);
        var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{clipFolderPath}/{newClip.name}.anim");
        AssetDatabase.CreateAsset(newClip, assetPath);
        return newClip;
    }

    private bool ConfirmCreateClip([NotNullWhen(true)] out string? clipFolderPath)
    {
        clipFolderPath = null;

        if (_skipCreateClip == null)
        {
            _skipCreateClip = !EditorUtility.DisplayDialog(
                Localization.S($"ExpressionDataClipImporter:ConfirmCreateClip:title"),
                Localization.S("ExpressionDataClipImporter:ConfirmCreateClip:message"),
                Localization.S("ExpressionDataClipImporter:ConfirmCreateClip:create"),
                Localization.S("ExpressionDataClipImporter:ConfirmCreateClip:skip")
            );
        }

        if (_skipCreateClip.Value)
        {
            return false;
        }
        else
        {
            if (string.IsNullOrEmpty(_clipFolderPath))
            {
                if (!TryGetClippath(out clipFolderPath))
                {
                    _skipCreateClip = null;
                    return false;
                }
                _clipFolderPath = clipFolderPath;
                return true;
            }
            else
            {
                clipFolderPath = _clipFolderPath!;
                return true;
            }
        }
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

    private static bool TryGetClippath([NotNullWhen(true)] out string? clipFolderPath)
    {
        clipFolderPath = null;
        var absolutePath = EditorUtility.OpenFolderPanel(Localization.S("ExpressionDataClipImporter:ConfirmCreateClip:title"), "Assets", "");
        if (string.IsNullOrEmpty(absolutePath))
        {
            Debug.LogError("folder not selected");
            return false;
        }
        var relativePath = FileUtil.GetProjectRelativePath(absolutePath);
        if (string.IsNullOrEmpty(relativePath) || !relativePath.StartsWith("Assets"))
        {
            Debug.LogError("folder not in project");
            return false;
        }
        clipFolderPath = relativePath.Replace("\\", "/");
        return true;
    }
}

internal class ExpressionDataClipExporter : EditorWindow
{
    private ExpressionDataComponent _component = null!;

    private bool _addZeroWeight = true;
    private bool _addFacialStyle = false;
    private bool _excludeTrackedShapes = true;

    private const int WindowWidth = 300;
    private const int WindowHeight = 100;

    public static void OpenWindow(ExpressionDataComponent component)
    {
        var window = GetWindow<ExpressionDataClipExporter>();
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
        var animations = new AnimationSet();
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
            var facialStyleAnimations = new List<BlendShapeWeightAnimation>();
            if (FacialStyleContext.TryGetFacialStyleAnimations(_component.gameObject, facialStyleAnimations))
            {
                animations.AddRange(facialStyleAnimations.ToGenericAnimations(context.BodyPath));
            }
        }
        _component.GetAnimations(animations, context);
        if (_excludeTrackedShapes)
        {
            animations.RemoveBlendShapes(context.TrackedBlendShapes);
        }
        CustomEditorUtility.SaveAsClip(clip =>
        {
            clip.AddGenericAnimations(animations);
        });
    }
}