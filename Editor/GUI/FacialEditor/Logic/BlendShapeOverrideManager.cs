namespace aoyon.facetune.gui.shapes_editor;

// styleはは初期状態から変化した場合のみoverride
// 通常のブレンドシェイプはweightに関わらず、overrideかどうかで判断
internal class BlendShapeOverrideManager : IDisposable
{
    private readonly SerializedObject _serializedObject;

    private readonly SerializedProperty _overrideFlagsProperty;
    private readonly SerializedProperty _overrideWeightsProperty;
    private readonly string[] _allKeysArray;
    private readonly IReadOnlyBlendShapeSet _styleSet;
    private readonly Dictionary<string, int> _shapeNameToIndexMap;

    public IReadOnlyList<string> AllKeys => _allKeysArray;
    public IReadOnlyBlendShapeSet StyleSet => _styleSet;
    public event Action<int>? OnSingleShapeOverride;
    public event Action<IEnumerable<int>>? OnMultipleShapeOverride;
    public event Action<int>? OnSingleShapeUnoverride;
    public event Action<IEnumerable<int>>? OnMultipleShapeUnoverride;
    public event Action<int>? OnSingleShapeWeightChanged;
    public event Action<IEnumerable<int>>? OnMultipleShapeWeightChanged;
    public event Action? OnUnknownChange;
    public event Action? OnAnyDataChange;

    public BlendShapeOverrideManager(
        SerializedObject serializedObject,
        SerializedProperty overrideFlagsProperty,
        SerializedProperty overrideWeightsProperty,
        string[] allKeysArray,
        IReadOnlyBlendShapeSet styleSet,
        IReadOnlyBlendShapeSet defaultOverrides)
    {
        _serializedObject = serializedObject;
        _allKeysArray = allKeysArray;
        var allkeys = _allKeysArray.ToHashSet();
        _styleSet = styleSet.Where(x => allkeys.Contains(x.Name)).AsReadOnly();
        
        // SerializedPropertyを直接設定
        _overrideFlagsProperty = overrideFlagsProperty;
        _overrideWeightsProperty = overrideWeightsProperty;

        _shapeNameToIndexMap = new Dictionary<string, int>(allKeysArray.Length);
        for (int i = 0; i < allKeysArray.Length; i++)
        {
            _shapeNameToIndexMap[allKeysArray[i]] = i;
        }
        
        InitializeData(defaultOverrides);
        Undo.undoRedoPerformed += OnUndoRedoPerformed;
        OnAnyDataChange += () =>
        {
            ValidateData();
        };
        OnAnyDataChange += () =>
        {
            /*
            var overrideCount = 0;
            for (int i = 0; i < _allKeysArray.Length; i++)
            {
                if (IsOverridden(i))
                    overrideCount++;
            }
            Debug.Log($"overrideCount: {overrideCount}");
            */
        };
    }

    private void InitializeData(IReadOnlyBlendShapeSet defaultOverrides)
    {
        _serializedObject.Update();

        _overrideFlagsProperty.arraySize = _allKeysArray.Length;
        _overrideWeightsProperty.arraySize = _allKeysArray.Length;

        for (int i = 0; i < _allKeysArray.Length; i++)
        {
            var flagProp = _overrideFlagsProperty.GetArrayElementAtIndex(i);
            var weightProp = _overrideWeightsProperty.GetArrayElementAtIndex(i);

            if (defaultOverrides.TryGetValue(_allKeysArray[i], out var shape))
            {
                flagProp.boolValue = true;
                weightProp.floatValue = shape.Weight;
            }
            else if (_styleSet.TryGetValue(_allKeysArray[i], out var styleShape))
            {
                flagProp.boolValue = false;
                weightProp.floatValue = styleShape.Weight;
            }
            else
            {
                flagProp.boolValue = false;
                weightProp.floatValue = 0f;
            }
        }

        _serializedObject.ApplyModifiedProperties();
        _serializedObject.Update();
    }

    public void GetCurrentOverrides(BlendShapeSet resultToAdd)
    {
        var length = _allKeysArray.Length;
        for (int i = 0; i < length; i++)
        {
            if (IsOverridden(i))
            {
                var shapeName = _allKeysArray[i];
                var weight = GetShapeWeight(i);
                resultToAdd.Add(new BlendShape(shapeName, weight));
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
    
    public bool IsStyleShape(int index) 
    {
        return _styleSet.Contains(_allKeysArray[index]);
    }
    
    public float GetShapeWeight(int index) 
    {
        return _overrideWeightsProperty.GetArrayElementAtIndex(index).floatValue;
    }

    public float GetRequiredInitialStyleWeight(string shapeName)
    {
        return _styleSet.TryGetValue(shapeName, out var shape) ? shape.Weight : throw new Exception($"Shape {shapeName} not found in style set");
    }
    public bool IsInitialStyleWeight(int index)
    {
        return _styleSet.TryGetValue(_allKeysArray[index], out var shape) && Mathf.Approximately(shape.Weight, GetShapeWeight(index));
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
        ExecuteModification(() =>
        {
            foreach (var index in indices) OverrideShapeAndSetWeightWithOutApply(index, weight);
            OnMultipleShapeOverride?.Invoke(indices);
            OnAnyDataChange?.Invoke();
        });
    }

    public void OverrideShapesAndSetWeight(IEnumerable<(int, float)> indicesAndWeights)
    {
        ExecuteModification(() =>
        {
            foreach (var (index, weight) in indicesAndWeights) OverrideShapeAndSetWeightWithOutApply(index, weight);
            OnMultipleShapeOverride?.Invoke(indicesAndWeights.Select(x => x.Item1));
            OnAnyDataChange?.Invoke();
        });
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
        
        // styleシェイプの場合、新しいweightでoverrideフラグを判定
        if (IsStyleShape(index))
        {
            if (IsInitialStyleWeight(index))
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
        var newWeight = GetRequiredInitialStyleWeight(_allKeysArray[index]);
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

    private void OnUndoRedoPerformed()
    {
        _serializedObject.Update();
        OnUnknownChange?.Invoke();
        OnAnyDataChange?.Invoke();
    }

    public void Dispose()
    {
        Undo.undoRedoPerformed -= OnUndoRedoPerformed;
    }
}
