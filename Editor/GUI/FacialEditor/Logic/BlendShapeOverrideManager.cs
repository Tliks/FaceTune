namespace Aoyon.FaceTune.Gui.ShapesEditor;

// Baseは初期状態から変化した場合のみoverride
// 通常のブレンドシェイプはweightに関わらず、overrideかどうかで判断
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
    private IReadOnlyBlendShapeSet _baseSet = new BlendShapeWeightSet();
    private Dictionary<string, int> _shapeNameToIndexMap = new();

    public IReadOnlyList<string> AllKeys => _allKeysArray;
    public IReadOnlyBlendShapeSet BaseSet => _baseSet;
    public event Action<int>? OnSingleShapeOverride;
    public event Action<IEnumerable<int>>? OnMultipleShapeOverride;
    public event Action<int>? OnSingleShapeUnoverride;
    public event Action<IEnumerable<int>>? OnMultipleShapeUnoverride;
    public event Action<int>? OnSingleShapeWeightChanged;
    public event Action<IEnumerable<int>>? OnMultipleShapeWeightChanged;
    public event Action? OnUnknownChange;
    public event Action? OnBaseSetChange;
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

    public void RefreshTargetRenderer(SkinnedMeshRenderer? targetRenderer)
    {
        var allBlendShapes = targetRenderer == null ? new BlendShapeWeight[0] : targetRenderer.GetBlendShapes(targetRenderer.sharedMesh);
        _allKeysArray = allBlendShapes.Select(x => x.Name).ToArray();
        _shapeNameToIndexMap = _allKeysArray.Select((x, i) => (x, i)).ToDictionary(x => x.x, x => x.i);
        _overrideFlagsProperty.arraySize = _allKeysArray.Length;
        _overrideWeightsProperty.arraySize = _allKeysArray.Length;
        _serializedObject.ApplyModifiedProperties();
        _serializedObject.Update();
    }

    public void RefreshBaseSetAndDefaultOverrides(IReadOnlyBlendShapeSet? baseSet, IReadOnlyBlendShapeSet? defaultOverrides)
    {
        _baseSet = baseSet ?? new BlendShapeWeightSet();
        var _defaultOverrides = defaultOverrides ?? new BlendShapeWeightSet();
        ExecuteModification(() =>
        {
            for (int i = 0; i < _allKeysArray.Length; i++)
            {
                _overrideFlagsProperty.GetArrayElementAtIndex(i).boolValue = false;
                if (_baseSet.TryGetValue(_allKeysArray[i], out var baseShape))
                {
                    _overrideWeightsProperty.GetArrayElementAtIndex(i).floatValue = baseShape.Weight;
                }
                else
                {
                    _overrideWeightsProperty.GetArrayElementAtIndex(i).floatValue = 0f;
                }
                if (_defaultOverrides.TryGetValue(_allKeysArray[i], out var defaultShape))
                {
                    _overrideFlagsProperty.GetArrayElementAtIndex(i).boolValue = true;
                    _overrideWeightsProperty.GetArrayElementAtIndex(i).floatValue = defaultShape.Weight;
                }
            }
        });

        _initialSnapshot = CaptureCurrentSnapshot();
        _editedSnapshotBeforeRestoreInitial = null;
        _restoreEditedRevision = null;

        OnBaseSetChange?.Invoke();
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
            if (_overrideFlagsProperty.GetArrayElementAtIndex(i).boolValue != snapshot.Flags[i]) return false;
            if (!Mathf.Approximately(_overrideWeightsProperty.GetArrayElementAtIndex(i).floatValue, snapshot.Weights[i])) return false;
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

    public void GetCurrentOverrides(BlendShapeWeightSet resultToAdd)
    {
        var length = _allKeysArray.Length;
        for (int i = 0; i < length; i++)
        {
            if (IsOverridden(i))
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

    public bool IsOverridden(int index) 
    {
        return _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue;
    }
    
    public bool IsBaseShape(int index) 
    {
        return _baseSet.ContainsKey(_allKeysArray[index]);
    }
    
    public float GetShapeWeight(int index) 
    {
        return _overrideWeightsProperty.GetArrayElementAtIndex(index).floatValue;
    }

    public float GetRequiredInitialBaseWeight(string shapeName)
    {
        return _baseSet.TryGetValue(shapeName, out var shape) ? shape.Weight : throw new Exception($"Shape {shapeName} not found in base set");
    }
    public bool IsInitialBaseWeight(int index)
    {
        return _baseSet.TryGetValue(_allKeysArray[index], out var shape) && Mathf.Approximately(shape.Weight, GetShapeWeight(index));
    }

    public IEnumerable<int> GetOverridenIndices(Func<int, bool> predicate)
    {
        for (int i = 0; i < _allKeysArray.Length; i++)
        {
            if (IsOverridden(i) && predicate(i)) yield return i;
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
        }
    }

    private void ExecuteModification(Action action)
    {
        _serializedObject.Update();
        _modificationRevision++;
        action();
        _serializedObject.ApplyModifiedProperties();
        _serializedObject.Update();
    }
    
    // override
    public void OverrideShapeAndSetWeightWithOutApply(int index, float weight)
    {
        _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue = true;
        SetShapeWeightWithOutApply(index, weight);
    }
    public void OverrideShapeAndSetWeight(int index, float weight)
    {
        ExecuteModification(() =>
        {
            OverrideShapeAndSetWeightWithOutApply(index, weight);
            OnSingleShapeOverride?.Invoke(index);
            OnAnyDataChange?.Invoke();
        });
    }
    public void OverrideShapesAndSetWeight(IEnumerable<int> indices, float weight)
    {
        var indicesList = indices as IReadOnlyList<int> ?? indices.ToList();
        ExecuteModification(() =>
        {
            foreach (var index in indicesList) OverrideShapeAndSetWeightWithOutApply(index, weight);
            OnMultipleShapeOverride?.Invoke(indicesList);
            OnAnyDataChange?.Invoke();
        });
    }

    public void OverrideShapesAndSetWeight(IEnumerable<(int, float)> indicesAndWeights)
    {
        var list = indicesAndWeights.ToList();
        ExecuteModification(() =>
        {
            foreach (var (index, weight) in list) OverrideShapeAndSetWeightWithOutApply(index, weight);
            OnMultipleShapeOverride?.Invoke(list.Select(x => x.Item1).ToList());
            OnAnyDataChange?.Invoke();
        });
    }
    public void OverrideShapesAndSetWeight(IReadOnlyCollection<BlendShapeWeight> shapes)
    {
        var indicesAndWeights = shapes.Select(x => (GetIndexForShape(x.Name), x.Weight))
            .Where(pair => pair.Item1 != -1);
        OverrideShapesAndSetWeight(indicesAndWeights);
    }

    // unoverride
    public void UnoverrideShapeWithOutApply(int index)
    {
        _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue = false;
    }
    public void UnoverrideShape(int index)
    {
        ExecuteModification(() =>
        {
            UnoverrideShapeWithOutApply(index);
            OnSingleShapeUnoverride?.Invoke(index);
            OnAnyDataChange?.Invoke();
        });
    }
    public void UnoverrideShapes(IEnumerable<int> indices)
    {
        ExecuteModification(() =>
        {
            foreach (var index in indices) UnoverrideShapeWithOutApply(index);
            OnMultipleShapeUnoverride?.Invoke(indices);
            OnAnyDataChange?.Invoke();
        });
    }

    // weight
    public void SetShapeWeightWithOutApply(int index, float weight)
    {        
        // 先にweightを設定
        _overrideWeightsProperty.GetArrayElementAtIndex(index).floatValue = weight;
        
        // baseシェイプの場合、新しいweightでoverrideフラグを判定
        if (IsBaseShape(index))
        {
            if (IsInitialBaseWeight(index))
            {
                _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue = false;
            }
            else
            {
                _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue = true;
            }
        }
    }
    public void SetShapeWeight(int index, float weight)
    {
        ExecuteModification(() =>
        {
            SetShapeWeightWithOutApply(index, weight);
            OnSingleShapeWeightChanged?.Invoke(index);
            OnAnyDataChange?.Invoke();
        });
    }
    public void SetShapesWeight(IEnumerable<int> indices, float weight)
    {
        ExecuteModification(() =>
        {
            foreach (var index in indices) SetShapeWeightWithOutApply(index, weight);
            OnMultipleShapeWeightChanged?.Invoke(indices);
            OnAnyDataChange?.Invoke();
        });
    }
    public void SetShapesWeight(IEnumerable<(int, float)> indicesAndWeights)
    {
        ExecuteModification(() =>
        {
            foreach (var (index, weight) in indicesAndWeights) SetShapeWeightWithOutApply(index, weight);
            OnMultipleShapeWeightChanged?.Invoke(indicesAndWeights.Select(x => x.Item1));
            OnAnyDataChange?.Invoke();
        });
    }
    
    public float ResetShapeWeightWithOutApply(int index)
    {
        _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue = false;
        var newWeight = GetRequiredInitialBaseWeight(_allKeysArray[index]);
        _overrideWeightsProperty.GetArrayElementAtIndex(index).floatValue = newWeight;
        return newWeight;
    }
    public float ResetShapeWeight(int index)
    {
        float weight = 0f;
        ExecuteModification(() =>
        {
            weight = ResetShapeWeightWithOutApply(index);
            OnSingleShapeWeightChanged?.Invoke(index);
            OnAnyDataChange?.Invoke();
        });
        return weight;
    }
    public void ResetShapesWeight(IEnumerable<int> indices)
    {
        ExecuteModification(() =>
        {
            foreach (var index in indices) ResetShapeWeightWithOutApply(index);
            OnMultipleShapeWeightChanged?.Invoke(indices);
            OnAnyDataChange?.Invoke();
        });
    }

    public void OnUndoRedo()
    {
        _serializedObject.Update();
        OnUnknownChange?.Invoke();
        OnAnyDataChange?.Invoke();
    }

    public void Dispose()
    {
    }
}
