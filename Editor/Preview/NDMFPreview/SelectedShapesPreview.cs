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

    private static readonly MultiFramePreview _multiFramePreview = new(EditCurrentNodeDirectly);

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
        _multiFramePreview.Stop();
    }

    // 無関係なオブジェクト同士の選択の切り替え時で更新がかかるらないように、_targetObjectのextractで監視する
    protected override void QueryBlendShapes(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, GameObject root, string bodyPath, ComputeContext context, BlendShapeWeightSet result, ref float defaultValue)
    {
        var animations = new List<BlendShapeWeightAnimation>();
        if (TryCollectBlendShapeAnimations(context, root, bodyPath, animations, out var isLooping))
        {
            // 0で初期化し他の影響を打ち消す
            defaultValue = 0;
            // プレビュー対象のブレンドシェイプに関しては上書き
            result.AddRange(animations.ToFirstFrameBlendShapes());
            
            if (animations.Any(a => a.IsMultiFrame)) {
                _multiFramePreview.Start(animations, isLooping);
            }
            else{
                _multiFramePreview.Stop();
            }
            
            return;
        }
        else
        {
            // 空のプレビュー
            _multiFramePreview.Stop();
            return;
        }
    }

    private static bool TryCollectBlendShapeAnimations(ComputeContext context, GameObject root, string bodyPath, List<BlendShapeWeightAnimation> resultToAdd, out bool isLooping)
    {
        isLooping = false;

        // Clip 経路
        var clip = context.Observe(_targetObject, o => o as AnimationClip, (a, b) => a == b);
        if (clip != null)
        {
            clip.GetAllBlendShapeAnimations(resultToAdd, bodyPath);
            isLooping = context.Observe(clip, c => c.isLooping, (a, b) => a == b); // NDMFのChangeStreamってアセットの変更も拾えるんだ…
            return true;
        }

        // GameObject 経路
        using var _dataComponents = ListPool<ExpressionDataComponent>.Get(out var dataComponents);
        if (TryGetExpressionData(context, root, dataComponents, out var expressionComponent))
        { 
            var observeContext = new NDMFPreviewObserveContext(context);

            using var _facial = ListPool<BlendShapeWeightAnimation>.Get(out var facial);
            FacialStyleContext.TryGetFacialStyleAnimationsAndObserve(dataComponents[0].gameObject, facial, root, observeContext);

            resultToAdd.AddRange(facial);

            foreach (var dataComponent in dataComponents)
            {
                dataComponent.GetBlendShapeAnimations(resultToAdd, facial, bodyPath, observeContext);
            }

            if (expressionComponent != null)
            {
                isLooping = context.Observe(expressionComponent, e => e.ExpressionSettings.LoopTime, (a, b) => a == b);
            }

            return true;
        }

        return false;
    }

    // data > expression > condition の順で対象を決定し早期リターン
    private static bool TryGetExpressionData(ComputeContext context, GameObject root, List<ExpressionDataComponent> dataComponents, out ExpressionComponent? expressionComponent)
    {
        expressionComponent = null;

        var dataComponent = context.Observe(_targetObject, o => o is GameObject gameObject ? context.GetComponent<ExpressionDataComponent>(gameObject) : null, (a, b) => a == b);
        if (dataComponent != null)
        {
            var targetGameObject = dataComponent.gameObject;
            if (!TryGetDataComponents(context, targetGameObject, dataComponents)) return false;
            context.TryGetComponentInParent(targetGameObject, root, true, out expressionComponent);
            return true;
        }

        var _expressionComponent = context.Observe(_targetObject, o => o is GameObject gameObject ? context.GetComponent<ExpressionComponent>(gameObject) : null, (a, b) => a == b);
        if (_expressionComponent != null)
        {
            var targetGameObject = _expressionComponent.gameObject;
            if (!TryGetDataComponents(context, targetGameObject, dataComponents)) return false;
            expressionComponent = _expressionComponent;
            return true;
        }

        var conditionComponent = context.Observe(_targetObject, o => o is GameObject gameObject ? context.GetComponent<ConditionComponent>(gameObject) : null, (a, b) => a == b);
        if (conditionComponent != null)
        {
            using var _ = ListPool<ConditionComponent>.Get(out var childrenConditionComponents);
            conditionComponent.gameObject.GetComponentsInChildren(true, childrenConditionComponents);
            if (childrenConditionComponents.All(x => x.gameObject == conditionComponent.gameObject))
            {
                var targetGameObject = conditionComponent.gameObject;
                if (!TryGetDataComponents(context, targetGameObject, dataComponents)) return false;
                context.TryGetComponentInParent(targetGameObject, root, true, out expressionComponent);
                return true;
            }
        }

        return false;

        static bool TryGetDataComponents(ComputeContext context, GameObject gameObject, List<ExpressionDataComponent> dataComponents)
        {
            context.GetComponentsInChildren(gameObject, true, dataComponents);
            if (dataComponents.Count == 0) return false;
            return true;
        }
    }
}