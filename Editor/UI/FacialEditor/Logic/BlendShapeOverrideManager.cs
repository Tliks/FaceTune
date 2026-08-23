namespace Aoyon.FaceTune.Gui.ShapesEditor;

// flagは保存対象のtarget listに含まれるかを表す。weightはflagが立つ場合だけ出力値として意味を持つ。
[Serializable]
internal class BlendShapeOverrideManager : IDisposable
{
    private readonly struct OverrideStateSnapshot
    {
        public readonly bool[] Flags;
        public readonly float[] Weights;

        public OverrideStateSnapshot(bool[] flags, float[] weights)
        {
            Flags = flags;
            Weights = weights;
        }
    }

    private SerializedObject _serializedObject;
    [SerializeField] private bool[] _overrideFlags = null!;
    [SerializeField] private float[] _overrideWeights = null!;
    private SerializedProperty _overrideFlagsProperty;
    private SerializedProperty _overrideWeightsProperty;

    private OverrideStateSnapshot? _initialSnapshot;
    private OverrideStateSnapshot? _editedSnapshotBeforeRestoreInitial;
    private int _modificationRevision;
    private int? _restoreEditedRevision;
    public bool IsChangedFromInitialState => _initialSnapshot.HasValue && !IsSameAsSnapshot(_initialSnapshot.Value);
    public bool CanRestoreEditedOverrides => _editedSnapshotBeforeRestoreInitial.HasValue &&
                                            _restoreEditedRevision.HasValue &&
                                            _restoreEditedRevision.Value == _modificationRevision;

    private string[] _allKeysArray = new string[0];
    private IReadOnlyBlendShapeSet _facialSet = new BlendShapeWeightSet();
    private IReadOnlyBlendShapeSet _baseSet = new BlendShapeWeightSet();
    private readonly BlendShapeWeightSet _effectiveBaseSet = new();
    private ISet<string> _explicitlyExcluded = new HashSet<string>();
    private Dictionary<string, int> _shapeNameToIndexMap = new();

    public IReadOnlyList<string> AllKeys => _allKeysArray;
    public IReadOnlyBlendShapeSet FacialSet => _facialSet;
    public IReadOnlyBlendShapeSet BaseSet => _baseSet;
    public IReadOnlyBlendShapeSet EffectiveBaseSet => _effectiveBaseSet;
    public IEnumerable<string> ExplicitlyExcluded => _explicitlyExcluded;
    public bool IsExplicitlyExcluded(string name) => _explicitlyExcluded.Contains(name);

    private void RebuildEffectiveBaseSet()
    {
        _effectiveBaseSet.Clear();
        _effectiveBaseSet.AddRange(_facialSet);
        _effectiveBaseSet.AddRange(_baseSet);
    }

    public event Action<int>? OnSingleShapeAdded;
    public event Action<IEnumerable<int>>? OnMultipleShapesAdded;
    public event Action<int>? OnSingleShapeRemoved;
    public event Action<IEnumerable<int>>? OnMultipleShapesRemoved;
    public event Action<int>? OnSingleShapeWeightChanged;
    public event Action<IEnumerable<int>>? OnMultipleShapeWeightChanged;
    public event Action? OnUnknownChange;
    public event Action? OnAnyDataChange;

    public BlendShapeOverrideManager(SerializedObject serializedObject, SerializedProperty baseProperty)
    {
        _serializedObject = serializedObject;
        _overrideFlagsProperty = baseProperty.FindPropertyRelative(nameof(_overrideFlags));
        _overrideWeightsProperty = baseProperty.FindPropertyRelative(nameof(_overrideWeights));
        OnAnyDataChange += () =>
        {
            ValidateData();
            // DebugLog();
        };
    }

    public void SetInitialState(
        SkinnedMeshRenderer? targetRenderer,
        IReadOnlyBlendShapeSet? facialSet,
        IReadOnlyBlendShapeSet? baseSet,
        IReadOnlyBlendShapeSet? targetSet,
        ISet<string> explicitlyExcluded)
    {
        _explicitlyExcluded = explicitlyExcluded;
        InitializeTargetRenderer(targetRenderer, explicitlyExcluded);
        InitializeSourceSets(facialSet, baseSet, targetSet);
    }

    private void InitializeTargetRenderer(
        SkinnedMeshRenderer? targetRenderer,
        ISet<string> explicitlyExcluded)
    {
        var allBlendShapes = targetRenderer == null
            ? Array.Empty<BlendShapeWeight>()
            : targetRenderer.GetBlendShapeWeights(targetRenderer.sharedMesh)
                .Where(shape => !explicitlyExcluded.Contains(shape.Name))
                .ToArray();
        _allKeysArray = allBlendShapes.Select(x => x.Name).ToArray();
        _shapeNameToIndexMap = _allKeysArray.Select((x, i) => (x, i)).ToDictionary(x => x.x, x => x.i);
        _overrideFlagsProperty.arraySize = _allKeysArray.Length;
        _overrideWeightsProperty.arraySize = _allKeysArray.Length;
        _serializedObject.ApplyModifiedProperties();
        _serializedObject.Update();
    }

    private void InitializeSourceSets(
        IReadOnlyBlendShapeSet? facialSet,
        IReadOnlyBlendShapeSet? baseSet,
        IReadOnlyBlendShapeSet? targetSet)
    {
        _facialSet = facialSet ?? new BlendShapeWeightSet();
        _baseSet = baseSet ?? new BlendShapeWeightSet();
        var initialTargetSet = targetSet ?? new BlendShapeWeightSet();
        RebuildEffectiveBaseSet();
        ExecuteModification(() =>
        {
            for (int i = 0; i < _allKeysArray.Length; i++)
            {
                _overrideFlagsProperty.GetArrayElementAtIndex(i).boolValue = false;
                if (_effectiveBaseSet.TryGetValue(_allKeysArray[i], out var baseShape))
                {
                    _overrideWeightsProperty.GetArrayElementAtIndex(i).floatValue = baseShape.Weight;
                }
                else
                {
                    _overrideWeightsProperty.GetArrayElementAtIndex(i).floatValue = 0f;
                }
                if (initialTargetSet.TryGetValue(_allKeysArray[i], out var defaultShape))
                {
                    _overrideFlagsProperty.GetArrayElementAtIndex(i).boolValue = true;
                    _overrideWeightsProperty.GetArrayElementAtIndex(i).floatValue = defaultShape.Weight;
                }
            }
        });

        _initialSnapshot = CaptureCurrentSnapshot();
        _editedSnapshotBeforeRestoreInitial = null;
        _restoreEditedRevision = null;

        OnAnyDataChange?.Invoke();
    }

    private OverrideStateSnapshot CaptureCurrentSnapshot()
    {
        var length = _overrideFlagsProperty.arraySize;
        var flags = new bool[length];
        var weights = new float[length];
        for (int i = 0; i < length; i++)
        {
            flags[i] = _overrideFlagsProperty.GetArrayElementAtIndex(i).boolValue;
            weights[i] = _overrideWeightsProperty.GetArrayElementAtIndex(i).floatValue;
        }
        return new OverrideStateSnapshot(flags, weights);
    }

    private bool IsSameAsSnapshot(OverrideStateSnapshot snapshot)
    {
        if (_overrideFlagsProperty.arraySize != snapshot.Flags.Length) return false;
        if (_overrideWeightsProperty.arraySize != snapshot.Weights.Length) return false;

        var length = snapshot.Flags.Length;
        for (int i = 0; i < length; i++)
        {
            var isOverridden = _overrideFlagsProperty.GetArrayElementAtIndex(i).boolValue;
            if (isOverridden != snapshot.Flags[i]) return false;
            if (isOverridden && !Mathf.Approximately(
                    _overrideWeightsProperty.GetArrayElementAtIndex(i).floatValue,
                    snapshot.Weights[i]))
                return false;
        }
        return true;
    }

    private void ApplySnapshot(OverrideStateSnapshot snapshot)
    {
        ExecuteModification(() =>
        {
            _overrideFlagsProperty.arraySize = snapshot.Flags.Length;
            _overrideWeightsProperty.arraySize = snapshot.Weights.Length;
            var length = Mathf.Min(_overrideFlagsProperty.arraySize, _overrideWeightsProperty.arraySize);
            for (int i = 0; i < length; i++)
            {
                _overrideFlagsProperty.GetArrayElementAtIndex(i).boolValue = snapshot.Flags[i];
                _overrideWeightsProperty.GetArrayElementAtIndex(i).floatValue = snapshot.Weights[i];
            }
        });
        OnUnknownChange?.Invoke();
        OnAnyDataChange?.Invoke();
    }

    public bool TryRestoreInitialOverrides()
    {
        if (!_initialSnapshot.HasValue) return false;
        if (!IsChangedFromInitialState) return false;
        _editedSnapshotBeforeRestoreInitial = CaptureCurrentSnapshot();
        ApplySnapshot(_initialSnapshot.Value);
        _restoreEditedRevision = _modificationRevision;
        return true;
    }

    public bool TryDiscardToInitialOverrides()
    {
        if (!_initialSnapshot.HasValue) return false;
        _editedSnapshotBeforeRestoreInitial = null;
        _restoreEditedRevision = null;
        ApplySnapshot(_initialSnapshot.Value);
        return true;
    }

    public void MarkCurrentAsInitialState()
    {
        _initialSnapshot = CaptureCurrentSnapshot();
        _editedSnapshotBeforeRestoreInitial = null;
        _restoreEditedRevision = null;
        OnAnyDataChange?.Invoke();
    }

    public bool TryRestoreEditedOverrides()
    {
        if (!CanRestoreEditedOverrides) return false;
        ApplySnapshot(_editedSnapshotBeforeRestoreInitial!.Value);
        _editedSnapshotBeforeRestoreInitial = null;
        _restoreEditedRevision = null;
        return true;
    }

    public void GetTargetValues(BlendShapeWeightSet resultToAdd)
    {
        var length = _allKeysArray.Length;
        for (int i = 0; i < length; i++)
        {
            if (IsInTarget(i))
            {
                var shapeName = _allKeysArray[i];
                var weight = GetShapeWeight(i);
                resultToAdd.Add(new BlendShapeWeight(shapeName, weight));
            }
        }
    }

    public int GetIndexForShape(string shapeName)
    {
        return _shapeNameToIndexMap.TryGetValue(shapeName, out var index) ? index : -1;
    }

    public bool IsInTarget(int index)
    {
        return _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue;
    }
    
    public bool IsFacialShape(int index)
        => _facialSet.ContainsKey(_allKeysArray[index]);

    public bool IsBaseShape(int index)
        => _baseSet.ContainsKey(_allKeysArray[index]);
    
    public float GetShapeWeight(int index) 
    {
        return _overrideWeightsProperty.GetArrayElementAtIndex(index).floatValue;
    }

    public float GetEffectiveShapeWeight(int index)
    {
        if (IsInTarget(index)) return GetShapeWeight(index);
        return _effectiveBaseSet.TryGetValue(_allKeysArray[index], out var shape) ? shape.Weight : 0f;
    }

    public IEnumerable<int> GetTargetIndices(Func<int, bool> predicate)
    {
        for (int i = 0; i < _allKeysArray.Length; i++)
        {
            if (IsInTarget(i) && predicate(i)) yield return i;
        }
    }

    private void ValidateData()
    {
        // 配列サイズが不整合の場合、再同期
        if (_overrideFlagsProperty.arraySize != _allKeysArray.Length ||
            _overrideWeightsProperty.arraySize != _allKeysArray.Length)
        {
            Debug.LogWarning($"Array size mismatch detected. Resynchronizing: " +
                $"_allKeysArray.Length: {_allKeysArray.Length}, " +
                $"_overrideFlagsProperty.arraySize: {_overrideFlagsProperty.arraySize}, " +
                $"_overrideWeightsProperty.arraySize: {_overrideWeightsProperty.arraySize}");

            _overrideFlagsProperty.arraySize = _allKeysArray.Length;
            _overrideWeightsProperty.arraySize = _allKeysArray.Length;
            _serializedObject.ApplyModifiedProperties();
            _serializedObject.Update();
        }
    }

    private void ExecuteModification(Action action)
    {
        _serializedObject.Update();
        ValidateData();
        _modificationRevision++;
        action();
        _serializedObject.ApplyModifiedProperties();
        _serializedObject.Update();
    }
    
    public void AddShapeWithWeightWithoutApply(int index, float weight)
    {
        _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue = true;
        SetShapeWeightWithoutApply(index, weight);
    }
    public void AddShapeWithWeight(int index, float weight)
    {
        ExecuteModification(() => AddShapeWithWeightWithoutApply(index, weight));
        OnSingleShapeAdded?.Invoke(index);
        OnAnyDataChange?.Invoke();
    }
    public void AddShapesWithWeight(IEnumerable<int> indices, float weight)
    {
        var indicesList = indices as IReadOnlyList<int> ?? indices.ToList();
        ExecuteModification(() =>
        {
            foreach (var index in indicesList) AddShapeWithWeightWithoutApply(index, weight);
        });
        OnMultipleShapesAdded?.Invoke(indicesList);
        OnAnyDataChange?.Invoke();
    }

    public void AddShapesWithWeight(IEnumerable<(int, float)> indicesAndWeights)
    {
        var values = new Dictionary<int, float>();
        foreach (var (index, weight) in indicesAndWeights)
        {
            if ((uint)index < (uint)_allKeysArray.Length) values[index] = weight;
        }
        var list = values.ToArray();
        ExecuteModification(() =>
        {
            foreach (var (index, weight) in list) AddShapeWithWeightWithoutApply(index, weight);
        });
        OnMultipleShapesAdded?.Invoke(list.Select(x => x.Key).ToArray());
        OnAnyDataChange?.Invoke();
    }
    public void AddShapesWithWeight(IReadOnlyCollection<BlendShapeWeight> shapes)
    {
        var indicesAndWeights = shapes.Select(x => (GetIndexForShape(x.Name), x.Weight))
            .Where(pair => pair.Item1 != -1);
        AddShapesWithWeight(indicesAndWeights);
    }

    public void RemoveShapeWithoutApply(int index)
    {
        _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue = false;
    }
    public void RemoveShape(int index)
    {
        ExecuteModification(() => RemoveShapeWithoutApply(index));
        OnSingleShapeRemoved?.Invoke(index);
        OnAnyDataChange?.Invoke();
    }
    public void RemoveShapes(IEnumerable<int> indices)
    {
        var list = indices.Distinct().ToArray();
        ExecuteModification(() =>
        {
            foreach (var index in list) RemoveShapeWithoutApply(index);
        });
        OnMultipleShapesRemoved?.Invoke(list);
        OnAnyDataChange?.Invoke();
    }

    public void SetShapeWeightWithoutApply(int index, float weight)
    {        
        _overrideWeightsProperty.GetArrayElementAtIndex(index).floatValue = weight;
        _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue = true;
    }
    public void SetShapeWeight(int index, float weight)
    {
        ExecuteModification(() => SetShapeWeightWithoutApply(index, weight));
        OnSingleShapeWeightChanged?.Invoke(index);
        OnAnyDataChange?.Invoke();
    }
    public void SetShapesWeight(IEnumerable<int> indices, float weight)
    {
        var list = indices.Distinct().ToArray();
        ExecuteModification(() =>
        {
            foreach (var index in list) SetShapeWeightWithoutApply(index, weight);
        });
        OnMultipleShapeWeightChanged?.Invoke(list);
        OnAnyDataChange?.Invoke();
    }
    public void SetShapesWeight(IEnumerable<(int, float)> indicesAndWeights)
    {
        var list = indicesAndWeights.ToArray();
        ExecuteModification(() =>
        {
            foreach (var (index, weight) in list) SetShapeWeightWithoutApply(index, weight);
        });
        OnMultipleShapeWeightChanged?.Invoke(list.Select(x => x.Item1).ToArray());
        OnAnyDataChange?.Invoke();
    }
    
    public void OnUndoRedo()
    {
        _serializedObject.Update();
        _modificationRevision++;
        OnAnyDataChange?.Invoke();
        OnUnknownChange?.Invoke();
    }

    public void Dispose()
    {
    }
}
