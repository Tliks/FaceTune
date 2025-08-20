using nadena.dev.ndmf.preview;
using Aoyon.FaceTune.Settings;

namespace Aoyon.FaceTune.Preview;

internal class SelectedShapesPreview : AbstractFaceTunePreview<SelectedShapesPreview>
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

        if (selections.Length == 1)
        {
            _targetObject.Value = selections[0];
        }
        else
        {
            _targetObject.Value = null;
        }
    }

    // 無関係なオブジェクト同士の選択の切り替え時で更新がかかるらないように、_targetObjectのextractで監視する
    protected override void QueryBlendShapes(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, GameObject root, ComputeContext context, BlendShapeSet result)
    {
        var clip = context.Observe(_targetObject, o => o as AnimationClip, (a, b) => a == b);
        if (clip != null)
        {
            using var _ = ListPool<BlendShapeWeightAnimation>.Get(out var animations);
            clip.GetAllFirstFrameBlendShapes(result);
            return;
        }

        // 処理が軽い data >= expression > condition の順に監視し、早期リターン
        
        // extractが呼ばれる順序の保証はないので、extract内におけるGameObjectかどうかの確認は必要
        var dataComponent = context.Observe(_targetObject, o => o is GameObject gameObject ? context.GetComponent<ExpressionDataComponent>(gameObject) : null, (a, b) => a == b);
        if (dataComponent != null)
        {
            ProcessChildrenBlendShapes(dataComponent.gameObject, root, proxy, context, result);
            return;
        }

        var expressionComponent = context.Observe(_targetObject, o => o is GameObject gameObject ? context.GetComponent<ExpressionComponent>(gameObject) : null, (a, b) => a == b);
        if (expressionComponent != null)
        {
            ProcessChildrenBlendShapes(expressionComponent.gameObject, root, proxy, context, result);
            return;
        }

        var conditionComponent = context.Observe(_targetObject, o => o is GameObject gameObject ? context.GetComponent<ConditionComponent>(gameObject) : null, (a, b) => a == b);
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
    }

    // プレビューの対象となり得るGameObjectであることが確定している場合
    private static void ProcessChildrenBlendShapes(GameObject targetGameObject, GameObject root, SkinnedMeshRenderer proxy, ComputeContext context, BlendShapeSet result)
    {
        var isEditorOnly = context.EditorOnlyInHierarchy(targetGameObject);
        if (isEditorOnly) return;

        var observeContext = new NDMFPreviewObserveContext(context);

        using var _ = BlendShapeSetPool.Get(out var zeroWeightBlendShapes);
        proxy.GetBlendShapesAndSetWeightToZero(zeroWeightBlendShapes);
        
        using var _2 = ListPool<BlendShapeWeightAnimation>.Get(out var facialStyleAnimations);
        FacialStyleContext.TryGetFacialStyleAnimationsAndObserve(targetGameObject, facialStyleAnimations, root, observeContext);

        result.AddRange(zeroWeightBlendShapes);
        result.AddRange(facialStyleAnimations.ToFirstFrameBlendShapes());

        using var _3 = ListPool<ExpressionDataComponent>.Get(out var childDataComponents);
        context.GetComponentsInChildren<ExpressionDataComponent>(targetGameObject, true, childDataComponents);
        foreach (var dataComponent in childDataComponents)
        {
            dataComponent.GetBlendShapes(result, facialStyleAnimations, observeContext);
        }
    }
}
