using nadena.dev.ndmf.preview;
using com.aoyon.facetune.Settings;

namespace com.aoyon.facetune.preview;

internal class SelectedShapesPreview : AbstractFaceTunePreview
{
    // 一時的に無効化出来るようにするために、必ずしもProjectSettings.EnableSelectedExpressionPreviewとは一致しない
    private static readonly PublishedValue<int> _disabledDepth = new(0); // 0で有効 無効化したい時は足す
    private static readonly PublishedValue<GameObject?> _targetGameObject = new(null);
    public static bool Enabled => _disabledDepth.Value == 0;
    public static void Disable() => _disabledDepth.Value++;
    public static void MayEnable() => _disabledDepth.Value--;

    [InitializeOnLoadMethod]
    static void Init()
    {
        _disabledDepth.Value = ProjectSettings.EnableSelectedExpressionPreview ? 0 : 1;
        _targetGameObject.Value = Selection.objects.OfType<GameObject>().FirstOrNull();
        Selection.selectionChanged += OnSelectionChanged;
        ProjectSettings.EnableSelectedExpressionPreviewChanged += (value) => { if (value) MayEnable(); else Disable(); };
    }

    private static void OnSelectionChanged()
    {
        if (!Enabled) return;

        var selections = Selection.objects.OfType<GameObject>();

        GameObject? target = null;
        if (selections.Count() == 1)
        {
            target = selections.First();
        }

        _targetGameObject.Value = target;
    }

    // FacialExpressionComponent以外の取得が必要
    protected override BlendShapeSet? QueryBlendShapeSet(SkinnedMeshRenderer original, SkinnedMeshRenderer proxy, FaceTuneComponent mainComponent, ComputeContext context)
    {
        context.Observe(_disabledDepth, d => d, (a, b) => false);
        if (!Enabled) return null;

        var expressionComponents = context.Observe(_targetGameObject, c => c?.GetComponents<FacialExpressionComponent>(), (a, b) => 
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.SequenceEqual(b);
        });
        if (expressionComponents != null && expressionComponents.Length > 0)
        {
            return GetBlendShapeSet(expressionComponents, context);
        }

        var connectComponents = context.Observe(_targetGameObject, c => c?.GetComponents<ConnectConditionAndExpressionComponent>(), (a, b) => 
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.SequenceEqual(b);
        });
        if (connectComponents != null && connectComponents.Length == 1)
        {
            var handGestureComponent = connectComponents.First();
            var expressionRoot = context.Observe(handGestureComponent, c => c.ExpressionRoot, (a, b) => a == b);
            var expressionComponents_ = context.GetComponentsInChildren<FacialExpressionComponent>(expressionRoot, false);
            return GetBlendShapeSet(expressionComponents_, context);
        }
        return null;
    }

    private static BlendShapeSet GetBlendShapeSet(FacialExpressionComponent[] expressionComponents, ComputeContext context)
    {
        var blendShapes = new List<BlendShape>();
        foreach (var expressionComponent in expressionComponents)
        {
            var defaultBlendShapes = context.Observe(expressionComponent, c => new List<BlendShape>(c.BlendShapes), (a, b) => a.SequenceEqual(b));
            blendShapes.AddRange(defaultBlendShapes);
        }
        return new BlendShapeSet(blendShapes);
    }
}
