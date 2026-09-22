using Aoyon.FaceTune.Platforms;
using nadena.dev.ndmf.runtime;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal class FacialShapesEditor : EditorWindow
{
    [SerializeField] private BlendShapeOverrideManager[] _dataManagers = null!;

    private FacialShapesEditorContext? _context;
    private bool _unsavedStateSyncPending;
    private int _initialUndoGroup = -1;
    private Func<SkinnedMeshRenderer, ISet<string>?>? _resolveUnavailableBlendShapeNames;

    private const int MIN_WINDOW_WIDTH = 570;
    private const int MIN_WINDOW_HEIGHT = 700;

    public static FacialShapesEditor? TryOpenEditor()
    {
        FacialShapesEditor? editableWindow = null;
        if (HasOpenInstances<FacialShapesEditor>())
        {
            var existingWindow = GetWindow<FacialShapesEditor>();
            if (existingWindow.hasUnsavedChanges && !existingWindow.ProcessUnsavedChanges(existingWindow))
            {
                editableWindow = null;
            }
            else
            {
                editableWindow = existingWindow;
            }
        }
        else
        {
            editableWindow = CreateInstance<FacialShapesEditor>();
        }
        if (editableWindow == null) return null;
        editableWindow.Show();
        return editableWindow;
    }

    public static FacialShapesEditor? TryOpenEditor(
        SkinnedMeshRenderer? renderer = null,
        Object? target = null,
        string? animationPropertyPath = null,
        IReadOnlyList<BlendShapeWeightAnimation>? facialAnimations = null,
        IReadOnlyList<BlendShapeWeightAnimation>? baseAnimations = null,
        IReadOnlyList<BlendShapeWeightAnimation>? initialOverrideAnimations = null,
        ISet<string>? unavailableBlendShapeNames = null,
        Func<SkinnedMeshRenderer, ISet<string>?>? resolveUnavailableBlendShapeNames = null)
    {
        if (TryOpenEditor() is not FacialShapesEditor window) return null;
        window._resolveUnavailableBlendShapeNames = resolveUnavailableBlendShapeNames;
        window.StartContext(
            renderer,
            target,
            animationPropertyPath,
            facialAnimations,
            baseAnimations,
            initialOverrideAnimations,
            unavailableBlendShapeNames);
        return window;
    }

    private void OnEnable()
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Facial Shapes Editor: Window Opened");
        Undo.RecordObject(this, "Facial Shapes Editor: Window Opened");
        Undo.IncrementCurrentGroup();
        _initialUndoGroup = Undo.GetCurrentGroup();

        minSize = new Vector2(MIN_WINDOW_WIDTH, MIN_WINDOW_HEIGHT);
        titleContent = "facialEditor.title".LG();
        saveChangesMessage = "facialEditor.unsavedChanges.message".LS();

        hasUnsavedChanges = false;
        SetupKeyboardShortcuts();
    }

    private void StartContext(
        SkinnedMeshRenderer? renderer,
        Object? target,
        string? animationPropertyPath,
        IReadOnlyList<BlendShapeWeightAnimation>? facialAnimations,
        IReadOnlyList<BlendShapeWeightAnimation>? baseAnimations,
        IReadOnlyList<BlendShapeWeightAnimation>? initialOverrideAnimations,
        ISet<string>? unavailableBlendShapeNames)
    {
        EndContext();

        initialOverrideAnimations ??= GetClipInitialOverrideAnimations(renderer, target);

        _dataManagers = new BlendShapeOverrideManager[1];
        var serializedObject = new SerializedObject(this);
        serializedObject.Update();
        _dataManagers[0] = new BlendShapeOverrideManager(
            serializedObject,
            serializedObject.FindProperty(nameof(_dataManagers)).GetArrayElementAtIndex(0));
        unavailableBlendShapeNames ??= renderer == null
            ? null
            : _resolveUnavailableBlendShapeNames?.Invoke(renderer);
        var dataManager = _dataManagers[0];
        dataManager.SetInitialState(
            renderer,
            ToFirstFrameSet(facialAnimations),
            ToFirstFrameSet(baseAnimations),
            ToFirstFrameSet(initialOverrideAnimations),
            unavailableBlendShapeNames ?? ImmutableHashSet<string>.Empty,
            GetInitialCurves(initialOverrideAnimations));
        dataManager.OnAnyDataChange += SyncUnsavedChangesFromData;

        _context = new FacialShapesEditorContext(
            serializedObject,
            _dataManagers,
            rootVisualElement,
            renderer,
            target,
            animationPropertyPath,
            TryChangeRenderer,
            SaveChanges);

        _unsavedStateSyncPending = false;
        hasUnsavedChanges = false;
        Undo.SetCurrentGroupName($"Facial Shapes Editor: StartContext: {renderer?.name}");
    }

    private static IReadOnlyList<BlendShapeWeightAnimation>? GetClipInitialOverrideAnimations(
        SkinnedMeshRenderer? renderer,
        Object? target)
    {
        if (renderer == null || target is not AnimationClip clip)
            return null;

        var animations = new List<BlendShapeWeightAnimation>();
        clip.GetBlendShapeAnimations(ClipImportOption.NonZero, animations, string.Empty);
        return animations;
    }

    private static ImmutableBlendShapeWeightSet? ToFirstFrameSet(
        IReadOnlyList<BlendShapeWeightAnimation>? animations)
    {
        if (animations == null) return null;
        return new ImmutableBlendShapeWeightSet(
            animations.Select(animation => animation.ToFirstFrameBlendShape()));
    }

    // MultiFrameのカーブは編集対象行として取り込むため、構造をそのままシードとして渡す。
    private static Dictionary<string, AnimationCurve>? GetInitialCurves(
        IReadOnlyList<BlendShapeWeightAnimation>? animations)
    {
        if (animations == null) return null;
        var curves = new Dictionary<string, AnimationCurve>(StringComparer.Ordinal);
        foreach (var animation in animations)
        {
            if (animation.IsMultiFrame)
                curves[animation.Name] = animation.Curve;
        }
        return curves.Count == 0 ? null : curves;
    }

    private void EndContext()
    {
        if (_context != null)
        {
            foreach (var dataManager in _context.DataManagers)
                dataManager.OnAnyDataChange -= SyncUnsavedChangesFromData;
            _context.Dispose();
            _context = null;
            _dataManagers = null!;
        }
        _unsavedStateSyncPending = false;
    }

    private void SyncUnsavedChangesFromData()
    {
        if (_context == null) return;

        // Keep close handling safe immediately, while the exact comparison is coalesced.
        hasUnsavedChanges = true;
        if (_unsavedStateSyncPending) return;

        _unsavedStateSyncPending = true;
        rootVisualElement.schedule.Execute(() =>
        {
            _unsavedStateSyncPending = false;
            SyncUnsavedChangesNow();
        });
    }

    private void SyncUnsavedChangesNow()
    {
        _unsavedStateSyncPending = false;
        hasUnsavedChanges = _context?.DataManagers.Any(
            dataManager => dataManager.IsChangedFromInitialState) == true;
    }

    private bool CanDiscardCurrentContext()
    {
        SyncUnsavedChangesNow();
        if (!hasUnsavedChanges) return true;
        return ProcessUnsavedChanges(this);
    }

    private bool TryChangeRenderer(SkinnedMeshRenderer? renderer)
    {
        if (_context == null || !_context.CanChangeRenderer) return false;
        if (_context.Renderer == renderer) return false;
        if (!CanDiscardCurrentContext()) return false;

        var target = _context.Target;
        var animationPropertyPath = _context.AnimationPropertyPath;
        EditorApplication.delayCall += () =>
        {
            var nextWindow = CreateInstance<FacialShapesEditor>();
            nextWindow.Show();
            nextWindow._resolveUnavailableBlendShapeNames = _resolveUnavailableBlendShapeNames;
            nextWindow.StartContext(
                renderer,
                target,
                animationPropertyPath,
                null,
                null,
                null,
                null);
            Close();
        };
        return true;
    }

    private bool ProcessUnsavedChanges(FacialShapesEditor window)
    {
        window.SyncUnsavedChangesNow();
        if (!window.hasUnsavedChanges) return true;

        var result = EditorUtility.DisplayDialogComplex(
            "facialEditor.unsavedChanges.title".LS(),
            "facialEditor.unsavedChanges.message".LS(),
            "facialEditor.unsavedChanges.save".LS(),
            "facialEditor.unsavedChanges.discard".LS(),
            "facialEditor.unsavedChanges.cancel".LS()
        );

        bool processed;
        switch (result)
        {
            case 0: // Save
                window.SaveChanges();
                processed = true;
                break;
            case 1: // Discard
                if (_context != null)
                {
                    foreach (var dataManager in _context.DataManagers)
                        dataManager.TryDiscardToInitialOverrides();
                }
                window.hasUnsavedChanges = false;
                processed = true;
                break;
            case 2: // Cancel
            default:
                processed = false;
                break;
        }
        return processed;
    }

    private void SetupKeyboardShortcuts()
    {
        rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);
        rootVisualElement.focusable = true;
        rootVisualElement.Focus();

        void OnKeyDown(KeyDownEvent evt)
        {
            // Ctrl+S（Windows/Linux）またはCmd+S（Mac）での保存
            if (evt.keyCode == KeyCode.S && (evt.ctrlKey || evt.commandKey))
            {
                SaveChanges();
                evt.StopPropagation();
                evt.PreventDefault();
            }
        }
    }

    public override void SaveChanges()
    {
        if (_context?.Renderer == null) throw new Exception("TargetRenderer is not set");

        var targetRoot = RuntimeUtil.FindAvatarInParents(_context.Renderer.transform);
        if (targetRoot == null) throw new Exception("TargetRenderer is not a child of an avatar");

        if (_context.Target == null) throw new Exception("Target is not set");
        FacialShapeSaver.Save(
            _context.Target,
            _context.AnimationPropertyPath,
            targetRoot.gameObject,
            _context.Renderer,
            _context.DataManager,
            _context.ZeroUnspecifiedBlendShapes,
            _context.ZeroUnavailableBlendShapes);
        foreach (var dataManager in _context.DataManagers)
            dataManager.MarkCurrentAsInitialState();
        SyncUnsavedChangesNow();
    }

    private void OnInspectorUpdate()
    {
        if (_context == null) return;
        foreach (var dataManager in _context.DataManagers)
            dataManager.SynchronizeSerializedState();
    }

    private void OnDisable()
    {
        EndContext();
        if (_initialUndoGroup >= 0)
            Undo.CollapseUndoOperations(_initialUndoGroup);
    }
}
