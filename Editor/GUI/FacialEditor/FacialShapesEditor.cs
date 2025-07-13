using UnityEngine.UIElements;

namespace aoyon.facetune.ui.shapes_editor;

internal class FacialShapesEditor : EditorWindow
{
    private SerializedObject _serializedObject = null!;
    [SerializeField] private bool[] _overrideFlags = new bool[0];
    [SerializeField] private float[] _overrideWeights = new float[0];
    
    private BlendShapeOverrideManager _dataManager = null!;
    private FacialShapeUI _ui = null!;
    private PreviewManager _previewManager = null!;
    private Action<BlendShapeSet> _onApply = null!;

    private const int MIN_WINDOW_WIDTH = 500;
    private const int MIN_WINDOW_HEIGHT = 500;
    
    public static FacialShapesEditor? OpenEditor(SkinnedMeshRenderer renderer, Mesh mesh, HashSet<string> allKeys, 
        BlendShapeSet? defaultOverrides = null, BlendShapeSet? facialStyleSet = null)
    {
        if (!CanOpenEditor()) return null;
        var window = CreateInstance<FacialShapesEditor>();
        window.titleContent = new GUIContent("Facial Shapes Editor");
        window.Init(renderer, allKeys.ToArray(), defaultOverrides ?? new BlendShapeSet(), facialStyleSet ?? new BlendShapeSet());
        window.Show();
        return window;
    }

    private static bool CanOpenEditor()
    {
        if (HasOpenInstances<FacialShapesEditor>())
        {
            var existingWindow = GetWindow<FacialShapesEditor>();
            if (existingWindow.hasUnsavedChanges)
            {
                var result = EditorUtility.DisplayDialogComplex(
                    "Unsaved Changes", 
                    "This window may have unsaved changes. Would you like to save?", 
                    "Save", 
                    "Discard", 
                    "Cancel"
                );
                
                switch (result)
                {
                    case 0: // Save
                        existingWindow.SaveChanges();
                        existingWindow.Close();
                        return true;
                    case 1: // Discard
                        existingWindow.ForceClose();
                        return true;
                    case 2: // Cancel
                    default:
                        return false;
                }
            }
            else
            {
                existingWindow.Close();
                return true;
            }
        }
        return true;
    }

    private void Init(SkinnedMeshRenderer renderer, string[] allKeys, BlendShapeSet defaultOverrides, BlendShapeSet facialStyleSet)
    {
        _serializedObject = new SerializedObject(this);
        var flagsProperty = _serializedObject.FindProperty(nameof(_overrideFlags));
        var weightsProperty = _serializedObject.FindProperty(nameof(_overrideWeights));
        _dataManager = new BlendShapeOverrideManager(_serializedObject, flagsProperty, weightsProperty, allKeys, facialStyleSet, defaultOverrides);
        _ui = new FacialShapeUI(rootVisualElement, _dataManager);
        _previewManager = new PreviewManager(renderer, _dataManager, _ui);
        _dataManager.OnAnyDataChange += OnDataChanged;
        minSize = new Vector2(MIN_WINDOW_WIDTH, MIN_WINDOW_HEIGHT);
        hasUnsavedChanges = false;
        SetupKeyboardShortcuts();
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

    public void RegisterApplyCallback(Action<BlendShapeSet> onApply) => _onApply = onApply;

    private void OnDataChanged()
    {
        hasUnsavedChanges = true;
    }

    public override void SaveChanges()
    {
        _onApply?.Invoke(_dataManager.GetCurrentOverrides(new()));
        hasUnsavedChanges = false;
    }

    public void ForceClose()
    {
        hasUnsavedChanges = false;
        Close();
    }

    private void OnDisable()
    {
        _dataManager?.Dispose();
        _previewManager?.Dispose();
    }
}