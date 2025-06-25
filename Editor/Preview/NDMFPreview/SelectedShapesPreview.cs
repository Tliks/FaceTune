using nadena.dev.ndmf.preview;
using com.aoyon.facetune.Settings;

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

        using var dfc = SessionContextBuilder.BuildDefaultBlendShapeSetContext(root, original, observeContext);
        
        var clip = context.Observe(_targetObject, o => o as AnimationClip, (a, b) => a == b);
        if (clip != null)
        {
            GetBlendShapes(clip, result);
            return;
        }

        // _targetObjectをそのまま監視すると、無関係なオブジェクト同士に対する選択の切り替え時に更新がかかるようになる。
        // そのため、extractを用いるが、propertymonitorの負荷を考えるとどうだろう
        var defaultSet = context.Observe(_targetObject, o => o is GameObject targetGameObject && dfc.Disposed is false ? dfc.GetDefaultBlendShapes(targetGameObject) : null, (a, b) =>
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.Equals(b);
        });
        if (defaultSet == null)
        {
            return;
        }

        // 何らかのGameObjcetを触っており、defaultExpressionはglobalもしくはpreset

        // Todo: // extractにcontext.GetComponentsがあるのどうにかしたい
        var expressionComponents = context.Observe(_targetObject, o => o is GameObject targetGameObject ? context.GetComponents<ExpressionComponentBase>(targetGameObject) : null, (a, b) => 
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.SequenceEqual(b);
        });
        if (expressionComponents != null && expressionComponents.Length > 0)
        {
            GetBlendShapes(expressionComponents, defaultSet, observeContext, result);
            return;
        }

        var conditionComponents = context.Observe(_targetObject, o => o is GameObject targetGameObject ? context.GetComponents<ConditionComponent>(targetGameObject) : null, (a, b) =>
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.SequenceEqual(b);
        });
        if (conditionComponents != null && conditionComponents.Length > 0)
        {
            var conditionComponent = conditionComponents.First();
            var childrenConditionComponents = conditionComponent.GetComponentsInChildren<ConditionComponent>(true);
            if (childrenConditionComponents.All(x => x.gameObject == conditionComponent.gameObject))
            {
                expressionComponents = context.GetComponentsInChildren<ExpressionComponentBase>(conditionComponent.gameObject, true);
                GetBlendShapes(expressionComponents, defaultSet, observeContext, result);
                return;
            }
        }

        var globalDefaultBlendShapes = dfc.GetGlobalDefaultBlendShapes();
        if (!defaultSet.SequenceEqual(globalDefaultBlendShapes))
        {
            // PresetdefaultExpression
            result.AddRange(defaultSet);
            return;
        }
        else
        {
            // GlobalDefaultExpression
            // DefaultPreviewと重複するが、DefaultPreviewがOFFの場合でも選択時はプレビューはして良いと思う。
            result.AddRange(globalDefaultBlendShapes);
            return;
        }
    }

    private static void GetBlendShapes(AnimationClip clip, BlendShapeSet result)
    {
        clip.GetFirstFrameBlendShapes(result);
    }

    private static void GetBlendShapes(IEnumerable<ExpressionComponentBase> expressionComponents, BlendShapeSet defaultSet, IObserveContext observeContext, BlendShapeSet result)
    {
        foreach (var expressionComponent in expressionComponents)
        {
            if (expressionComponent is not IHasBlendShapes hasBlendShapes) continue;
            hasBlendShapes.GetBlendShapes(result, defaultSet, observeContext);
        }
    }
}
