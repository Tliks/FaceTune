namespace Aoyon.FaceTune.Gui.ShapesEditor;

// flagは保存対象のtarget listに含まれるかを表す。weightはflagが立つ場合だけ出力値として意味を持つ。
// keysが2以上のcurveを持つ行はカーブモードとし、weightは先頭キーに同期する。
[Serializable]
internal class BlendShapeOverrideManager : IDisposable
{
    private readonly struct OverrideStateSnapshot
    {
        public readonly bool[] Flags;
        public readonly float[] Weights;
        public readonly AnimationCurve?[] Curves;

        public OverrideStateSnapshot(bool[] flags, float[] weights, AnimationCurve?[] curves)
        {
            Flags = flags;
            Weights = weights;
            Curves = curves;
        }
    }

    private SerializedObject _serializedObject;
    [SerializeField] private bool[] _overrideFlags = null!;
    [SerializeField] private float[] _overrideWeights = null!;
    [SerializeField] private AnimationCurve[] _overrideCurves = null!;
    // Keep the change marker serialized so Unity Undo restores it with the edited values.
    [SerializeField] private int _stateVersion;
    private SerializedProperty _overrideFlagsProperty;
    private SerializedProperty _overrideWeightsProperty;
    private SerializedProperty _overrideCurvesProperty;
    private SerializedProperty _stateVersionProperty;

    private OverrideStateSnapshot? _initialSnapshot;
    private OverrideStateSnapshot? _editedSnapshotBeforeRestoreInitial;
    private int? _restoreStateVersion;
    private int _initialStateVersion;
    private int _maximumStateVersion;
    private int _lastObservedStateVersion;
    private bool _canRedo;
    private int _changedStateVersion;
    private bool _hasChangedStateCache;
    private bool _changedFromInitialState;

    public bool IsChangedFromInitialState
    {
        get
        {
            var currentVersion = _stateVersionProperty.intValue;
            if (!_hasChangedStateCache || _changedStateVersion != currentVersion)
            {
                _changedStateVersion = currentVersion;
                _changedFromInitialState = _initialSnapshot.HasValue
                    && !IsSameAsSnapshot(_initialSnapshot.Value);
                _hasChangedStateCache = true;
            }
            return _changedFromInitialState;
        }
    }

    public bool CanUndo => _stateVersionProperty.intValue > _initialStateVersion;
    public bool CanRedo => _canRedo;
    public bool CanRestoreEditedOverrides => _editedSnapshotBeforeRestoreInitial.HasValue
                                            && _restoreStateVersion == _stateVersionProperty.intValue;

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
    public ISet<string> ExplicitlyExcluded => _explicitlyExcluded;
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
        _overrideCurvesProperty = baseProperty.FindPropertyRelative(nameof(_overrideCurves));
        _stateVersionProperty = baseProperty.FindPropertyRelative(nameof(_stateVersion));
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
        ISet<string> explicitlyExcluded,
        IReadOnlyDictionary<string, AnimationCurve>? initialCurves = null)
    {
        _explicitlyExcluded = explicitlyExcluded;
        InitializeTargetRenderer(targetRenderer, explicitlyExcluded);
        InitializeSourceSets(facialSet, baseSet, targetSet, initialCurves);
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
        _overrideCurvesProperty.arraySize = _allKeysArray.Length;
        _serializedObject.ApplyModifiedPropertiesWithoutUndo();
        _serializedObject.Update();
    }

    private void InitializeSourceSets(
        IReadOnlyBlendShapeSet? facialSet,
        IReadOnlyBlendShapeSet? baseSet,
        IReadOnlyBlendShapeSet? targetSet,
        IReadOnlyDictionary<string, AnimationCurve>? initialCurves)
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
                _overrideCurvesProperty.GetArrayElementAtIndex(i).animationCurveValue = new AnimationCurve();
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
                    if (initialCurves != null
                        && initialCurves.TryGetValue(_allKeysArray[i], out var seedCurve)
                        && seedCurve.keys.Length >= 2)
                    {
                        _overrideCurvesProperty.GetArrayElementAtIndex(i).animationCurveValue = seedCurve;
                    }
                }
            }
        }, registerUndo: false);

        _stateVersionProperty.intValue = 0;
        _serializedObject.ApplyModifiedPropertiesWithoutUndo();
        _serializedObject.UpdateIfRequiredOrScript();
        _initialStateVersion = _stateVersionProperty.intValue;
        _maximumStateVersion = _initialStateVersion;
        _lastObservedStateVersion = _stateVersionProperty.intValue;
        _canRedo = false;
        _initialSnapshot = CaptureCurrentSnapshot();
        _editedSnapshotBeforeRestoreInitial = null;
        _restoreStateVersion = null;
        _hasChangedStateCache = false;

        OnAnyDataChange?.Invoke();
    }

    private OverrideStateSnapshot CaptureCurrentSnapshot()
    {
        var length = _overrideFlagsProperty.arraySize;
        var flags = new bool[length];
        var weights = new float[length];
        var curves = new AnimationCurve?[length];
        for (int i = 0; i < length; i++)
        {
            flags[i] = _overrideFlagsProperty.GetArrayElementAtIndex(i).boolValue;
            weights[i] = _overrideWeightsProperty.GetArrayElementAtIndex(i).floatValue;
            curves[i] = IsCurveModeAt(i) ? GetCurveValueAt(i) : null;
        }
        return new OverrideStateSnapshot(flags, weights, curves);
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
            if (!isOverridden) continue;
            if (!Mathf.Approximately(
                    _overrideWeightsProperty.GetArrayElementAtIndex(i).floatValue,
                    snapshot.Weights[i]))
                return false;
            if (!Equals(
                    IsCurveModeAt(i) ? GetCurveValueAt(i) : null,
                    snapshot.Curves[i]))
                return false;
        }
        return true;
    }

    private void ApplySnapshot(OverrideStateSnapshot snapshot, bool registerUndo = true)
    {
        if (!ExecuteModification(() =>
        {
            _overrideFlagsProperty.arraySize = snapshot.Flags.Length;
            _overrideWeightsProperty.arraySize = snapshot.Weights.Length;
            _overrideCurvesProperty.arraySize = snapshot.Curves.Length;
            var length = Mathf.Min(
                _overrideFlagsProperty.arraySize,
                _overrideWeightsProperty.arraySize,
                _overrideCurvesProperty.arraySize);
            for (int i = 0; i < length; i++)
            {
                _overrideFlagsProperty.GetArrayElementAtIndex(i).boolValue = snapshot.Flags[i];
                _overrideWeightsProperty.GetArrayElementAtIndex(i).floatValue = snapshot.Weights[i];
                _overrideCurvesProperty.GetArrayElementAtIndex(i).animationCurveValue
                    = snapshot.Curves[i] ?? new AnimationCurve();
            }
        }, registerUndo)) return;
        OnUnknownChange?.Invoke();
        OnAnyDataChange?.Invoke();
    }

    public bool TryRestoreInitialOverrides()
    {
        if (!_initialSnapshot.HasValue) return false;
        if (!IsChangedFromInitialState) return false;
        _editedSnapshotBeforeRestoreInitial = CaptureCurrentSnapshot();
        ApplySnapshot(_initialSnapshot.Value);
        _restoreStateVersion = _stateVersionProperty.intValue;
        return true;
    }

    public bool TryDiscardToInitialOverrides()
    {
        if (!_initialSnapshot.HasValue) return false;
        _editedSnapshotBeforeRestoreInitial = null;
        _restoreStateVersion = null;
        Undo.ClearUndo(_serializedObject.targetObject);
        _stateVersionProperty.intValue = _initialStateVersion;
        _serializedObject.ApplyModifiedPropertiesWithoutUndo();
        _serializedObject.UpdateIfRequiredOrScript();
        _lastObservedStateVersion = _initialStateVersion;
        _maximumStateVersion = _initialStateVersion;
        _canRedo = false;
        ApplySnapshot(_initialSnapshot.Value, registerUndo: false);
        return true;
    }

    public void MarkCurrentAsInitialState()
    {
        _initialSnapshot = CaptureCurrentSnapshot();
        _editedSnapshotBeforeRestoreInitial = null;
        _restoreStateVersion = null;
        _hasChangedStateCache = false;
        OnAnyDataChange?.Invoke();
    }

    public bool TryRestoreEditedOverrides()
    {
        if (!CanRestoreEditedOverrides) return false;
        ApplySnapshot(_editedSnapshotBeforeRestoreInitial!.Value);
        _editedSnapshotBeforeRestoreInitial = null;
        _restoreStateVersion = null;
        return true;
    }

    public bool SynchronizeSerializedState()
    {
        _serializedObject.UpdateIfRequiredOrScript();
        var currentVersion = _stateVersionProperty.intValue;
        if (currentVersion == _lastObservedStateVersion)
        {
            // フローティングのカーブ編集ウィンドウはversionを進めずに適用するため、変化状態を再確認する。
            if (!_hasChangedStateCache) return false;
            var cached = _changedFromInitialState;
            _hasChangedStateCache = false;
            if (IsChangedFromInitialState == cached) return false;

            OnUnknownChange?.Invoke();
            OnAnyDataChange?.Invoke();
            return true;
        }

        if (currentVersion < _lastObservedStateVersion)
            _canRedo = true;
        else if (currentVersion > _lastObservedStateVersion)
            _canRedo = currentVersion < _maximumStateVersion;

        _lastObservedStateVersion = currentVersion;
        _hasChangedStateCache = false;
        OnUnknownChange?.Invoke();
        OnAnyDataChange?.Invoke();
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

    /// <summary>この行がeditorで編集対象として管理されているか。falseのshape名は保存時にオリジナルのカーブが保持される。</summary>
    public bool Manages(string shapeName) => _shapeNameToIndexMap.ContainsKey(shapeName);

    public bool IsCurveMode(int index) => IsCurveModeAt(index);

    public SerializedProperty GetCurveProperty(int index)
        => _overrideCurvesProperty.GetArrayElementAtIndex(index);

    private bool IsCurveModeAt(int index)
        => index < _overrideCurvesProperty.arraySize
           && GetCurveValueAt(index).length >= 2;

    private AnimationCurve GetCurveValueAt(int index)
        => _overrideCurvesProperty.GetArrayElementAtIndex(index).animationCurveValue;

    public void ToggleCurveMode(int index)
    {
        if (!IsInTarget(index)) return;

        if (IsCurveModeAt(index))
        {
            if (!ExecuteModification(() =>
            {
                _overrideWeightsProperty.GetArrayElementAtIndex(index).floatValue
                    = GetCurveValueAt(index).Evaluate(0f);
                _overrideCurvesProperty.GetArrayElementAtIndex(index).animationCurveValue
                    = new AnimationCurve();
            })) return;
        }
        else
        {
            var weight = GetShapeWeight(index);
            if (!ExecuteModification(() =>
            {
                _overrideCurvesProperty.GetArrayElementAtIndex(index).animationCurveValue
                    = new AnimationCurve(new Keyframe(0f, weight), new Keyframe(1f, weight));
            })) return;
        }

        OnSingleShapeWeightChanged?.Invoke(index);
        OnUnknownChange?.Invoke();
        OnAnyDataChange?.Invoke();
    }

    /// <summary>カーブ編集UIの確定。フローティングウィンドウ側が先にpropertyを適用しているケースがあるため、常に状態を再評価する。</summary>
    public void CommitCurveEdit(int index, AnimationCurve curve)
    {
        var isCurveMode = curve.keys.Length >= 2;
        ExecuteModification(() =>
        {
            _overrideCurvesProperty.GetArrayElementAtIndex(index).animationCurveValue
                = isCurveMode ? curve : new AnimationCurve();
            _overrideWeightsProperty.GetArrayElementAtIndex(index).floatValue = curve.Evaluate(0f);
        });
        _hasChangedStateCache = false;
        OnSingleShapeWeightChanged?.Invoke(index);
        OnUnknownChange?.Invoke();
        OnAnyDataChange?.Invoke();
    }

    public void GetTargetAnimations(List<BlendShapeWeightAnimation> resultToAdd)
    {
        var length = _allKeysArray.Length;
        for (int i = 0; i < length; i++)
        {
            if (!IsInTarget(i)) continue;
            var shapeName = _allKeysArray[i];
            resultToAdd.Add(IsCurveModeAt(i)
                ? new BlendShapeWeightAnimation(shapeName, GetCurveValueAt(i))
                : BlendShapeWeightAnimation.SingleFrame(shapeName, GetShapeWeight(i)));
        }
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
            _overrideWeightsProperty.arraySize != _allKeysArray.Length ||
            _overrideCurvesProperty.arraySize != _allKeysArray.Length)
        {
            Debug.LogWarning($"Array size mismatch detected. Resynchronizing: " +
                $"_allKeysArray.Length: {_allKeysArray.Length}, " +
                $"_overrideFlagsProperty.arraySize: {_overrideFlagsProperty.arraySize}, " +
                $"_overrideWeightsProperty.arraySize: {_overrideWeightsProperty.arraySize}, " +
                $"_overrideCurvesProperty.arraySize: {_overrideCurvesProperty.arraySize}");

            _overrideFlagsProperty.arraySize = _allKeysArray.Length;
            _overrideWeightsProperty.arraySize = _allKeysArray.Length;
            _overrideCurvesProperty.arraySize = _allKeysArray.Length;
            _serializedObject.ApplyModifiedPropertiesWithoutUndo();
            _serializedObject.Update();
        }
    }

    private bool ExecuteModification(Action action, bool registerUndo = true)
    {
        _serializedObject.UpdateIfRequiredOrScript();
        ValidateData();
        action();
        if (!_serializedObject.hasModifiedProperties)
        {
            _serializedObject.UpdateIfRequiredOrScript();
            return false;
        }

        if (registerUndo)
        {
            _stateVersionProperty.intValue++;
            _serializedObject.ApplyModifiedProperties();
            _lastObservedStateVersion = _stateVersionProperty.intValue;
            _maximumStateVersion = _lastObservedStateVersion;
            _canRedo = false;
        }
        else
        {
            _serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
        _hasChangedStateCache = false;
        return true;
    }
    
    public void AddShapeWithWeightWithoutApply(int index, float weight)
    {
        _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue = true;
        SetShapeWeightWithoutApply(index, weight);
    }
    public void AddShapeWithWeight(int index, float weight)
    {
        if (!ExecuteModification(() => AddShapeWithWeightWithoutApply(index, weight))) return;
        OnSingleShapeAdded?.Invoke(index);
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
        if (!ExecuteModification(() =>
        {
            foreach (var (index, weight) in list) AddShapeWithWeightWithoutApply(index, weight);
        })) return;
        OnMultipleShapesAdded?.Invoke(list.Select(x => x.Key).ToArray());
        OnAnyDataChange?.Invoke();
    }

    public void RemoveShapeWithoutApply(int index)
    {
        _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue = false;
        _overrideCurvesProperty.GetArrayElementAtIndex(index).animationCurveValue = new AnimationCurve();
    }
    public void RemoveShape(int index)
    {
        if (!ExecuteModification(() => RemoveShapeWithoutApply(index))) return;
        OnSingleShapeRemoved?.Invoke(index);
        OnAnyDataChange?.Invoke();
    }
    public void RemoveShapes(IEnumerable<int> indices)
    {
        var list = indices.Distinct().ToArray();
        if (!ExecuteModification(() =>
        {
            foreach (var index in list) RemoveShapeWithoutApply(index);
        })) return;
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
        if (IsCurveModeAt(index)) return;
        if (!ExecuteModification(() => SetShapeWeightWithoutApply(index, weight))) return;
        OnSingleShapeWeightChanged?.Invoke(index);
        OnAnyDataChange?.Invoke();
    }
    public void SetShapesWeight(IEnumerable<int> indices, float weight)
    {
        var list = indices.Distinct().Where(index => !IsCurveModeAt(index)).ToArray();
        if (list.Length == 0) return;
        if (!ExecuteModification(() =>
        {
            foreach (var index in list) SetShapeWeightWithoutApply(index, weight);
        })) return;
        OnMultipleShapeWeightChanged?.Invoke(list);
        OnAnyDataChange?.Invoke();
    }
    public void SetShapesWeight(IEnumerable<(int, float)> indicesAndWeights)
    {
        var list = indicesAndWeights
            .Where(pair => !IsCurveModeAt(pair.Item1))
            .ToArray();
        if (list.Length == 0) return;
        if (!ExecuteModification(() =>
        {
            foreach (var (index, weight) in list) SetShapeWeightWithoutApply(index, weight);
        })) return;
        OnMultipleShapeWeightChanged?.Invoke(list.Select(x => x.Item1).ToArray());
        OnAnyDataChange?.Invoke();
    }

    /// <summary>clip import用。MultiFrameのanimationはカーブモードで追加する。</summary>
    public void AddShapesWithAnimations(IEnumerable<BlendShapeWeightAnimation> animations)
    {
        var targets = new List<(int Index, BlendShapeWeightAnimation Animation)>();
        foreach (var animation in animations)
        {
            var index = GetIndexForShape(animation.Name);
            if (index >= 0) targets.Add((index, animation));
        }
        if (!ExecuteModification(() =>
        {
            foreach (var (index, animation) in targets)
            {
                _overrideFlagsProperty.GetArrayElementAtIndex(index).boolValue = true;
                _overrideWeightsProperty.GetArrayElementAtIndex(index).floatValue = animation.Weight(0f);
                _overrideCurvesProperty.GetArrayElementAtIndex(index).animationCurveValue
                    = animation.IsMultiFrame ? animation.Curve : new AnimationCurve();
            }
        })) return;
        OnMultipleShapesAdded?.Invoke(targets.Select(target => target.Index).ToArray());
        OnAnyDataChange?.Invoke();
    }
    
    public void Dispose()
    {
    }
}
