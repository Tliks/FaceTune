using nadena.dev.ndmf.runtime;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

[Serializable]
internal class TargetManager
{
    private SerializedObject _serializedObject;
    private BlendShapeOverrideManager _dataManager;

    private IReadOnlyBlendShapeSet? _currentBaseSet;
    private IReadOnlyBlendShapeSet? _currentDefaultOverrides;

    [SerializeField] private SkinnedMeshRenderer? _targetRenderer;
    public SkinnedMeshRenderer? TargetRenderer
    {
        get => _targetRendererProperty.objectReferenceValue as SkinnedMeshRenderer;
    }
    private SerializedProperty _targetRendererProperty;
    public List<Func<SkinnedMeshRenderer?, bool>> RenderChangeConditions;
    public event Action<SkinnedMeshRenderer?>? OnTargetRendererChanged;
    public event Action<SkinnedMeshRenderer?, IReadOnlyBlendShapeSet?, IReadOnlyBlendShapeSet?>? OnTargetRendererChangedWithSets;
    private SkinnedMeshRenderer? _cachedTargetRendererForUndo;

    private Transform? _targetRoot;

    private IShapesEditorTargeting? _targeting;
    public IShapesEditorTargeting? Targeting
    {
        get => _targeting;
        set
        {
            _targeting = value;
            OnTargetingChanged  ?.Invoke(value);
            UpdateCanSave();
        }
    }
    public List<Func<IShapesEditorTargeting?, bool>> TargetingChangeConditions;
    public event Action<IShapesEditorTargeting?>? OnTargetingChanged;
    public event Action? OnTargetChanged;

    private bool _hasUnsavedChanges = false;
    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        set
        {
            if (_hasUnsavedChanges != value)
            {
                _hasUnsavedChanges = value;
                OnHasUnsavedChangesChanged?.Invoke(value);
                UpdateCanSave();
            }
        }
    }
    public event Action<bool>? OnHasUnsavedChangesChanged;

    private bool _canSave = false;
    public bool CanSave
    {
        get => _canSave;
        set
        {
            if (_canSave != value)
            {
                _canSave = value;
                OnCanSaveChanged?.Invoke(value);
            }
        }
    }
    public event Action<bool>? OnCanSaveChanged;

    private void UpdateCanSave()
    {
        bool newCanSave = TargetRenderer != null && Targeting != null && Targeting.GetTarget() != null && HasUnsavedChanges;
        CanSave = newCanSave;
    }

    public TargetManager(SerializedObject serializedObject, SerializedProperty baseProperty, BlendShapeOverrideManager dataManager)
    {
        _serializedObject = serializedObject;
        _targetRendererProperty = baseProperty.FindPropertyRelative(nameof(_targetRenderer));
        _dataManager = dataManager;
        RenderChangeConditions = new();
        TargetingChangeConditions = new();
    }

    private void ApplyTargetRendererToSerializedObject(SkinnedMeshRenderer? value)
    {
        _serializedObject.Update();
        _targetRendererProperty.objectReferenceValue = value;
        _serializedObject.ApplyModifiedProperties();
        _serializedObject.Update();

        _cachedTargetRendererForUndo = value;
        OnTargetRendererChanged?.Invoke(value);
        OnTargetRendererChangedWithSets?.Invoke(value, _currentBaseSet, _currentDefaultOverrides);
        UpdateCanSave();
    }

    public bool TrySetTargetRenderer(SkinnedMeshRenderer? renderer)
    {
        return TrySetTargetRenderer(renderer, null, null);
    }

    public bool TrySetTargetRenderer(SkinnedMeshRenderer? renderer, IReadOnlyBlendShapeSet? baseSet, IReadOnlyBlendShapeSet? defaultOverrides)
    {
        if (TargetRenderer == renderer && baseSet == _currentBaseSet && defaultOverrides == _currentDefaultOverrides) return false;
        foreach (var condition in RenderChangeConditions)
        {
            if (!condition(renderer)) return false;
        }
        SetTargetRenderer(renderer, baseSet, defaultOverrides);
        return true;
    }

    public void SetTargetRenderer(SkinnedMeshRenderer? renderer)
    {
        SetTargetRenderer(renderer, null, null);
    }

    public void SetTargetRenderer(SkinnedMeshRenderer? renderer, IReadOnlyBlendShapeSet? baseSet, IReadOnlyBlendShapeSet? defaultOverrides)
    {
        var rendererChanged = TargetRenderer != renderer;
        _currentBaseSet = baseSet;
        _currentDefaultOverrides = defaultOverrides;

        if (!rendererChanged)
        {
            OnTargetRendererChangedWithSets?.Invoke(renderer, _currentBaseSet, _currentDefaultOverrides);
            UpdateCanSave();
            return;
        }
        if (renderer != null)
        {
            _targetRoot = RuntimeUtil.FindAvatarInParents(renderer.transform);
            if (_targetRoot == null) throw new Exception("TargetRenderer is not a child of an avatar");
        }
        ApplyTargetRendererToSerializedObject(renderer);
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
            targeting.OnTargetChanged += () => 
            {
                OnTargetChanged?.Invoke();
                UpdateCanSave();
            };
        }
    }
    
    public void Save()  
    {
        if (TargetRenderer == null) throw new Exception("TargetRenderer is not set");
        if (_targetRoot == null) throw new Exception("TargetRoot is not set");
        Targeting?.Save(_targetRoot.gameObject, TargetRenderer, _dataManager);
        _dataManager.MarkCurrentAsInitialState();
        HasUnsavedChanges = false;
    }

    public void OnUndoRedo()
    {
        _serializedObject.Update();
        if (_cachedTargetRendererForUndo != TargetRenderer)
        {
            //Debug.Log($"OnUndoRedo: {_cachedTargetRendererForUndo} != {TargetRenderer}");
            _cachedTargetRendererForUndo = TargetRenderer;
            OnTargetRendererChanged?.Invoke(TargetRenderer);
            OnTargetRendererChangedWithSets?.Invoke(TargetRenderer, _currentBaseSet, _currentDefaultOverrides);
        }
        else
        {
            //Debug.Log($"OnUndoRedo: {_cachedTargetRendererForUndo} == {TargetRenderer}");
        }
        OnTargetingChanged?.Invoke(Targeting);
        UpdateCanSave();
    }
}