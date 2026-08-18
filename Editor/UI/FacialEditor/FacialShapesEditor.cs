using UnityEngine.UIElements;
using nadena.dev.ndmf.runtime;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal class FacialShapesEditor : EditorWindow
{
    [SerializeField] private BlendShapeOverrideManager _dataManager = null!;

    private FacialShapesEditorContext? _context;

    private const int MIN_WINDOW_WIDTH = 500;
    private const int MIN_WINDOW_HEIGHT = 700;

    private int _initialUndoGroup = -1;

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
        IReadOnlyBlendShapeSet? styleSet = null,
        IReadOnlyBlendShapeSet? baseSet = null,
        IReadOnlyBlendShapeSet? defaultOverrides = null)
    {
        if (TryOpenEditor() is not FacialShapesEditor window) return null;
        window.StartContext(renderer, targeting, styleSet, baseSet, defaultOverrides);
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
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
    }

    private void StartContext(
        SkinnedMeshRenderer? renderer,
        IShapesEditorTargeting? targeting,
        IReadOnlyBlendShapeSet? styleSet,
        IReadOnlyBlendShapeSet? baseSet,
        IReadOnlyBlendShapeSet? defaultOverrides)
    {
        EndContext();

        renderer = renderer.DestroyedAsNull();
        targeting ??= new AnimationClipTargeting();

        var serializedObject = new SerializedObject(this);
        _dataManager = new BlendShapeOverrideManager(
            serializedObject,
            serializedObject.FindProperty(nameof(_dataManager)));
        serializedObject.Update();
        _dataManager.SetInitialState(renderer, styleSet, baseSet, defaultOverrides);
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

        hasUnsavedChanges = false;
        Undo.SetCurrentGroupName($"Facial Shapes Editor: StartContext: {renderer?.name}");
    }

    private void EndContext()
    {
        if (_context != null)
        {
            _context.DataManager.OnAnyDataChange -= SyncUnsavedChangesFromData;
            _context.Dispose();
            _context = null;
            _dataManager = null!;
        }
    }

    private void SyncUnsavedChangesFromData()
    {
        hasUnsavedChanges = _context?.DataManager.IsChangedFromInitialState == true;
    }

    private bool CanDiscardCurrentContext()
    {
        if (!hasUnsavedChanges) return true;
        return ProcessUnsavedChanges(this);
    }

    private bool TryChangeRenderer(SkinnedMeshRenderer? renderer)
    {
        renderer = renderer.DestroyedAsNull();
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
        SyncUnsavedChangesFromData();
    }

    private void OnUndoRedoPerformed()
    {
        if (_context == null) return;

        _context.DataManager.OnUndoRedo();
        SyncUnsavedChangesFromData();
    }

    private void OnDisable()
    {
        EndContext();
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        Undo.CollapseUndoOperations(_initialUndoGroup);
    }
}
