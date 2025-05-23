using nadena.dev.ndmf.preview;
using com.aoyon.facetune.Settings;

namespace com.aoyon.facetune.preview;

internal class SelectedShapesPreview : AbstractFaceTunePreview
{
    // 一時的に無効化出来るようにするために、必ずしもProjectSettings.EnableSelectedExpressionPreviewとは一致しない
    private static readonly PublishedValue<int> _disabledDepth = new(0); // 0で有効 無効化したい時は足す
    private static readonly PublishedValue<Object?> _targetObject = new(null);
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

    protected override BlendShapeSet? QueryBlendShapeSet(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, FaceTuneComponent mainComponent, ComputeContext context)
    {
        context.Observe(_disabledDepth, d => d, (a, b) => false);
        if (!Enabled) return null;

        if (!mainComponent.TryGetSessionContext(out var sessionContext)) return null;

        var clip = context.Observe(_targetObject, o => o as AnimationClip, (a, b) => a == b);
        if (clip != null)
        {
            return GetBlendShapeSet(clip);
        }

        var expressionComponents = context.Observe(_targetObject, o => (o as GameObject)?.GetComponents<FacialExpressionComponentBase>(), (a, b) => 
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.SequenceEqual(b);
        });
        if (expressionComponents != null && expressionComponents.Length > 0)
        {
            return GetBlendShapeSet(expressionComponents, sessionContext, context);
        }

        var conditionComponents = context.Observe(_targetObject, o => (o as GameObject)?.GetComponents<ConditionComponent>(), (a, b) => 
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.SequenceEqual(b);
        });
        if (conditionComponents != null && conditionComponents.Length == 1)
        {
            var conditionComponent = conditionComponents.First();
            var expressionRoot = context.Observe(conditionComponent, c => c.GetExpressionRoot(), (a, b) => a == b);
            var expressionComponents_ = context.GetComponentsInChildren<FacialExpressionComponentBase>(expressionRoot, false);
            return GetBlendShapeSet(expressionComponents_, sessionContext, context);
        }
        return null;
    }

    private static BlendShapeSet GetBlendShapeSet(AnimationClip clip)
    {
        return AnimationUtility.GetBlendShapesFromClip(clip).ToSet();
    }

    private static BlendShapeSet GetBlendShapeSet(IEnumerable<FacialExpressionComponentBase> expressionComponents, SessionContext sessionContext, ComputeContext context)
    {
        var blendShapes = new BlendShapeSet();
        foreach (var expressionComponent in expressionComponents)
        {
            var facialExpression = context.Observe(expressionComponent, c => (c as IExpressionProvider)!.ToExpression(sessionContext) as FacialExpression, (a, b) => 
            {
                if (a == null && b == null) return true;
                if (a == null || b == null) return false;
                return a.Equals(b);
            });
            if (facialExpression != null)
            {
                blendShapes.Add(facialExpression.BlendShapeSet);
            }
        }
        return blendShapes;
    }
}
