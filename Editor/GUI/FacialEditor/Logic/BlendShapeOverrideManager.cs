namespace aoyon.facetune.ui;

[Serializable]
internal class BlendShapeOverrideData
{
    [SerializeField] internal bool[] _overrideFlags = new bool[0];
    [SerializeField] internal float[] _overrideWeights = new float[0];
}

internal class BlendShapeOverrideManager
{
    private readonly SerializedObject _serializedObject;
    private readonly SerializedProperty _overrideFlagsProperty;
    private readonly SerializedProperty _overrideWeightsProperty;
    private readonly string[] _allKeysArray;
    private readonly BlendShapeSet _styleSet;
    private readonly Dictionary<string, int> _shapeNameToIndexMap;

    public event Action? OnDataModified;

    public BlendShapeOverrideManager(
        SerializedObject serializedObject,
        SerializedProperty dataProperty,
        string[] allKeysArray,
        BlendShapeSet styleSet)
    {
        _serializedObject = serializedObject;
        _allKeysArray = allKeysArray;
        var allkeys = _allKeysArray.ToHashSet();
        _styleSet = styleSet.Where(x => allkeys.Contains(x.Name));
        _overrideFlagsProperty = dataProperty.FindPropertyRelative(nameof(BlendShapeOverrideData._overrideFlags));
        _overrideWeightsProperty = dataProperty.FindPropertyRelative(nameof(BlendShapeOverrideData._overrideWeights));

        _shapeNameToIndexMap = new Dictionary<string, int>(allKeysArray.Length);
        for (int i = 0; i < allKeysArray.Length; i++)
        {
            _shapeNameToIndexMap[allKeysArray[i]] = i;
        }
    }

    public void InitializeData(BlendShapeSet defaultOverrides)
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
            else
            {
                // Ensure clean state for elements not in defaults
                flagProp.boolValue = false;
                weightProp.floatValue = 0f;
            }
        }

        _serializedObject.ApplyModifiedProperties();
    }

    public BlendShapeSet GetCurrentOverrides(BlendShapeSet resultToAdd)
    {
        for (int i = 0; i < _allKeysArray.Length; i++)
        {
            if (IsOverridden(i))
            {
                var shapeName = _allKeysArray[i];
                var weight = GetShapeWeight(shapeName);
                resultToAdd.Add(new BlendShape(shapeName, weight));
            }
        }
        return resultToAdd;
    }

    public int GetIndexForShape(string shapeName)
    {
        return _shapeNameToIndexMap.TryGetValue(shapeName, out var index) ? index : -1;
    }

    public IReadOnlyList<string> GetAllKeys() => _allKeysArray;

    public bool IsOverridden(int index) => _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue;
    public bool IsOverridden(string shapeName) => IsOverridden(GetIndexForShape(shapeName));
    public bool IsStyleShape(int index) => _styleSet.Contains(_allKeysArray[index]);
    public bool IsStyleShape(string shapeName) => IsStyleShape(GetIndexForShape(shapeName));
    
    public void OverrideShape(int index, float weight)
    {
        var flagProp = _overrideFlagsProperty.GetArrayElementAtIndex(index);
        var weightProp = _overrideWeightsProperty.GetArrayElementAtIndex(index);
        var isAlreadySet = flagProp.boolValue && Mathf.Approximately(weightProp.floatValue, weight);

        if (isAlreadySet) return;
        
        _serializedObject.Update();
        // スタイルシェイプでデフォルト値に戻す場合は、オーバーライドを解除する
        if (_styleSet.TryGetValue(_allKeysArray[index], out var styleShape) && Mathf.Approximately(styleShape.Weight, weight))
        {
            if (flagProp.boolValue)
            {
                flagProp.boolValue = false;
                _serializedObject.ApplyModifiedProperties();
                OnDataModified?.Invoke();
            }
            return;
        }

        flagProp.boolValue = true;
        weightProp.floatValue = weight;
        _serializedObject.ApplyModifiedProperties();
        OnDataModified?.Invoke();
    }
    public void OverrideShape(string shapeName, float weight) => OverrideShape(GetIndexForShape(shapeName), weight);
    public void OverrideShapes(IEnumerable<int> indices, float weight)
    {
        _serializedObject.Update();
        bool modified = false;
        foreach (var index in indices)
        {
            if (index < 0 || index >= _allKeysArray.Length) continue;

            // スタイルシェイプでデフォルト値と同じ場合はスキップ
            if (_styleSet.TryGetValue(_allKeysArray[index], out var styleShape) && Mathf.Approximately(styleShape.Weight, weight)) continue;

            var flagProp = _overrideFlagsProperty.GetArrayElementAtIndex(index);
            var weightProp = _overrideWeightsProperty.GetArrayElementAtIndex(index);

            if (!flagProp.boolValue || !Mathf.Approximately(weightProp.floatValue, weight))
            {
                flagProp.boolValue = true;
                weightProp.floatValue = weight;
                modified = true;
            }
        }
        if (modified)
        {
            _serializedObject.ApplyModifiedProperties();
            OnDataModified?.Invoke();
        }
    }

    public void UnoverrideShape(int index)
    {
        var prop = _overrideFlagsProperty.GetArrayElementAtIndex(index);
        if (!prop.boolValue) return;

        _serializedObject.Update();
        prop.boolValue = false;
        _serializedObject.ApplyModifiedProperties();
        OnDataModified?.Invoke();
    }
    public void UnoverrideShape(string shapeName) => UnoverrideShape(GetIndexForShape(shapeName));
    public void UnoverrideShapes(IEnumerable<int> indices)
    {
        _serializedObject.Update();
        bool modified = false;
        foreach (var index in indices)
        {
            var prop = _overrideFlagsProperty.GetArrayElementAtIndex(index);
            if (prop.boolValue)
            {
                prop.boolValue = false;
                modified = true;
            }
        }
        if (modified)
        {
            _serializedObject.ApplyModifiedProperties();
            OnDataModified?.Invoke();
        }
    }

    public float GetInitialWeight(string shapeName)
    {
        return _styleSet.TryGetValue(shapeName, out var shape) ? shape.Weight : 0f;
    }

    public float GetShapeWeight(int index) => _overrideWeightsProperty.GetArrayElementAtIndex(index).floatValue;
    public float GetShapeWeight(string shapeName) => GetShapeWeight(GetIndexForShape(shapeName));

    public void SetShapeWeight(int index, float weight)
    {
        var prop = _overrideWeightsProperty.GetArrayElementAtIndex(index);
        if (Mathf.Approximately(prop.floatValue, weight)) return;

        _serializedObject.Update();
        prop.floatValue = weight;
        _serializedObject.ApplyModifiedProperties();
        OnDataModified?.Invoke();
    }
    public void SetShapeWeight(string shapeName, float weight) => SetShapeWeight(GetIndexForShape(shapeName), weight);

    public void SetShapesWeight(IEnumerable<string> shapeNames, float weight)
    {
        _serializedObject.Update();
        bool modified = false;
        foreach (var shapeName in shapeNames)
        {
            var index = GetIndexForShape(shapeName);
            if (index != -1)
            {
                var prop = _overrideWeightsProperty.GetArrayElementAtIndex(index);
                if (!Mathf.Approximately(prop.floatValue, weight))
                {
                    prop.floatValue = weight;
                    modified = true;
                }
            }
        }
        if (modified)
        {
            _serializedObject.ApplyModifiedProperties();
            OnDataModified?.Invoke();
        }
    }
}
