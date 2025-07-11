namespace aoyon.facetune.ui;

internal class FacialShapesEditor : EditorWindow
{
    private SerializedObject _serializedObject = null!;
    [SerializeField] private BlendShapeOverrideData _data = null!;
    private BlendShapeOverrideManager _dataManager = null!;
    private FacialShapeUI _ui = null!;
    private Action<BlendShapeSet> _onApply = null!;

    public static FacialShapesEditor OpenEditor(SkinnedMeshRenderer renderer, Mesh mesh, HashSet<string> allKeys, 
        BlendShapeSet? defaultOverrides = null, BlendShapeSet? facialStyleSet = null)
    {
        var window = CreateInstance<FacialShapesEditor>();
        window.titleContent = new GUIContent("Facial Shapes Editor");
        window.Init(allKeys.ToArray(), defaultOverrides ?? new BlendShapeSet(), facialStyleSet ?? new BlendShapeSet());
        window.Show();
        return window;
    }

    private void Init(string[] allKeys, BlendShapeSet defaultOverrides, BlendShapeSet facialStyleSet)
    {
        _serializedObject = new SerializedObject(this);
        _data = new BlendShapeOverrideData();
        var dataProperty = _serializedObject.FindProperty(nameof(_data));
        _dataManager = new BlendShapeOverrideManager(_serializedObject, dataProperty, allKeys, facialStyleSet);
        _dataManager.InitializeData(defaultOverrides);

        _ui = new FacialShapeUI(rootVisualElement, _dataManager);
        _dataManager.OnDataModified += OnDataChanged;
        
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    public void RegisterApplyCallback(Action<BlendShapeSet> onApply) => _onApply = onApply;

    private void OnDataChanged()
    {
        // _onApply?.Invoke(_dataManager.GetCurrentOverrides(new()));
    }

    private void OnDestroy()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        if (_dataManager != null)
        {
            _dataManager.OnDataModified -= OnDataChanged;
        }
    }

    private void OnUndoRedo()
    {
        _serializedObject.Update();
        _ui?.RefreshUI();
    }
}