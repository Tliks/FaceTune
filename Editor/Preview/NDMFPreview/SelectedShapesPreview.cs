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

    protected override BlendShapeSet? QueryBlendShapeSet(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, SessionContext sessionContext, ComputeContext context)
    {
        var observeContext = new NDMFPreviewObserveContext(context);
        if (!IsEnabled(context)) return null;

        var dfc = sessionContext.DEC;
        
        var clip = context.Observe(_targetObject, o => o as AnimationClip, (a, b) => a == b);
        if (clip != null)
        {
            return GetBlendShapeSet(clip);
        }

        var defaultExpression = context.Observe(_targetObject, o => o is GameObject targetGameObject ? dfc.GetDefaultExpression(targetGameObject) : null, (a, b) =>
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.Equals(b);
        });
        if (defaultExpression == null)
        {
            return null;
        }

        // 何らかのGameObjcetを触っており、defaultExpressionはglobalもしくはpreset

        var expressionComponents = context.Observe(_targetObject, o => o is GameObject targetGameObject ? context.GetComponents<ExpressionComponentBase>(targetGameObject) : null, (a, b) =>
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.SequenceEqual(b);
        });
        if (expressionComponents != null && expressionComponents.Length > 0)
        {
            return GetBlendShapeSet(expressionComponents, defaultExpression, observeContext);
        }

        var conditionComponents = context.Observe(_targetObject, o => o is GameObject targetGameObject ? context.GetComponents<ConditionComponent>(targetGameObject) : null, (a, b) =>
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.SequenceEqual(b);
        });
        if (conditionComponents != null && conditionComponents.Length == 1)
        {
            var conditionComponent = conditionComponents.First();
            var expressionComponents_ = conditionComponent.GetExpressionComponents(observeContext);
            return GetBlendShapeSet(expressionComponents_, defaultExpression, observeContext);
        }

        var globalDefaultExpression = dfc.GetGlobalDefaultExpression();
        if (!defaultExpression.Equals(globalDefaultExpression))
        {
            // PresetdefaultExpression
            return defaultExpression.BlendShapeSet;
        }
        else
        {
            // GlobalDefaultExpression
            // DefaultPreviewと重複するが、DefaultPreviewがOFFの場合でも選択時はプレビューはして良いと思う。
            return globalDefaultExpression.BlendShapeSet;
        }
    }

    private static BlendShapeSet GetBlendShapeSet(AnimationClip clip)
    {
        return clip.GetBlendShapes().ToSet();
    }

    private static BlendShapeSet GetBlendShapeSet(IEnumerable<ExpressionComponentBase> expressionComponents, FacialExpression defaultExpression, IOberveContext observeContext)
    {
        var blendShapes = new BlendShapeSet();
        foreach (var expressionComponent in expressionComponents)
        {
            var expression = observeContext.Observe(expressionComponent, c => (c as IExpressionProvider)!.ToExpression(defaultExpression, observeContext), (a, b) => 
            {
                if (a == null && b == null) return true;
                if (a == null || b == null) return false;
                return a.Equals(b);
            });
            if (expression != null)
            {
                blendShapes.Add(expression.GetBlendShapeSet());
            }
        }
        if (blendShapes.Count == 0) return defaultExpression.BlendShapeSet;
        return blendShapes;
    }
}
