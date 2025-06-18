namespace com.aoyon.facetune;

// GenericAnimationのコレクションに対する高速なアクセス・簡易な編集を行うためのラッパーオブジェクト
internal class AnimationIndex
{
    private List<GenericAnimation> _animations;
    public IReadOnlyList<GenericAnimation> Animations => _animations.AsReadOnly();

    // cache
    private Dictionary<string, Dictionary<string, List<GenericAnimation>>>? _pathNameAnimationMap;
    private Dictionary<string, Dictionary<string, List<AnimationCurve>>>? _pathNameCurves;
    private Dictionary<string, Dictionary<string, List<AnimationCurve>>>? _pathNameBlendShapeCurves;
    private Dictionary<string, BlendShapeSet>? _pathFirstFrameBlendShapeSets; 
    private BlendShapeSet? _allFirstFrameBlendShapeSet;
    private bool _cacheValid = false;

    private static readonly string BlendShapePrefix = "blendShape.";

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

    private void InvalidateCache()
    {
        _cacheValid = false;
        _pathNameAnimationMap = null;
        _pathNameBlendShapeCurves = null;
        _pathFirstFrameBlendShapeSets = null;
    }


    // get
    public bool TryGetNameToCurvesMap(string path, [NotNullWhen(true)] out Dictionary<string, List<AnimationCurve>>? nameToCurvesMap)
    {
        return GetPathNameToCurvesMap().TryGetValue(path, out nameToCurvesMap);
    }

    public bool TryGetBlendShapeNameToCurvesMap(string path, [NotNullWhen(true)] out Dictionary<string, List<AnimationCurve>>? nameToCurvesMap)
    {
        return GetPathNameBlendShapeCurves().TryGetValue(path, out nameToCurvesMap);
    }

    public Dictionary<string, List<AnimationCurve>> GetAllNameToCurvesMap() //ignore path
    {
        var nameToCurvesMap = new Dictionary<string, List<AnimationCurve>>();
        foreach (var nameToCurvesMap_ in GetPathNameToCurvesMap().Values)
        {
            foreach (var (name, curves) in nameToCurvesMap_)
            {
                nameToCurvesMap.GetOrAddNew(name).AddRange(curves);
            }
        }
        return nameToCurvesMap;
    }

    public bool TryGetCurves(string path, string name, [NotNullWhen(true)] out List<AnimationCurve>? curves)
    {
        curves = null;
        if (!TryGetNameToCurvesMap(path, out var nameToCurvesMap)) return false;
        return nameToCurvesMap.TryGetValue(name, out curves);
    }

    public bool TryGetBlendShapeCurves(string path, string name, [NotNullWhen(true)] out List<AnimationCurve>? curves)
    {
        curves = null;
        if (!TryGetBlendShapeNameToCurvesMap(path, out var nameToCurvesMap)) return false;
        return nameToCurvesMap.TryGetValue(name, out curves);
    }

    public IEnumerable<string> GetBlendShapeNames(string path)
    {
        if (!TryGetBlendShapeNameToCurvesMap(path, out var nameToCurvesMap)) return Enumerable.Empty<string>();
        return nameToCurvesMap.Keys;
    }

    public IEnumerable<string> GetAllBlendShapeNames()
    {
        var nameToCurvesMap = GetAllNameToCurvesMap();
        return nameToCurvesMap.Keys;
    }

    public bool TryGetFirstFrameBlendShapeSet(string path, [NotNullWhen(true)] out BlendShapeSet? blendShapeSet)
    {
        if (!_cacheValid || _pathFirstFrameBlendShapeSets == null)
        {
            if (!TryGetBlendShapeNameToCurvesMap(path, out var nameToCurvesMap))
            {
                blendShapeSet = null;
                return false;
            }
            var blendShapes = nameToCurvesMap.Select(x => new BlendShape(x.Key, x.Value.Last().Evaluate(0))).ToList(); // Todo : 重複のハンドリング
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
            var blendShapes = GetAllNameToCurvesMap().Select(x => new BlendShape(x.Key, x.Value.Last().Evaluate(0))).ToList(); // Todo : 重複のハンドリング
            _allFirstFrameBlendShapeSet = new BlendShapeSet(blendShapes);
        }
        return _allFirstFrameBlendShapeSet.Clone();
    }

    private Dictionary<string, Dictionary<string, List<AnimationCurve>>> GetPathNameToCurvesMap()
    {
        if (!_cacheValid || _pathNameCurves == null)
        {
            var pathNameCurveMap = new Dictionary<string, Dictionary<string, List<AnimationCurve>>>();
            foreach (var animation in _animations)
            {
                var binding = animation.CurveBinding;
                var path = binding.Path;
                var name = binding.PropertyName;
                pathNameCurveMap.GetOrAddNew(path).GetOrAddNew(name).Add(animation.Curve);
            }
            _pathNameCurves = pathNameCurveMap;
            _cacheValid = true;
        }
        return _pathNameCurves;
    }

    private Dictionary<string, Dictionary<string, List<AnimationCurve>>> GetPathNameBlendShapeCurves() 
    {
        if (!_cacheValid || _pathNameBlendShapeCurves == null)
        {
            var pathNameCurveMap = new Dictionary<string, Dictionary<string, List<AnimationCurve>>>();
            foreach (var animation in _animations)
            {
                var binding = animation.CurveBinding;
                if (binding.Type == typeof(SkinnedMeshRenderer) && binding.PropertyName.StartsWith(BlendShapePrefix))
                {
                    var path = binding.Path;
                    var name = binding.PropertyName.Replace(BlendShapePrefix, string.Empty);
                    pathNameCurveMap.GetOrAddNew(path).GetOrAddNew(name).Add(animation.Curve);
                }
            }
            _pathNameBlendShapeCurves = pathNameCurveMap;
            _cacheValid = true;
        }

        return _pathNameBlendShapeCurves;
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
}