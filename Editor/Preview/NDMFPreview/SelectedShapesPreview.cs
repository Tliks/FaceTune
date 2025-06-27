using nadena.dev.ndmf.preview;
using com.aoyon.facetune.Settings;
using System.Runtime.Remoting.Contexts;

namespace com.aoyon.facetune.preview;

internal class SelectedShapesPreview : AbstractFaceTunePreview
{
    // 一時的に無効化出来るようにするために、必ずしもProjectSettings.EnableSelectedExpressionPreviewとは一致しない
    private static readonly PublishedValue<int> _disabledDepth = new(0); // 0で有効 無効化したい時は足す
    private static readonly PublishedValue<Object?> _targetObject = new(null);
    public override bool IsEnabled(ComputeContext context) => context.Observe(_disabledDepth, d => d == 0, (a, b) => a == b);
    public static bool Enabled => _disabledDepth.Value == 0;
    public static void Disable() => _disabledDepth.Value++;
    public static void MayEnable() => _disabledDepth.Value--;

    [InitializeOnLoadMethod]
    static void Init()
    {
        _disabledDepth.Value = ProjectSettings.EnableSelectedExpressionPreview ? 0 : 1;
        _targetObject.Value = Selection.objects.FirstOrNull();
        Selection.selectionChanged += OnSelectionChanged;
        ProjectSettings.EnableSelectedExpressionPreviewChanged += (value) => { if (value) MayEnable(); else Disable(); };
    }

    private static void OnSelectionChanged()
    {
        if (!Enabled) return;

        var selections = Selection.objects;

        Object? target = null;
        if (selections.Count() == 1)
        {
            target = selections.First();
        }

        _targetObject.Value = target;
    }

    protected override void QueryBlendShapes(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, GameObject root, ComputeContext context, BlendShapeSet result)
    {
        var observeContext = new NDMFPreviewObserveContext(context);
        if (!IsEnabled(context)) return;
        
        var clip = context.Observe(_targetObject, o => o as AnimationClip, (a, b) => a == b);
        if (clip != null)
        {
            GetBlendShapes(clip, result);
            return;
        }

        // _targetObjectをそのまま監視すると、無関係なオブジェクト同士に対する選択の切り替え時に更新がかかるようになる。
        // そのため、extractを用いるが、propertymonitorの負荷を考えるとどうだろう
        // Todo: extractにcontext.GetComponentsがあるのどうにかしたい

        using var _defaultSet = context.Observe(_targetObject, o => o is GameObject targetGameObject ? GetDefaultBlendShapes(root, targetGameObject, original, observeContext) : null, (a, b) =>
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.Value.Equals(b.Value);
        });
        if (_defaultSet == null) return;

        // 何らかのGameObjcetを選択してる

        using var dataComponents = context.Observe(_targetObject, o => o is GameObject targetGameObject ? GetComponents<ExpressionDataComponent>(targetGameObject, observeContext) : null, (a, b) => 
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.Value.SequenceEqual(b.Value);
        });
        if (dataComponents != null && dataComponents.Value.Count > 0)
        {
            GetBlendShapes(dataComponents.Value, _defaultSet.Value, observeContext, result);
            return;
        }

        using var _conditionComponents = context.Observe(_targetObject, o => o is GameObject targetGameObject ? GetComponents<ConditionComponent>(targetGameObject, observeContext) : null, (a, b) =>
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.Value.SequenceEqual(b.Value);
        });
        if (_conditionComponents != null && _conditionComponents.Value.Count > 0)
        {
            var conditionComponent = _conditionComponents.Value.First();
            using var _ = ListPool<ConditionComponent>.Get(out var childrenConditionComponents);
            conditionComponent.gameObject.GetComponentsInChildren<ConditionComponent>(true, childrenConditionComponents);
            if (childrenConditionComponents.All(x => x.gameObject == conditionComponent.gameObject))
            {
                using var _expressionComponents = GetComponentsInChildren<ExpressionDataComponent>(conditionComponent.gameObject, true, observeContext);
                GetBlendShapes(_expressionComponents.Value, _defaultSet.Value, observeContext, result);
                return;
            }
        }

        result.AddRange(_defaultSet.Value);
    }

    private static PooledObject<BlendShapeSet> GetDefaultBlendShapes(GameObject root, GameObject targetGameObject, SkinnedMeshRenderer renderer, IObserveContext observeContext)
    {
        var _result = BlendShapeSetPool.Get(out var result);
        renderer.GetBlendShapesAndSetZeroWeight(result);

        using var _ = ListPool<FacialStyleComponent>.Get(out var facialStyleComponents);
        targetGameObject.GetComponentsInParent<FacialStyleComponent>(true, facialStyleComponents);
        // GetComponentsInParentを監視できないのでその代わり
        using var _2 = ListPool<FacialStyleComponent>.Get(out var tmp);
        observeContext.GetComponentsInChildren<FacialStyleComponent>(root, true, tmp);


        if (facialStyleComponents.Count != 0)
        {
            var nearset = facialStyleComponents[0];
            nearset.GetBlendShapes(result, observeContext);
        }

        return _result;
    }

    private static PooledObject<List<T>> GetComponents<T>(GameObject targetGameObject, IObserveContext observeContext) where T : Component
    {
        var _result = ListPool<T>.Get(out var result);
        observeContext.GetComponents<T>(targetGameObject, result);
        return _result;
    }

    private static PooledObject<List<T>> GetComponentsInChildren<T>(GameObject targetGameObject, bool includeInactive, IObserveContext observeContext) where T : Component
    {
        var _result = ListPool<T>.Get(out var result);
        observeContext.GetComponentsInChildren<T>(targetGameObject, includeInactive, result);
        return _result;
    }

    private static void GetBlendShapes(AnimationClip clip, BlendShapeSet result)
    {
        clip.GetFirstFrameBlendShapes(result);
    }

    private static void GetBlendShapes(IEnumerable<ExpressionDataComponent> dataComponents, BlendShapeSet defaultSet, IObserveContext observeContext, BlendShapeSet result)
    {
        result.AddRange(defaultSet);
        foreach (var dataComponent in dataComponents)
        {
            dataComponent.GetBlendShapes(result, observeContext);
        }
    }
}
