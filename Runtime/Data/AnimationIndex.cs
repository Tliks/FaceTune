namespace com.aoyon.facetune;

// GenericAnimationのコレクションに対する高速なアクセス・簡易な編集を行うためのラッパーオブジェクト
internal class AnimationIndex
{
    private List<GenericAnimation> _animations;
    public IReadOnlyList<GenericAnimation> Animations => _animations.AsReadOnly();

    // cache
    private Dictionary<string, Dictionary<string, GenericAnimation>>? _pathNameAnimationMap;
    private Dictionary<string, Dictionary<string, AnimationCurve>>? _pathNameCurves;
    private Dictionary<string, Dictionary<string, AnimationCurve>>? _pathNameBlendShapeCurves;
    private Dictionary<string, BlendShapeSet>? _pathFirstFrameBlendShapeSets; 
    private BlendShapeSet? _allFirstFrameBlendShapeSet;
    private bool _cacheValid = false;

    private static readonly string BlendShapePrefix = "blendShape.";

    public AnimationIndex(IReadOnlyList<GenericAnimation> animations)
    {
        _animations = new List<GenericAnimation>(animations);
    }

    private Dictionary<string, Dictionary<string, GenericAnimation>> GetPathNameAnimationMap()
    {
        if (!_cacheValid || _pathNameAnimationMap == null)
        {
            var mapping = new Dictionary<string, Dictionary<string, GenericAnimation>>();
            foreach (var animation in _animations)
            {
                mapping[animation.CurveBinding.Path][animation.CurveBinding.PropertyName] = animation;
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
    public bool TryGetCurves(string path, [NotNullWhen(true)] out Dictionary<string, AnimationCurve>? curves)
    {
        return GetPathNameCurves().TryGetValue(path, out curves);
    }

    public bool TryGetBlendShapeCurves(string path, [NotNullWhen(true)] out Dictionary<string, AnimationCurve>? curves)
    {
        return GetPathNameBlendShapeCurves().TryGetValue(path, out curves);
    }

    public bool TryGetCurve(string path, string name, [NotNullWhen(true)] out AnimationCurve? curve)
    {
        curve = null;
        if (!GetPathNameCurves().TryGetValue(path, out var curves)) return false;
        return curves.TryGetValue(name, out curve);
    }

    public bool TryGetBlendShapeCurve(string path, string name, [NotNullWhen(true)] out AnimationCurve? curve)
    {
        curve = null;
        if (!GetPathNameBlendShapeCurves().TryGetValue(path, out var curves)) return false;
        return curves.TryGetValue(name, out curve);
    }

    public IEnumerable<string> GetBlendShapeNames(string path)
    {
        if (!GetPathNameBlendShapeCurves().TryGetValue(path, out var curves)) return Enumerable.Empty<string>();
        return curves.Keys;
    }

    public IEnumerable<string> GetAllBlendShapeNames()
    {
        return GetPathNameBlendShapeCurves().SelectMany(x => x.Value.Keys);
    }

    public bool TryGetFirstFrameBlendShapeSet(string path, [NotNullWhen(true)] out BlendShapeSet? blendShapeSet)
    {
        if (!_cacheValid || _pathFirstFrameBlendShapeSets == null)
        {
            if (!GetPathNameBlendShapeCurves().TryGetValue(path, out var curves))
            {
                blendShapeSet = null;
                return false;
            }
            var blendShapes = curves.Select(x => new BlendShape(x.Key, x.Value.Evaluate(0))).ToList();
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
            var blendShapes = GetPathNameBlendShapeCurves().SelectMany(x => x.Value.Select(y => new BlendShape(y.Key, y.Value.Evaluate(0)))).ToList();
            _allFirstFrameBlendShapeSet = new BlendShapeSet(blendShapes);
        }
        return _allFirstFrameBlendShapeSet.Clone();
    }

    private Dictionary<string, Dictionary<string, AnimationCurve>> GetPathNameCurves()
    {
        if (!_cacheValid || _pathNameCurves == null)
        {
            var pathNameCurveMap = new Dictionary<string, Dictionary<string, AnimationCurve>>();
            foreach (var animation in _animations)
            {
                var curves = pathNameCurveMap.GetOrAddNew(animation.CurveBinding.Path);
                curves[animation.CurveBinding.PropertyName] = animation.GetCurve();
            }
            _pathNameCurves = pathNameCurveMap;
            _cacheValid = true;
        }
        return _pathNameCurves;
    }

    private Dictionary<string, Dictionary<string, AnimationCurve>> GetPathNameBlendShapeCurves() 
    {
        if (!_cacheValid || _pathNameBlendShapeCurves == null)
        {
            var pathNameCurveMap = new Dictionary<string, Dictionary<string, AnimationCurve>>();
            foreach (var animation in _animations)
            {
                var binding = animation.CurveBinding;
                if (binding.Type == typeof(SkinnedMeshRenderer) && binding.PropertyName.StartsWith(BlendShapePrefix))
                {
                    var name = binding.PropertyName.Replace(BlendShapePrefix, string.Empty);

                    var curves = pathNameCurveMap.GetOrAddNew(binding.Path);
                    curves[name] = animation.GetCurve();
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
        var pathNameAnimationMap = GetPathNameAnimationMap();

        foreach (var other in others)
        {
            // 追加 or 同一パスかつ同一プロパティ名の場合に完全に上書き
            pathNameAnimationMap[other.CurveBinding.Path][other.CurveBinding.PropertyName] = other with {};
        }

        _animations = pathNameAnimationMap.SelectMany(x => x.Value.Select(y => y.Value)).ToList();

        InvalidateCache();
    }
}