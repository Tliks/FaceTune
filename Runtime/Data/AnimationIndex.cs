namespace com.aoyon.facetune;

// GenericAnimationのコレクションに対する高速なアクセス・簡易な編集を行うためのラッパーオブジェクト
// Todo: 重複のハンドリング
internal class AnimationIndex
{
    private List<GenericAnimation> _animations;
    public IReadOnlyList<GenericAnimation> Animations => _animations.AsReadOnly();

    // cache
    private Dictionary<string, Dictionary<string, List<GenericAnimation>>>? _pathNameAnimationMap;
    private Dictionary<string, Dictionary<string, List<BlendShapeAnimation>>>? _pathNameBlendShapeAnimationMap;
    private Dictionary<string, BlendShapeSet>? _pathFirstFrameBlendShapeSets; 
    private BlendShapeSet? _allFirstFrameBlendShapeSet;
    private bool _cacheValid = false;

    private static readonly string BlendShapePrefix = FaceTuneConsts.AnimatedBlendShapePrefix;

    public AnimationIndex(IReadOnlyList<GenericAnimation> animations)
    {
        _animations = new List<GenericAnimation>(animations);
    }

    private Dictionary<string, Dictionary<string, List<GenericAnimation>>> GetPathNameToAnimationsMap()
    {
        if (!_cacheValid || _pathNameAnimationMap == null)
        {
            var mapping = new Dictionary<string, Dictionary<string, List<GenericAnimation>>>();
            foreach (var animation in _animations)
            {
                mapping.GetOrAddNew(animation.CurveBinding.Path).GetOrAddNew(animation.CurveBinding.PropertyName).Add(animation);
            }
            _pathNameAnimationMap = mapping;
            _cacheValid = true;
        }
        return _pathNameAnimationMap;
    }

    private Dictionary<string, Dictionary<string, List<BlendShapeAnimation>>> GetPathNameToBlendShapeAnimationsMap()
    {
        if (!_cacheValid || _pathNameBlendShapeAnimationMap == null)
        {
            var mapping = new Dictionary<string, Dictionary<string, List<BlendShapeAnimation>>>();
            foreach (var animation in _animations)
            {
                if (animation.TryToBlendShapeAnimation(out var blendShapeAnimation))
                {
                    mapping.GetOrAddNew(animation.CurveBinding.Path).GetOrAddNew(animation.CurveBinding.PropertyName).Add(blendShapeAnimation);
                }
            }
            _pathNameBlendShapeAnimationMap = mapping;
            _cacheValid = true;
        }
        return _pathNameBlendShapeAnimationMap;
    }

    private void InvalidateCache()
    {
        _cacheValid = false;
        _pathNameAnimationMap = null;
        _pathFirstFrameBlendShapeSets = null;
    }


    // get
    public bool TryGetAnimations(string path, string name, [NotNullWhen(true)] out List<GenericAnimation>? animations)
    {
        animations = null;
        var pathNameToAnimationsMap = GetPathNameToAnimationsMap();
        return pathNameToAnimationsMap.TryGetValue(path, out var nameToAnimationsMap) && nameToAnimationsMap.TryGetValue(name, out animations);
    }

    public bool TryGetBlendShapeAnimations(string path, string name, [NotNullWhen(true)] out List<BlendShapeAnimation>? animations)
    {
        animations = null;
        var pathNameToBlendShapeAnimationsMap = GetPathNameToBlendShapeAnimationsMap();
        return pathNameToBlendShapeAnimationsMap.TryGetValue(path, out var nameToAnimationsMap) && nameToAnimationsMap.TryGetValue(name, out animations);
    }

    public IEnumerable<string> GetBlendShapeNames(string path)
    {
        var pathNameToBlendShapeAnimationsMap = GetPathNameToBlendShapeAnimationsMap();
        return pathNameToBlendShapeAnimationsMap.TryGetValue(path, out var nameToAnimationsMap) ? nameToAnimationsMap.Keys : Array.Empty<string>();
    }
    
    public IEnumerable<string> GetAllBlendShapeNames()
    {
        var pathNameToBlendShapeAnimationsMap = GetPathNameToBlendShapeAnimationsMap();
        return pathNameToBlendShapeAnimationsMap.SelectMany(x => x.Value.SelectMany(y => y.Value.Select(z => z.Name))).Distinct();
    }

    public bool TryGetFirstFrameBlendShapeSet(string path, [NotNullWhen(true)] out BlendShapeSet? blendShapeSet)
    {
        if (!_cacheValid || _pathFirstFrameBlendShapeSets == null)
        {
            var pathNameToBlendShapeAnimationsMap = GetPathNameToBlendShapeAnimationsMap();
            if (!pathNameToBlendShapeAnimationsMap.TryGetValue(path, out var nameToAnimationsMap))
            {
                blendShapeSet = null;
                return false;
            }
            var blendShapes = nameToAnimationsMap.Values.SelectMany(x => x.Select(y => y.ToFirstFrameBlendShape())).ToList(); // Todo : 重複のハンドリング
            _pathFirstFrameBlendShapeSets = new Dictionary<string, BlendShapeSet> { { path, new BlendShapeSet(blendShapes) } };
            _cacheValid = true;
        }
        blendShapeSet = _pathFirstFrameBlendShapeSets[path!].Clone();
        return true;
    }

    public BlendShapeSet GetAllFirstFrameBlendShapeSet()
    {
        if (_allFirstFrameBlendShapeSet == null)
        {
            var pathNameToBlendShapeAnimationsMap = GetPathNameToBlendShapeAnimationsMap();
            var blendShapes = pathNameToBlendShapeAnimationsMap.SelectMany(x => x.Value.SelectMany(y => y.Value.Select(z => z.ToFirstFrameBlendShape()))).ToList(); // Todo : 重複のハンドリング
            _allFirstFrameBlendShapeSet = new BlendShapeSet(blendShapes);
        }
        return _allFirstFrameBlendShapeSet.Clone();
    }


    // add
    public void AddAnimation(GenericAnimation animation)
    {
        _animations.Add(animation);
        InvalidateCache();
    }

    public void AddSingleFrameBlendShapeAnimation(string path, string name, float weight)
    {
        var animation = BlendShapeAnimation.SingleFrame(name, weight).ToGeneric(path);
        _animations.Add(animation);
        InvalidateCache();
    }

    // replace
    public void ReplacePropertyNames(Dictionary<string, string> propertyMapping)
    {
        var newAnimations = ReplacePropertyNames(_animations, propertyMapping);
        _animations = newAnimations;
        InvalidateCache();
    }

    public void ReplaceBlendShapeNames(string path, Dictionary<string, string> shapeMapping)
    {
        var targetAnimations = _animations.Where(b => b.CurveBinding.Path == path);
        var newAnimations = ReplacePropertyNames(targetAnimations, shapeMapping.ToDictionary(x => $"{BlendShapePrefix}{x.Key}", x => $"{BlendShapePrefix}{x.Value}"));
        _animations = newAnimations;
        InvalidateCache();
    }

    private static List<GenericAnimation> ReplacePropertyNames(IEnumerable<GenericAnimation> originalAnimations, Dictionary<string, string> propertyMapping)
    {
        var nameCurveMap = new Dictionary<string, List<GenericAnimation>>();
        foreach (var animation in originalAnimations)
        {
            nameCurveMap.GetOrAddNew(animation.CurveBinding.PropertyName).Add(animation);
        }
        foreach (var (oldName, newName) in propertyMapping)
        {
            if (nameCurveMap.TryGetValue(oldName, out var animations))
            {
                nameCurveMap.Remove(oldName);
                nameCurveMap[newName] = animations.Select(a => a with { CurveBinding = a.CurveBinding with { PropertyName = newName } }).ToList();
            }
        }
        return nameCurveMap.SelectMany(x => x.Value).ToList();
    }

    // remove
    public void RemoveProperties(IEnumerable<string> namesToRemove)
    {
        _animations = RemoveProperties(_animations, namesToRemove);
        InvalidateCache();
    }

    public void RemoveBlendShapes(IEnumerable<string> namesToRemove)
    {
        _animations = RemoveProperties(_animations, namesToRemove.Select(x => $"{BlendShapePrefix}{x}"));
        InvalidateCache();
    }

    public void RemoveBlendShapes(string path, IEnumerable<string> namesToRemove)
    {
        var targetBindings = _animations.Where(b => b.CurveBinding.Path == path);
        var newBindings = RemoveProperties(targetBindings, namesToRemove.Select(x => $"{BlendShapePrefix}{x}"));
        _animations = newBindings;
        InvalidateCache();
    }

    private static List<GenericAnimation> RemoveProperties(IEnumerable<GenericAnimation> originalAnimations, IEnumerable<string> namesToRemove)
    {
        var nameCurveMap = new Dictionary<string, List<GenericAnimation>>();
        foreach (var animation in originalAnimations)
        {
            nameCurveMap.GetOrAddNew(animation.CurveBinding.PropertyName).Add(animation);
        }
        foreach (var name in namesToRemove)
        {
            nameCurveMap.Remove(name);
        }
        return nameCurveMap.SelectMany(x => x.Value).ToList();
    }   

    // merge
    public void MergeAnimation(IEnumerable<GenericAnimation> others)
    {
        var pathNameAnimationMap = GetPathNameToAnimationsMap();

        foreach (var other in others)
        {
            var animations = pathNameAnimationMap.GetOrAddNew(other.CurveBinding.Path).GetOrAddNew(other.CurveBinding.PropertyName);
            animations.Clear();
            animations.Add(other);
        }

        _animations = pathNameAnimationMap.SelectMany(x => x.Value.SelectMany(y => y.Value)).ToList();

        InvalidateCache();
    }
    
    public void AllToSingleFrame()
    {
        _animations = _animations.Select(a => a.ToSingleFrame()).ToList();
        InvalidateCache();
    }
}