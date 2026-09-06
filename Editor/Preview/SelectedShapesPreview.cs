using nadena.dev.ndmf.preview;
using Aoyon.FaceTune.Settings;


namespace Aoyon.FaceTune.Preview;

internal class SelectedShapesPreview
{
    private readonly DirectBlendShapePreviewContext _preview;
    private SelectedShapesPreviewSession? _session;

    internal SelectedShapesPreview(DirectBlendShapePreviewContext preview)
    {
        _preview = preview;
        ProjectSettings.SelectedExpressionPreviewSettingsChanged += RebuildSessionFromSelection;
        Selection.selectionChanged += RebuildSessionFromSelection;
        RebuildSessionFromSelection();
    }

    private void RebuildSessionFromSelection()
    {
        var selection = Selection.objects.Length == 1 ? Selection.objects[0] : null;
        RebuildSession(selection);
    }

    private void RebuildSession(Object? selection)
    {
        DisposeSession();
        if (selection == null) return;

        var isProjectSelection = selection is AnimationClip || EditorUtility.IsPersistent(selection);
        var selectionPreviewEnabled = isProjectSelection
            ? ProjectSettings.EnableProjectSelectedExpressionPreview
            : ProjectSettings.EnableHierarchySelectedExpressionPreview;
        if (!selectionPreviewEnabled) return;

        _session = selection switch
        {
            AnimationClip clip => SelectedShapesPreviewSession.FromClip(
                clip, _preview, () => RebuildSession(selection)),
            GameObject obj => SelectedShapesPreviewSession.FromGameObject(
                obj, _preview, () => RebuildSession(selection)),
            _ => null
        };
    }

    private void DisposeSession()
    {
        _session?.Dispose();
        _session = null;
    }
}

internal class SelectedShapesPreviewSession : IDisposable
{
    private readonly DirectBlendShapePreviewContext _preview;
    private readonly Action _onInvalidate;

    private readonly ComputeContext _context;
    private readonly List<IDisposable> _previews;
    private bool _disposed;

    private SelectedShapesPreviewSession(
        DirectBlendShapePreviewContext preview,
        Action onInvalidate)
    {
        _preview = preview;
        _onInvalidate = onInvalidate;
        _context = new($"{nameof(SelectedShapesPreviewSession)}:{nameof(_context)}");
        _previews = new List<IDisposable>();
        _context.InvokeOnInvalidate(this, s => s.OnInvalidate());
    }

    public static SelectedShapesPreviewSession FromClip(
        AnimationClip clip,
        DirectBlendShapePreviewContext preview,
        Action onInvalidate)
    {
        var session = new SelectedShapesPreviewSession(preview, onInvalidate);
        session.AddWriterForClip(clip, session._previews);
        return session;
    }

    public static SelectedShapesPreviewSession FromGameObject(
        GameObject gameObject,
        DirectBlendShapePreviewContext preview,
        Action onInvalidate)
    {
        var session = new SelectedShapesPreviewSession(preview, onInvalidate);
        session.AddWriterForGameObject(gameObject, session._previews);
        return session;
    }

    private void OnInvalidate()
    {
        if (_disposed) return;
        _onInvalidate();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var preview in _previews) preview.Dispose();
        _previews.Clear();
    }
    
    private void AddWriterForClip(AnimationClip clip, List<IDisposable> resultToAdd)
    {
        var isLooping = _context.Observe(clip, c => c.isLooping, (a, b) => a == b);
        var targets = _preview.GetTargets(_context);

        foreach (var target in targets)
        {
            var animations = new List<BlendShapeWeightAnimation>();
            clip.GetBlendShapeAnimations(ClipImportOption.NonZero, animations, target.BodyPath);

            // Clip preview は既存 preview の上に、clip が持つ値だけを重ねる。
            var apply = new BlendShapeApply(new ImmutableBlendShapeWeightSet());
            resultToAdd.Add(_preview.ApplyAnimation(target.FaceRenderer, apply, animations, isLooping));
        }
    }

    private void AddWriterForGameObject(GameObject obj, List<IDisposable> resultToAdd)
    {
        var target = _preview.GetTargets(_context)
            .FirstOrDefault(target => obj.transform.IsChildOf(target.Root.transform));
        if (target == null) return;

        var animations = new List<BlendShapeWeightAnimation>();
        if (!TryGetGameObjectAnimations(_context, obj, target.Root, target.BodyPath, animations, out var isLooping)) return;

        var ignoredNames = AvatarContext.GetExplicitlyExcludedBlendShapeNames(target.Root, _context);
        var apply = new BlendShapeApply(new ImmutableBlendShapeWeightSet(), 0f, ignoredNames);
        // GameObject preview は選択表情の facial style を含めて完全に置き換える。
        resultToAdd.Add(_preview.ApplyAnimation(target.FaceRenderer, apply, animations, isLooping));
    }

    private static bool TryGetGameObjectAnimations(ComputeContext context, GameObject target, GameObject root, string bodyPath, List<BlendShapeWeightAnimation> resultToAdd, out bool isLooping)
    {
        using var _ = ListPool<ExpressionComponent>.Get(out var expressions);
        context.GetComponentsInChildren<ExpressionComponent>(target, true, expressions);
        var expressionCount = expressions.Count;

        // 配下に複数Expressionがある場合は境界が推定不能なので無効化
        if (expressionCount > 1)
        {
            isLooping = false;
            return false;
        }

        if (expressionCount == 1)
        {
            var expression = expressions[0];
            var facial = new FacialAnimationResolver(root, context);
            resultToAdd.AddRange(facial.ResolveIncoming(expression.transform, bodyPath));
            if (facial.TryResolve(expression, bodyPath, out var definition))
                resultToAdd.AddRange(definition);
            isLooping = new MultiFrameResolver(context).Resolve(expression).MultiFrameMode
                        == MultiFrameSettings.Kind.Loop;
        }
        else
        {
            // Dataの配置はExpressionへ影響しないが、Data自身を選択した場合は編集用にpreviewする。
            var dataComponents = context.GetComponents<ExpressionDataComponent>(target).ToList();
            var facial = new FacialAnimationResolver(root, context);
            if (dataComponents.Count != 1
                || !facial.TryResolve(dataComponents[0], bodyPath, out var dataAnimations))
            {
                isLooping = false;
                return false;
            }

            resultToAdd.AddRange(facial.ResolveIncoming(target.transform, bodyPath));
            foreach (var animation in dataAnimations)
                resultToAdd.Add(animation);
            isLooping = false;
        }

        return true;
    }
}
