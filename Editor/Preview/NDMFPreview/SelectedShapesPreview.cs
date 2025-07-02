using nadena.dev.ndmf.preview;
using aoyon.facetune.Settings;

namespace aoyon.facetune.preview;

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

        var defaultSet = context.Observe(_targetObject, o => o is GameObject targetGameObject && proxy != null ? GetDefaultBlendShapes(root, targetGameObject, proxy, observeContext) : null, (a, b) =>
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.Equals(b);
        });
        if (defaultSet == null) return;

        // 何らかのGameObjcetを選択してる

        var dataComponents = context.Observe(_targetObject, o => o is GameObject targetGameObject ? GetComponents<FacialDataComponent>(targetGameObject, observeContext) : null, (a, b) => 
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.SequenceEqual(b);
        });
        if (dataComponents != null && dataComponents.Count > 0)
        {
            GetBlendShapes(dataComponents, defaultSet, observeContext, result);
            return;
        }

        var conditionComponents = context.Observe(_targetObject, o => o is GameObject targetGameObject ? GetComponents<ConditionComponent>(targetGameObject, observeContext) : null, (a, b) =>
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.SequenceEqual(b);
        });
        if (conditionComponents != null && conditionComponents.Count > 0)
        {
            var conditionComponent = conditionComponents.First();
            using var _ = ListPool<ConditionComponent>.Get(out var childrenConditionComponents);
            conditionComponent.gameObject.GetComponentsInChildren<ConditionComponent>(true, childrenConditionComponents);
            if (childrenConditionComponents.All(x => x.gameObject == conditionComponent.gameObject))
            {
                using var _expressionComponents = GetComponentsInChildren<FacialDataComponent>(conditionComponent.gameObject, true, observeContext);
                GetBlendShapes(_expressionComponents.Value, defaultSet, observeContext, result);
                return;
            }
        }

        // result.AddRange(defaultSet);
    }

    private static BlendShapeSet GetDefaultBlendShapes(GameObject root, GameObject targetGameObject, SkinnedMeshRenderer renderer, IObserveContext observeContext)
    {
        var result = new BlendShapeSet(); // Todo
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

        return result;
    }

    private static List<T> GetComponents<T>(GameObject targetGameObject, IObserveContext observeContext) where T : Component
    {
        var result = new List<T>(); // Todo
        observeContext.GetComponents<T>(targetGameObject, result);
        return result;
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

    private static void GetBlendShapes(IEnumerable<FacialDataComponent> dataComponents, BlendShapeSet defaultSet, IObserveContext observeContext, BlendShapeSet result)
    {
        result.AddRange(defaultSet);
        foreach (var dataComponent in dataComponents)
        {
            dataComponent.GetBlendShapes(result, defaultSet, observeContext);
        }
    }
}
