using UnityEngine.UIElements;

namespace aoyon.facetune.gui.shapes_editor;

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
        SkinnedMeshRenderer? renderer, IShapesEditorTargeting? targeting = null, IReadOnlyBlendShapeSet? defaultOverrides = null, IReadOnlyBlendShapeSet? facialStyleSet = null)
    {
        if (TryOpenEditor() is not FacialShapesEditor window) return null;
        window.RefreshTargetRenderer(renderer, facialStyleSet, defaultOverrides);
        targeting ??= new AnimationClipTargeting();
        window._targetManager.SetTargeting(targeting);
        return window;
    }

    private void OnEnable()
    {
        _serializedObject = new SerializedObject(this);
        if (_dataManager == null) 
        {
            _dataManager = new BlendShapeOverrideManager(_serializedObject, _serializedObject.FindProperty(nameof(_dataManager)));
        }
        else
        {
            _dataManager.OnDomainReload(_serializedObject, _serializedObject.FindProperty(nameof(_dataManager)));
        }
        if (_targetManager == null) 
        {
            _targetManager = new TargetManager(_serializedObject, _serializedObject.FindProperty(nameof(_targetManager)), _dataManager);
        }
        else
        {
            _targetManager.OnDomainReload(_serializedObject, _serializedObject.FindProperty(nameof(_targetManager)));
        }
        _groupManager = new BlendShapeGrouping(_targetManager, _dataManager);
        _previewManager = new PreviewManager(_dataManager, rootVisualElement);
        _ui = new FacialShapeUI(rootVisualElement, _targetManager, _dataManager, _groupManager, _previewManager);

        minSize = new Vector2(MIN_WINDOW_WIDTH, MIN_WINDOW_HEIGHT);
        titleContent = new GUIContent("Facial Shapes Editor");

        hasUnsavedChanges = false;
        SetupKeyboardShortcuts();
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Facial Shapes Editor");
        _initialUndoGroup = Undo.GetCurrentGroup();
        Undo.undoRedoPerformed += OnUndoRedoPerformed;

        _targetManager.RenderChangeConditions.Add(renderer => CanRefreshTargetRenderer());
        _targetManager.OnTargetRendererChanged += (renderer) => OnTargetRendererChanged(renderer);
        _targetManager.TargetingChangeConditions.Add(targeting => true);
        _targetManager.OnTargetingChanged += (targeting) => {};
        _targetManager.OnTargetChanged += () => {};
        _dataManager.OnAnyDataChange += OnDataChanged;
    }

    private bool CanRefreshTargetRenderer()
    {
        if (!hasUnsavedChanges) return true;
        return ProcessUnsavedChanges(this);
    }

    public void RefreshTargetRenderer(SkinnedMeshRenderer? renderer, IReadOnlyBlendShapeSet? facialStyleSet = null, IReadOnlyBlendShapeSet? defaultOverrides = null)
    {
        _targetManager.SetTargetRenderer(renderer);
        _dataManager.SetStyleSet(facialStyleSet ?? new BlendShapeSet());
        _dataManager.OverrideShapesAndSetWeight(defaultOverrides ?? new BlendShapeSet());
    }

    private void OnTargetRendererChanged(SkinnedMeshRenderer? renderer)
    {
        _dataManager.RefreshTargetRenderer(renderer);
        _groupManager.Refresh(_dataManager.AllKeys);
        _previewManager.RefreshTargetRenderer(renderer);
        _ui.RefreshTarget();
        EditorApplication.delayCall += () => hasUnsavedChanges = false;
    }

    private bool ProcessUnsavedChanges(FacialShapesEditor window)
    {
        var result = EditorUtility.DisplayDialogComplex(
            "Unsaved Changes", 
            "This window may have unsaved changes. Would you like to save?", 
            "Save", 
            "Discard", 
            "Cancel"
        );

        bool processedUnsavedChanges;
        switch (result)
        {
            case 0: // Save
                window.SaveChanges();
                processedUnsavedChanges = true;
                break;
            case 1: // Discard
                window.hasUnsavedChanges = false;
                processedUnsavedChanges = true;
                break;
            case 2: // Cancel
            default:
                processedUnsavedChanges = false;
                break;
        }
        return processedUnsavedChanges;
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

    private void OnDataChanged()
    {
        hasUnsavedChanges = true;
    }

    public override void SaveChanges()
    {
        _targetManager.Save();
        hasUnsavedChanges = false;
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
}