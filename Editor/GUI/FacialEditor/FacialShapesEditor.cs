using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal class FacialShapesEditor : EditorWindow
{
    private SerializedObject _serializedObject = null!;
    
    [SerializeField] private TargetManager _targetManager = null!;
    [SerializeField] private BlendShapeOverrideManager _dataManager = null!;
    private BlendShapeGrouping _groupManager = null!;
    private PreviewManager _previewManager = null!;

    private FacialShapeUI _ui = null!;

    private const int MIN_WINDOW_WIDTH = 500;
    private const int MIN_WINDOW_HEIGHT = 500;

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
        SkinnedMeshRenderer? renderer = null, IShapesEditorTargeting? targeting = null, IReadOnlyBlendShapeSet? defaultOverrides = null, IReadOnlyBlendShapeSet? baseSet = null)
    {
        if (TryOpenEditor() is not FacialShapesEditor window) return null;
        window.RefreshTargetRenderer(renderer, baseSet, defaultOverrides);
        targeting ??= new AnimationClipTargeting();
        window._targetManager.SetTargeting(targeting);
        return window;
    }

    private void OnEnable()
    {
        _serializedObject = new SerializedObject(this);
        _dataManager = new BlendShapeOverrideManager(_serializedObject, _serializedObject.FindProperty(nameof(_dataManager))); // 初期化
        _targetManager = new TargetManager(_serializedObject, _serializedObject.FindProperty(nameof(_targetManager)), _dataManager); // 初期化
        _groupManager = new BlendShapeGrouping(_targetManager, _dataManager);
        _previewManager = new PreviewManager(_dataManager, rootVisualElement);
        _ui = new FacialShapeUI(rootVisualElement, _targetManager, _dataManager, _groupManager, _previewManager);

        minSize = new Vector2(MIN_WINDOW_WIDTH, MIN_WINDOW_HEIGHT);
        titleContent = new GUIContent("Facial Shapes Editor");
        saveChangesMessage = Localization.S("FacialEditor:UnsavedChanges:Message");

        hasUnsavedChanges = false;
        SetupKeyboardShortcuts();
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Facial Shapes Editor: OnEnable");
        _initialUndoGroup = Undo.GetCurrentGroup();
        Undo.undoRedoPerformed += OnUndoRedoPerformed;

        _targetManager.RenderChangeConditions.Add(renderer => CanRefreshTargetRenderer());
        _targetManager.OnTargetRendererChanged += (renderer) => OnTargetRendererChanged(renderer);
        _targetManager.TargetingChangeConditions.Add(targeting => true);
        _targetManager.OnTargetingChanged += (targeting) => {};
        _targetManager.OnTargetChanged += () => {};
        _targetManager.OnHasUnsavedChangesChanged += (hasUnsavedChanges) => this.hasUnsavedChanges = hasUnsavedChanges;
        _dataManager.OnAnyDataChange += () => _targetManager.HasUnsavedChanges = true;
    }

    private bool CanRefreshTargetRenderer()
    {
        if (!_targetManager.HasUnsavedChanges) return true;
        return ProcessUnsavedChanges(this);
    }

    public void RefreshTargetRenderer(SkinnedMeshRenderer? renderer, IReadOnlyBlendShapeSet? baseSet = null, IReadOnlyBlendShapeSet? defaultOverrides = null)
    {
        _targetManager.SetTargetRenderer(renderer);
        _dataManager.RefreshBaseSetAndDefaultOverrides(baseSet, defaultOverrides);
    }

    private void OnTargetRendererChanged(SkinnedMeshRenderer? renderer)
    {
        _dataManager.RefreshTargetRenderer(renderer);
        _groupManager.Refresh(_dataManager.AllKeys);
        _previewManager.RefreshTargetRenderer(renderer);
        _ui.RefreshTarget();
        EditorApplication.delayCall += () => _targetManager.HasUnsavedChanges = false;
        Undo.SetCurrentGroupName($"Facial Shapes Editor: OnTargetRendererChanged: {renderer?.name}");
    }

    private bool ProcessUnsavedChanges(FacialShapesEditor window)
    {
        var result = EditorUtility.DisplayDialogComplex(
            Localization.S("FacialEditor:UnsavedChanges:Title"),
            Localization.S("FacialEditor:UnsavedChanges:Message"), 
            Localization.S("FacialEditor:UnsavedChanges:Save"), 
            Localization.S("FacialEditor:UnsavedChanges:Discard"), 
            Localization.S("FacialEditor:UnsavedChanges:Cancel")
        );

        bool processed;
        switch (result)
        {
            case 0: // Save
                window.SaveChanges();
                processed = true;
                break;
            case 1: // Discard
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
        _targetManager.Save();
    }

    private void OnUndoRedoPerformed()
    {
        _targetManager.OnUndoRedo();
        _dataManager.OnUndoRedo();
    }

    private void OnDisable()
    {
        if (_serializedObject != null)
        {
            _serializedObject.Dispose();
            _serializedObject = null!;
        }
        if (_targetManager != null)
        {
            // _targetManager.Dispose();
            _targetManager = null!;
        }
        if (_dataManager != null)
        {
            _dataManager.Dispose();
            _dataManager = null!;
        }
        if (_groupManager != null)
        {
            // _groupManager.Dispose();
            _groupManager = null!;
        }
        if (_previewManager != null)
        {
            _previewManager.Dispose();
            _previewManager = null!;
        }
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        Undo.CollapseUndoOperations(_initialUndoGroup);
    }

    private void DebugLog(string message)
    {
#if FACETUNE_SHAPESEDITOR_DEBUG
        Debug.Log(message);
#endif
    }
}