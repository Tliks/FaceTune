using nadena.dev.ndmf.preview;
using Aoyon.FaceTune.Settings;


namespace Aoyon.FaceTune.Preview;

internal class SelectedShapesPreview : DirectBlendShapePreview<SelectedShapesPreview>
{
    // 編集UIなどから一時的にプレビュー全体を無効化するための深さ。
    private static int _disabledDepth = 0; // 0で有効 無効化したい時は足す
    public static bool Enabled => _disabledDepth == 0;
    public static void MayEnable()
    {
        if (_disabledDepth <= 0) return;
        _disabledDepth--;
        if (Enabled) RebuildSessionFromSelection();
    }
    public static void Disable()
    {
        _disabledDepth++;
        DisposeSession();
    }

    private static SelectedShapesPreviewSession? _session;
    private static readonly List<(GameObject root, SkinnedMeshRenderer renderer, string path)> _targets = new();
    
    [InitializeOnLoadMethod]
    static void Init()
    {
        ProjectSettings.SelectedExpressionPreviewSettingsChanged += RebuildSessionFromSelection;
        Selection.selectionChanged += RebuildSessionFromSelection;
        RebuildSessionFromSelection();
    }

    private static void RebuildSessionFromSelection()
    {
        var selection = Selection.objects.Length == 1 ? Selection.objects[0] : null;
        RebuildSession(selection);
    }

    private static void RebuildSession(Object? selection)
    {
        DisposeSession();
        if (!Enabled) return;
        if (selection == null) return;

        var isProjectSelection = selection is AnimationClip || EditorUtility.IsPersistent(selection);
        var selectionPreviewEnabled = isProjectSelection
            ? ProjectSettings.EnableProjectSelectedExpressionPreview
            : ProjectSettings.EnableHierarchySelectedExpressionPreview;
        if (!selectionPreviewEnabled) return;

        _session = selection switch
        {
            AnimationClip clip => SelectedShapesPreviewSession.FromClip(
                clip, _targets, SetCurrentNodeDirectly, ClearCurrentNodeDirectly,
                () => RebuildSession(selection)),
            GameObject obj => SelectedShapesPreviewSession.FromGameObject(
                obj, _targets, SetCurrentNodeDirectly, ClearCurrentNodeDirectly,
                () => RebuildSession(selection)),
            _ => null
        };
    }

    private static void DisposeSession()
    {
        _session?.Dispose();
        _session = null;
    }

    // FaceTuneのコンポーネントがあれば常に対象とする
    protected override void GetTargetRenderers(ComputeContext context, List<SkinnedMeshRenderer> targetRenderers)
    {
        _targets.Clear();
        foreach (var root in context.GetAvatarRoots())
        {
            if (!AvatarContext.TryGet(root, out var avatarContext, out _, context)) continue;
            if (!_hasAnyComponent.Get(context, root)) continue;
            _targets.Add((root, avatarContext.FaceRenderer, avatarContext.BodyPath));
            targetRenderers.Add(avatarContext.FaceRenderer);
        }
    }

    // Component増減時の再計算の範囲を縮小するためのPropCache
    private static readonly PropCache<GameObject, bool> _hasAnyComponent = new(
        $"{nameof(SelectedShapesPreview)}:{nameof(HasAnyComponent)}", HasAnyComponent, (a, b) => a == b
    );

    private static bool HasAnyComponent(ComputeContext context, GameObject root)
    {
        var components = context.GetComponentsInChildren<FaceTuneTagComponent>(root, true);
        return components.Length > 0;
    }
}

internal class SelectedShapesPreviewSession : IDisposable
{
    private readonly (GameObject root, SkinnedMeshRenderer renderer, string path)[] _targets;
    private readonly Action<BlendShapeApply> _setPreview;
    private readonly Action<SkinnedMeshRenderer> _clearPreview;
    private readonly Action _onInvalidate;

    private readonly ComputeContext _context;
    private readonly List<Writer> _writers;
    private bool _disposed;

    private SelectedShapesPreviewSession(
        IReadOnlyList<(GameObject root, SkinnedMeshRenderer renderer, string path)> targets,
        Action<BlendShapeApply> setPreview,
        Action<SkinnedMeshRenderer> clearPreview,
        Action onInvalidate)
    {
        _targets = targets.ToArray();
        _setPreview = setPreview;
        _clearPreview = clearPreview;
        _onInvalidate = onInvalidate;
        _context = new($"{nameof(SelectedShapesPreviewSession)}:{nameof(_context)}");
        _writers = new List<Writer>();
        _context.InvokeOnInvalidate(this, s => s.OnInvalidate());
    }

    public static SelectedShapesPreviewSession FromClip(
        AnimationClip clip,
        IReadOnlyList<(GameObject root, SkinnedMeshRenderer renderer, string path)> targets,
        Action<BlendShapeApply> setPreview,
        Action<SkinnedMeshRenderer> clearPreview,
        Action onInvalidate)
    {
        var session = new SelectedShapesPreviewSession(targets, setPreview, clearPreview, onInvalidate);
        session.AddWriterForClip(clip, session._writers);
        return session;
    }

    public static SelectedShapesPreviewSession FromGameObject(
        GameObject gameObject,
        IReadOnlyList<(GameObject root, SkinnedMeshRenderer renderer, string path)> targets,
        Action<BlendShapeApply> setPreview,
        Action<SkinnedMeshRenderer> clearPreview,
        Action onInvalidate)
    {
        var session = new SelectedShapesPreviewSession(targets, setPreview, clearPreview, onInvalidate);
        session.AddWriterForGameObject(gameObject, session._writers);
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
        foreach (var node in _writers) node.Dispose();
        _writers.Clear();
    }
    
    private void AddWriterForClip(AnimationClip clip, List<Writer> resultToAdd)
    {
        var isLooping = _context.Observe(clip, c => c.isLooping, (a, b) => a == b);

        foreach (var (_, renderer, path) in _targets)
        {
            var animations = new List<BlendShapeWeightAnimation>();
            clip.GetBlendShapeAnimations(ClipImportOption.NonZero, animations, path);

            // Clip preview は既存 preview の上に、clip が持つ値だけを重ねる。
            resultToAdd.Add(Writer.Create(
                new BlendShapeApply(renderer, new BlendShapeWeightSet()),
                animations,
                isLooping,
                _setPreview,
                _clearPreview));
        }
    }

    private void AddWriterForGameObject(GameObject obj, List<Writer> resultToAdd)
    {
        var target = _targets
            .FirstOrDefault(pair => obj.transform.IsChildOf(pair.root.transform));
        if (target == default) return;

        var animations = new List<BlendShapeWeightAnimation>();
        if (!TryGetGameObjectAnimations(_context, obj, target.root, target.path, animations, out var isLooping)) return;

        var ignoredNames = AvatarContext.GetExplicitlyExcludedBlendShapeNames(target.root, _context);
        var apply = new BlendShapeApply(
            target.renderer,
            new BlendShapeWeightSet(),
            0f,
            ignoredNames);
        // GameObject preview は選択表情の facial style を含めて完全に置き換える。
        resultToAdd.Add(Writer.Create(apply, animations, isLooping, _setPreview, _clearPreview));
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

    sealed class Writer : IDisposable
    {
        private readonly SkinnedMeshRenderer _renderer;
        private readonly IDisposable? _multiFrame;
        private readonly Action<SkinnedMeshRenderer> _clearPreview;
        
        private Writer(SkinnedMeshRenderer renderer, IDisposable? multiFrame, Action<SkinnedMeshRenderer> clearPreview)
        {
            _renderer = renderer;
            _multiFrame = multiFrame;
            _clearPreview = clearPreview;
        }

        public static Writer Create(
            BlendShapeApply apply,
            List<BlendShapeWeightAnimation> animations,
            bool isLooping,
            Action<BlendShapeApply> applyPreview,
            Action<SkinnedMeshRenderer> clearPreview)
        {
            if (animations.Any(a => a.IsMultiFrame))
            {
                var multiFrame = new BlendShapeMultiFramePreview(
                    apply,
                    animations,
                    isLooping,
                    applyPreview);
                return new Writer(apply.Renderer, multiFrame, clearPreview);
            }

            applyPreview(apply with
            {
                Set = new BlendShapeWeightSet(animations.ToFirstFrameBlendShapes())
            });
            return new Writer(apply.Renderer, null, clearPreview);
        }

        public void Dispose()
        {
            _multiFrame?.Dispose();
            _clearPreview(_renderer);
        }
    }
}
