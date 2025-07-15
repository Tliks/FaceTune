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
    public static void MayEnable()
    {
        if (_disabledDepth.Value > 0)
        {
            _disabledDepth.Value--;
        }
    }

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
            clip.GetFirstFrameBlendShapes(result);
            return;
        }

        // 無関係なオブジェクト同士に対する選択の切り替え時に更新がかかるらないように、_targetObjectのextractで監視する
        // ただしpropertymonitorの負荷が高い
        // Todo: extractにcontext.GetComponentsとList等のアロケーションがあるのをどうにかしたい
        var isGameObject = context.Observe(_targetObject, o => o is GameObject, (a, b) => a == b);
        if (!isGameObject) return;

        using var _ = BlendShapeSetPool.Get(out var zeroWeightBlendShapes);
        proxy.GetBlendShapesAndSetZeroWeight(zeroWeightBlendShapes);
        
        var facialStyleSet = context.Observe(_targetObject, o => GetFacialStyle((GameObject)o!, root, observeContext), (a, b) => a.SequenceEqual(b));

        // 処理が軽い data > expression > condition の順に監視し、早期リターン

        var dataComponents = context.Observe(_targetObject, o => GetComponents<AbstractDataComponent>((GameObject)o!, observeContext), (a, b) => a.SequenceEqual(b));
        if (dataComponents.Count > 0)
        {
            result.AddRange(zeroWeightBlendShapes);
            result.AddRange(facialStyleSet);
            ProcessDataComponents(dataComponents, facialStyleSet, observeContext, result);
            return;
        }

        var expressionComponent = context.Observe(_targetObject, o => context.GetComponent<ExpressionComponent>((GameObject)o!), (a, b) => ReferenceEquals(a, b));
        if (expressionComponent != null)
        {
            result.AddRange(zeroWeightBlendShapes);
            result.AddRange(facialStyleSet);
            ProcessExpressionComponent(expressionComponent, facialStyleSet, observeContext, result);
            return;
        }

        var conditionComponents = context.Observe(_targetObject, o => GetComponents<ConditionComponent>((GameObject)o!, observeContext), (a, b) => a.SequenceEqual(b));
        if (conditionComponents.Count > 0)
        {
            result.AddRange(zeroWeightBlendShapes);
            result.AddRange(facialStyleSet);
            ProcessConditionComponents(conditionComponents, facialStyleSet, observeContext, result);
            return;
        }
    }

    private static BlendShapeSet GetFacialStyle(GameObject targetGameObject, GameObject root, IObserveContext observeContext)
    {
        var result = new BlendShapeSet(); // Todo
        FacialStyleContext.TryAddFacialStyleShapes(targetGameObject, result, root, observeContext);
        return result;
    }

    private static List<T> GetComponents<T>(GameObject targetGameObject, IObserveContext observeContext) where T : Component
    {
        var result = new List<T>(); // Todo
        observeContext.GetComponents<T>(targetGameObject, result);
        return result;
    }

    private static void ProcessDataComponents(IEnumerable<AbstractDataComponent> dataComponents, IReadOnlyBlendShapeSet facialStyleSet, IObserveContext observeContext, BlendShapeSet result)
    {
        foreach (var dataComponent in dataComponents)
        {
            dataComponent.GetBlendShapes(result, facialStyleSet, observeContext);
        }
    }

    private static void ProcessExpressionComponent(ExpressionComponent expressionComponent, IReadOnlyBlendShapeSet facialStyleSet, IObserveContext observeContext, BlendShapeSet result)
    {
        using var _ = ListPool<AbstractDataComponent>.Get(out var childDataComponents);
        observeContext.GetComponentsInChildren<AbstractDataComponent>(expressionComponent.gameObject, true, childDataComponents);
        foreach (var dataComponent in childDataComponents)
        {
            dataComponent.GetBlendShapes(result, facialStyleSet, observeContext);
        }
    }

    private static void ProcessConditionComponents(List<ConditionComponent> conditionComponents, IReadOnlyBlendShapeSet facialStyleSet, IObserveContext observeContext, BlendShapeSet result)
    {
        var conditionComponent = conditionComponents.First();
        using var _ = ListPool<ConditionComponent>.Get(out var childrenConditionComponents);
        conditionComponent.gameObject.GetComponentsInChildren<ConditionComponent>(true, childrenConditionComponents);
        if (childrenConditionComponents.All(x => x.gameObject == conditionComponent.gameObject))
        {
            using var _2 = ListPool<AbstractDataComponent>.Get(out var childDataComponents);
            conditionComponent.gameObject.GetComponentsInChildren<AbstractDataComponent>(true, childDataComponents);
            foreach (var dataComponent in childDataComponents)
            {
                dataComponent.GetBlendShapes(result, facialStyleSet, observeContext);
            }
        }
    }
}
