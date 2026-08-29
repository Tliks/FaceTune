using Aoyon.FaceTune.Platforms;
using nadena.dev.ndmf.runtime;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal class FacialShapesEditor : EditorWindow
{
    [SerializeField] private BlendShapeOverrideManager _dataManager = null!;

    private FacialShapesEditorContext? _context;
    private bool _unsavedStateSyncPending;
    private int _initialUndoGroup = -1;

    private const int MIN_WINDOW_WIDTH = 500;
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
        IShapesEditorTargeting? targeting = null,
        IReadOnlyList<BlendShapeWeightAnimation>? facialAnimations = null,
        IReadOnlyList<BlendShapeWeightAnimation>? baseAnimations = null,
        IReadOnlyList<BlendShapeWeightAnimation>? targetAnimations = null)
    {
        if (TryOpenEditor() is not FacialShapesEditor window) return null;
        window.StartContext(renderer, targeting, facialAnimations, baseAnimations, targetAnimations);
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
        IShapesEditorTargeting? targeting,
        IReadOnlyList<BlendShapeWeightAnimation>? facialAnimations,
        IReadOnlyList<BlendShapeWeightAnimation>? baseAnimations,
        IReadOnlyList<BlendShapeWeightAnimation>? targetAnimations)
    {
        EndContext();

        targeting ??= new AnimationClipTargeting();

        var serializedObject = new SerializedObject(this);
        _dataManager = new BlendShapeOverrideManager(
            serializedObject,
            serializedObject.FindProperty(nameof(_dataManager)));
        serializedObject.Update();
        var avatarRoot = renderer == null
            ? null
            : RuntimeUtil.FindAvatarInParents(renderer.transform);
        var unavailableBlendShapes = avatarRoot == null
            ? ImmutableHashSet.Create<string>(StringComparer.Ordinal)
            : AvatarContext.GetUnavailableBlendShapeNames(
                avatarRoot.gameObject,
                FaceTuneWriteKind.FacialData);
        _dataManager.SetInitialState(
            renderer,
            ToFirstFrameSet(facialAnimations),
            ToFirstFrameSet(baseAnimations),
            ToFirstFrameSet(targetAnimations, excludeMultiFrame: true),
            unavailableBlendShapes,
            GetMultiFrameNames(targetAnimations));
        _dataManager.OnAnyDataChange += SyncUnsavedChangesFromData;

        _context = new FacialShapesEditorContext(
            serializedObject,
            _dataManager,
            rootVisualElement,
            renderer,
            targeting,
            targeting is AnimationClipTargeting,
            TryChangeRenderer,
            SaveChanges);

        _unsavedStateSyncPending = false;
        hasUnsavedChanges = false;
        Undo.SetCurrentGroupName($"Facial Shapes Editor: StartContext: {renderer?.name}");
    }

    private static IReadOnlyBlendShapeSet? ToFirstFrameSet(
        IReadOnlyList<BlendShapeWeightAnimation>? animations,
        bool excludeMultiFrame = false)
    {
        if (animations == null) return null;
        return new BlendShapeWeightSet(
            animations
                .Where(animation => !excludeMultiFrame || !animation.IsMultiFrame)
                .Select(animation => animation.ToFirstFrameBlendShape()));
    }

    private static ISet<string>? GetMultiFrameNames(
        IReadOnlyList<BlendShapeWeightAnimation>? animations)
        => animations?
            .Where(animation => animation.IsMultiFrame)
            .Select(animation => animation.Name)
            .ToHashSet(StringComparer.Ordinal);

    private void EndContext()
    {
        if (_context != null)
        {
            _context.DataManager.OnAnyDataChange -= SyncUnsavedChangesFromData;
            _context.Dispose();
            _context = null;
            _dataManager = null!;
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
        hasUnsavedChanges = _context?.DataManager.IsChangedFromInitialState == true;
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

        var targeting = _context.Targeting;
        EditorApplication.delayCall += () =>
        {
            var nextWindow = CreateInstance<FacialShapesEditor>();
            nextWindow.Show();
            nextWindow.StartContext(renderer, targeting, null, null, null);
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
                _context?.DataManager.TryDiscardToInitialOverrides();
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

        _context.Targeting.Save(targetRoot.gameObject, _context.Renderer, _context.DataManager);
        _context.DataManager.MarkCurrentAsInitialState();
        SyncUnsavedChangesNow();
    }

    private void OnInspectorUpdate()
    {
        _context?.DataManager.SynchronizeSerializedState();
    }

    private void OnDisable()
    {
        EndContext();
        if (_initialUndoGroup >= 0)
            Undo.CollapseUndoOperations(_initialUndoGroup);
    }
}
