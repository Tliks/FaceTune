using nadena.dev.ndmf.runtime;

namespace aoyon.facetune.gui.shapes_editor;

[Serializable]
internal class TargetManager
{
    private SerializedObject _serializedObject;

    [SerializeField] private SkinnedMeshRenderer? _targetRenderer;
    public SkinnedMeshRenderer? TargetRenderer
    {
        get => _targetRendererProperty.objectReferenceValue as SkinnedMeshRenderer;
        set
        {
            _serializedObject.Update();
            _targetRendererProperty.objectReferenceValue = value;
            _serializedObject.ApplyModifiedProperties();
            _serializedObject.Update();
            OnTargetRendererChanged?.Invoke(value);
        }
    }
    private SerializedProperty _targetRendererProperty;
    public List<Func<SkinnedMeshRenderer?, bool>> RenderChangeConditions = new();
    public event Action<SkinnedMeshRenderer?>? OnTargetRendererChanged;
    private SkinnedMeshRenderer? _unserializedTargetRenderer;

    private IShapesEditorTargeting? _targeting;
    public IShapesEditorTargeting? Targeting
    {
        get => _targeting;
        set
        {
            _targeting = value;
            OnTargetingChanged?.Invoke(value);
        }
    }
    public List<Func<IShapesEditorTargeting?, bool>> TargetingChangeConditions = new();
    public event Action<IShapesEditorTargeting?>? OnTargetingChanged;
    public event Action? OnTargetChanged;

    private Transform? _targetRoot;
    private readonly BlendShapeOverrideManager _dataManager;

    public TargetManager(SerializedObject serializedObject, SerializedProperty baseProperty, BlendShapeOverrideManager dataManager)
    {
        _serializedObject = serializedObject;
        _dataManager = dataManager;
        _targetRendererProperty = baseProperty.FindPropertyRelative(nameof(_targetRenderer));
    }

    public void OnDomainReload(SerializedObject serializedObject, SerializedProperty baseProperty)
    {
        _serializedObject = serializedObject;
        _targetRendererProperty = baseProperty.FindPropertyRelative(nameof(_targetRenderer));
        RenderChangeConditions ??= new();
        TargetingChangeConditions ??= new();
    }

    public bool TrySetTargetRenderer(SkinnedMeshRenderer? renderer)
    {
        if (TargetRenderer == renderer) return false;
        foreach (var condition in RenderChangeConditions)
        {
            if (!condition(renderer)) return false;
        }
        SetTargetRenderer(renderer);
        return true;
    }

    public void SetTargetRenderer(SkinnedMeshRenderer? renderer)
    {
        if (TargetRenderer == renderer) return;
        if (renderer != null)
        {
            _targetRoot = RuntimeUtil.FindAvatarInParents(renderer.transform);
            if (_targetRoot == null) throw new Exception("TargetRenderer is not a child of an avatar");
        }
        TargetRenderer = renderer;
        _unserializedTargetRenderer = renderer;
        OnTargetRendererChanged?.Invoke(renderer);
    }

    public bool TrySetTargeting(IShapesEditorTargeting targeting)
    {
        if (Targeting == targeting) return false;
        foreach (var condition in TargetingChangeConditions)
        {
            if (!condition(targeting)) return false;
        }
        SetTargeting(targeting);
        return true;
    }

    public void SetTargeting(IShapesEditorTargeting? targeting)
    {
        if (Targeting == targeting) return;
        Targeting = targeting;
        OnTargetingChanged?.Invoke(targeting);
        if (targeting != null)
        {
            targeting.OnTargetChanged += () => OnTargetChanged?.Invoke();
        }
    }
    
    public void Save()  
    {
        if (TargetRenderer == null) throw new Exception("TargetRenderer is not set");
        if (_targetRoot == null) throw new Exception("TargetRoot is not set");
        Targeting?.Save(_targetRoot.gameObject, TargetRenderer, _dataManager);
    }

    public void OnUndoRedo()
    {
        if (_unserializedTargetRenderer != TargetRenderer)
        {
            _unserializedTargetRenderer = TargetRenderer;
            OnTargetRendererChanged?.Invoke(TargetRenderer);
        }
        OnTargetingChanged?.Invoke(Targeting);
    }
}