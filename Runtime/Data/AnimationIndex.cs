namespace com.aoyon.facetune;

// GenericAnimationのコレクションに対するアクセス・簡易な編集を行うためのラッパーオブジェクト
// 重複を許容しない、Dictionary基盤の設計
internal class AnimationIndex : ICollection<GenericAnimation>
{
    // メインデータ構造: Path -> PropertyName -> GenericAnimation
    private Dictionary<string, Dictionary<string, GenericAnimation>> _pathPropertyAnimationMap;
    
    // BlendShapeAnimation用のキャッシュ
    private Dictionary<string, Dictionary<string, BlendShapeAnimation>>? _pathNameBlendShapeAnimationMap;
    private Dictionary<string, BlendShapeSet>? _pathFirstFrameBlendShapeSets; 
    private BlendShapeSet? _allFirstFrameBlendShapeSet;
    private bool _blendShapeCacheValid = false;

    private static readonly string BlendShapePrefix = FaceTuneConsts.AnimatedBlendShapePrefix;

    public int Count => _pathPropertyAnimationMap.Values.Sum(dict => dict.Count);
    public bool IsReadOnly => false;
    
    public IReadOnlyList<GenericAnimation> Animations => 
        _pathPropertyAnimationMap.Values.SelectMany(dict => dict.Values).ToList().AsReadOnly();

    public AnimationIndex(IEnumerable<GenericAnimation> animations)
    {
        _pathPropertyAnimationMap = new Dictionary<string, Dictionary<string, GenericAnimation>>();
        foreach (var animation in animations)
        {
            AddInternal(animation);
        }
    }

    public AnimationIndex()
    {
        _pathPropertyAnimationMap = new Dictionary<string, Dictionary<string, GenericAnimation>>();
    }

    private void AddInternal(GenericAnimation animation)
    {
        var path = animation.CurveBinding.Path;
        var propertyName = animation.CurveBinding.PropertyName;
        
        // 重複は上書きする（最新のものを保持）
        _pathPropertyAnimationMap.GetOrAddNew(path)[propertyName] = animation;
    }

    private void InvalidateBlendShapeCache()
    {
        _blendShapeCacheValid = false;
        _pathNameBlendShapeAnimationMap = null;
        _pathFirstFrameBlendShapeSets = null;
        _allFirstFrameBlendShapeSet = null;
    }

    private Dictionary<string, Dictionary<string, BlendShapeAnimation>> GetPathNameToBlendShapeAnimationsMap()
    {
        if (!_blendShapeCacheValid || _pathNameBlendShapeAnimationMap == null)
        {
            var mapping = new Dictionary<string, Dictionary<string, BlendShapeAnimation>>();
            foreach (var pathEntry in _pathPropertyAnimationMap)
            {
                foreach (var propertyEntry in pathEntry.Value)
                {
                    if (propertyEntry.Value.TryToBlendShapeAnimation(out var blendShapeAnimation))
                    {
                        mapping.GetOrAddNew(pathEntry.Key)[propertyEntry.Key] = blendShapeAnimation;
                    }
                }
            }
            _pathNameBlendShapeAnimationMap = mapping;
            _blendShapeCacheValid = true;
        }
        return _pathNameBlendShapeAnimationMap;
    }

    // Get methods
    public bool TryGetAnimation(string path, string propertyName, [NotNullWhen(true)] out GenericAnimation? animation)
    {
        animation = null;
        return _pathPropertyAnimationMap.TryGetValue(path, out var propertyMap) && 
               propertyMap.TryGetValue(propertyName, out animation);
    }

    public bool TryGetBlendShapeAnimation(string path, string name, [NotNullWhen(true)] out BlendShapeAnimation? animation)
    {
        animation = null;
        var pathNameToBlendShapeAnimationsMap = GetPathNameToBlendShapeAnimationsMap();
        return pathNameToBlendShapeAnimationsMap.TryGetValue(path, out var nameToAnimationsMap) && 
               nameToAnimationsMap.TryGetValue(name, out animation);
    }

    public IEnumerable<string> GetBlendShapeNames(string path)
    {
        var pathNameToBlendShapeAnimationsMap = GetPathNameToBlendShapeAnimationsMap();
        return pathNameToBlendShapeAnimationsMap.TryGetValue(path, out var nameToAnimationsMap) ? 
               nameToAnimationsMap.Keys : Array.Empty<string>();
    }
    
    public IEnumerable<string> GetAllBlendShapeNames()
    {
        var pathNameToBlendShapeAnimationsMap = GetPathNameToBlendShapeAnimationsMap();
        return pathNameToBlendShapeAnimationsMap.SelectMany(x => x.Value.Select(y => y.Value.Name)).Distinct();
    }

    public bool TryGetFirstFrameBlendShapeSet(string path, [NotNullWhen(true)] out BlendShapeSet? blendShapeSet)
    {
        if (!_blendShapeCacheValid || _pathFirstFrameBlendShapeSets == null)
        {
            var pathNameToBlendShapeAnimationsMap = GetPathNameToBlendShapeAnimationsMap();
            _pathFirstFrameBlendShapeSets = new Dictionary<string, BlendShapeSet>();
            
            foreach (var pathEntry in pathNameToBlendShapeAnimationsMap)
            {
                var blendShapes = pathEntry.Value.Values.Select(x => x.ToFirstFrameBlendShape()).ToList();
                _pathFirstFrameBlendShapeSets[pathEntry.Key] = new BlendShapeSet(blendShapes);
            }
            _blendShapeCacheValid = true;
        }
        
        if (_pathFirstFrameBlendShapeSets.TryGetValue(path, out blendShapeSet))
        {
            blendShapeSet = blendShapeSet.Clone();
            return true;
        }
        
        blendShapeSet = null;
        return false;
    }

    public BlendShapeSet GetAllFirstFrameBlendShapeSet()
    {
        if (_allFirstFrameBlendShapeSet == null)
        {
            var pathNameToBlendShapeAnimationsMap = GetPathNameToBlendShapeAnimationsMap();
            var blendShapes = pathNameToBlendShapeAnimationsMap.SelectMany(x => x.Value.Values.Select(y => y.ToFirstFrameBlendShape())).ToList();
            _allFirstFrameBlendShapeSet = new BlendShapeSet(blendShapes);
        }
        return _allFirstFrameBlendShapeSet.Clone();
    }

    // ICollection implementation
    public void Add(GenericAnimation animation)
    {
        AddInternal(animation);
        InvalidateBlendShapeCache();
    }

    public void AddRange(IEnumerable<GenericAnimation> animations)
    {
        foreach (var animation in animations)
        {
            Add(animation);
        }
    }

    public void AddSingleFrameBlendShapeAnimation(string path, string name, float weight)
    {
        var animation = BlendShapeAnimation.SingleFrame(name, weight).ToGeneric(path);
        Add(animation);
    }

    public void Clear()
    {
        _pathPropertyAnimationMap.Clear();
        InvalidateBlendShapeCache();
    }

    public bool Contains(GenericAnimation item)
    {
        var path = item.CurveBinding.Path;
        var propertyName = item.CurveBinding.PropertyName;
        
        if (_pathPropertyAnimationMap.TryGetValue(path, out var propertyMap) &&
            propertyMap.TryGetValue(propertyName, out var existing))
        {
            return existing.Equals(item);
        }
        return false;
    }

    public void CopyTo(GenericAnimation[] array, int arrayIndex)
    {
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < Count) throw new ArgumentException("Array is too small");

        var index = arrayIndex;
        foreach (var animation in this)
        {
            array[index++] = animation;
        }
    }

    public bool Remove(GenericAnimation item)
    {
        var path = item.CurveBinding.Path;
        var propertyName = item.CurveBinding.PropertyName;
        
        if (_pathPropertyAnimationMap.TryGetValue(path, out var propertyMap) &&
            propertyMap.ContainsKey(propertyName))
        {
            var removed = propertyMap.Remove(propertyName);
            if (propertyMap.Count == 0)
            {
                _pathPropertyAnimationMap.Remove(path);
            }
            if (removed)
            {
                InvalidateBlendShapeCache();
            }
            return removed;
        }
        return false;
    }

    public IEnumerator<GenericAnimation> GetEnumerator()
    {
        return _pathPropertyAnimationMap.Values
            .SelectMany(dict => dict.Values)
            .GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    // Replace methods
    public void ReplacePropertyNames(Dictionary<string, string> propertyMapping)
    {
        var newMap = new Dictionary<string, Dictionary<string, GenericAnimation>>();
        
        foreach (var pathEntry in _pathPropertyAnimationMap)
        {
            var newPropertyMap = new Dictionary<string, GenericAnimation>();
            
            foreach (var propertyEntry in pathEntry.Value)
            {
                var oldPropertyName = propertyEntry.Key;
                var newPropertyName = propertyMapping.TryGetValue(oldPropertyName, out var mapped) ? mapped : oldPropertyName;
                var newAnimation = propertyEntry.Value with 
                { 
                    CurveBinding = propertyEntry.Value.CurveBinding with { PropertyName = newPropertyName } 
                };
                newPropertyMap[newPropertyName] = newAnimation;
            }
            
            newMap[pathEntry.Key] = newPropertyMap;
        }
        
        _pathPropertyAnimationMap = newMap;
        InvalidateBlendShapeCache();
    }

    public void ReplaceBlendShapeNames(string path, Dictionary<string, string> shapeMapping)
    {
        var propertyMapping = shapeMapping.ToDictionary(
            x => $"{BlendShapePrefix}{x.Key}", 
            x => $"{BlendShapePrefix}{x.Value}"
        );
        
        if (_pathPropertyAnimationMap.TryGetValue(path, out var propertyMap))
        {
            var newPropertyMap = new Dictionary<string, GenericAnimation>();
            
            foreach (var propertyEntry in propertyMap)
            {
                var oldPropertyName = propertyEntry.Key;
                var newPropertyName = propertyMapping.TryGetValue(oldPropertyName, out var mapped) ? mapped : oldPropertyName;
                var newAnimation = propertyEntry.Value with 
                { 
                    CurveBinding = propertyEntry.Value.CurveBinding with { PropertyName = newPropertyName } 
                };
                newPropertyMap[newPropertyName] = newAnimation;
            }
            
            _pathPropertyAnimationMap[path] = newPropertyMap;
            InvalidateBlendShapeCache();
        }
    }

    // Remove methods
    public void RemoveProperties(IEnumerable<string> namesToRemove)
    {
        var nameSet = namesToRemove.ToHashSet();
        var pathsToRemove = new List<string>();
        
        foreach (var pathEntry in _pathPropertyAnimationMap)
        {
            var propertiesToRemove = pathEntry.Value.Keys.Where(nameSet.Contains).ToList();
            foreach (var property in propertiesToRemove)
            {
                pathEntry.Value.Remove(property);
            }
            
            if (pathEntry.Value.Count == 0)
            {
                pathsToRemove.Add(pathEntry.Key);
            }
        }
        
        foreach (var path in pathsToRemove)
        {
            _pathPropertyAnimationMap.Remove(path);
        }
        
        InvalidateBlendShapeCache();
    }

    public void RemoveBlendShapes(IEnumerable<string> namesToRemove)
    {
        var propertyNamesToRemove = namesToRemove.Select(x => $"{BlendShapePrefix}{x}");
        RemoveProperties(propertyNamesToRemove);
    }

    public void RemoveBlendShapes(string path, IEnumerable<string> namesToRemove)
    {
        if (_pathPropertyAnimationMap.TryGetValue(path, out var propertyMap))
        {
            var propertyNamesToRemove = namesToRemove.Select(x => $"{BlendShapePrefix}{x}").ToList();
            foreach (var propertyName in propertyNamesToRemove)
            {
                propertyMap.Remove(propertyName);
            }
            
            if (propertyMap.Count == 0)
            {
                _pathPropertyAnimationMap.Remove(path);
            }
            
            InvalidateBlendShapeCache();
        }
    }

    // Merge methods
    public void MergeAnimation(IEnumerable<GenericAnimation> others)
    {
        foreach (var animation in others)
        {
            AddInternal(animation); // 重複は自動的に上書きされる
        }
        InvalidateBlendShapeCache();
    }
    
    public void AllToSingleFrame()
    {
        var newMap = new Dictionary<string, Dictionary<string, GenericAnimation>>();
        
        foreach (var pathEntry in _pathPropertyAnimationMap)
        {
            var newPropertyMap = new Dictionary<string, GenericAnimation>();
            foreach (var propertyEntry in pathEntry.Value)
            {
                newPropertyMap[propertyEntry.Key] = propertyEntry.Value.ToSingleFrame();
            }
            newMap[pathEntry.Key] = newPropertyMap;
        }
        
        _pathPropertyAnimationMap = newMap;
        InvalidateBlendShapeCache();
    }
}