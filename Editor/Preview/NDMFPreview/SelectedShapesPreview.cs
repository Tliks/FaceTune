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

    // 無関係なオブジェクト同士の選択の切り替え時で更新がかかるらないように、_targetObjectのextractで監視する
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

        var isGameObject = context.Observe(_targetObject, o => o is GameObject, (a, b) => a == b);
        if (!isGameObject) return; // 早期リターン

        // 処理が軽い data >= expression > condition の順に監視し、早期リターン
        
        // extractが呼ばれる順序の保証はないので、extract内におけるGameObjectかどうかの確認は必要
        var dataComponent = context.Observe(_targetObject, o => o is GameObject gameObject ? context.GetComponent<AbstractDataComponent>(gameObject) : null, (a, b) => EQ(a, b));
        if (dataComponent != null)
        {
            ProcessChildrenBlendShapes(dataComponent.gameObject, root, proxy, context, result);
            return;
        }

        var expressionComponent = context.Observe(_targetObject, o => o is GameObject gameObject ? context.GetComponent<ExpressionComponent>(gameObject) : null, (a, b) => EQ(a, b));
        if (expressionComponent != null)
        {
            ProcessChildrenBlendShapes(expressionComponent.gameObject, root, proxy, context, result);
            return;
        }

        var conditionComponent = context.Observe(_targetObject, o => o is GameObject gameObject ? context.GetComponent<ConditionComponent>(gameObject) : null, (a, b) => EQ(a, b));
        if (conditionComponent != null)
        {
            using var _ = ListPool<ConditionComponent>.Get(out var childrenConditionComponents);
            conditionComponent.gameObject.GetComponentsInChildren<ConditionComponent>(true, childrenConditionComponents);
            if (childrenConditionComponents.All(x => x.gameObject == conditionComponent.gameObject))
            {
                ProcessChildrenBlendShapes(conditionComponent.gameObject, root, proxy, context, result);
                return;
            }
        }

        return; // 空のプレビュー

        static bool EQ(Component? a, Component? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return ReferenceEquals(a, b);
        }
    }

    // プレビューの対象となり得るGameObjectであることが確定している場合
    private static void ProcessChildrenBlendShapes(GameObject targetGameObject, GameObject root, SkinnedMeshRenderer proxy, ComputeContext context, BlendShapeSet result)
    {
        var isEditorOnly = context.Observe(targetGameObject, o => o.CompareTag("EditorOnly"), (a, b) => a == b);
        if (isEditorOnly) return;

        using var _ = BlendShapeSetPool.Get(out var zeroWeightBlendShapes);
        proxy.GetBlendShapesAndSetZeroWeight(zeroWeightBlendShapes);
        
        using var _2 = BlendShapeSetPool.Get(out var facialStyleSet);
        FacialStyleContext.TryGetFacialStyleShapesAndObserve(targetGameObject, facialStyleSet, root, new NDMFPreviewObserveContext(context));

        result.AddRange(zeroWeightBlendShapes);
        result.AddRange(facialStyleSet);

        using var _3 = ListPool<AbstractDataComponent>.Get(out var childDataComponents);
        context.GetComponentsInChildren<AbstractDataComponent>(targetGameObject, true, childDataComponents);
        foreach (var dataComponent in childDataComponents)
        {
            dataComponent.GetBlendShapes(result, facialStyleSet, new NDMFPreviewObserveContext(context));
        }
    }
}
