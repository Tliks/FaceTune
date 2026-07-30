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
        RebuildSession(GetCurrentSelection());
    }

    private static Object? GetCurrentSelection()
    {
        var selections = Selection.objects;
        return selections.Length == 1 ? selections[0] : null;
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
    private readonly Action<SkinnedMeshRenderer, IReadOnlyBlendShapeSet, float> _setPreview;
    private readonly Action<SkinnedMeshRenderer> _clearPreview;
    private readonly Action _onInvalidate;

    private readonly ComputeContext _context;
    private readonly List<Writer> _writers;
    private bool _disposed;

    private SelectedShapesPreviewSession(
        IReadOnlyList<(GameObject root, SkinnedMeshRenderer renderer, string path)> targets,
        Action<SkinnedMeshRenderer, IReadOnlyBlendShapeSet, float> setPreview,
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
        Action<SkinnedMeshRenderer, IReadOnlyBlendShapeSet, float> setPreview,
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
        Action<SkinnedMeshRenderer, IReadOnlyBlendShapeSet, float> setPreview,
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
            resultToAdd.Add(Writer.Create(renderer, animations, isLooping, (r, s) => _setPreview(r, s, -1), _clearPreview));
        }
    }

    private void AddWriterForGameObject(GameObject obj, List<Writer> resultToAdd)
    {
        var target = _targets
            .FirstOrDefault(pair => obj.transform.IsChildOf(pair.root.transform));
        if (target == default) return;

        var animations = new List<BlendShapeWeightAnimation>();
        if (!TryGetGameObjectAnimations(_context, obj, target.root, target.path, animations, out var isLooping)) return;

        // GameObject preview は選択表情の facial style を含めて完全に置き換える。
        resultToAdd.Add(Writer.Create(target.renderer, animations, isLooping, (r, s) => _setPreview(r, s, 0), _clearPreview));
    }

    private static bool TryGetGameObjectAnimations(ComputeContext context, GameObject target, GameObject root, string bodyPath, List<BlendShapeWeightAnimation> resultToAdd, out bool isLooping)
    {
        isLooping = false;

        using var _dataComponents = ListPool<DataComponent>.Get(out var dataComponents);
        if (TryGetDataSource(context, target, dataComponents, out var expressionComponent, out var sourceRoot))
        { 
            // dataCompononentのデータ取得用および、代入用にに顔つきを取得する
            using var _facial = ListPool<BlendShapeWeightAnimation>.Get(out var facial);
            FacialStyleContext.TryGetFacialStyleAnimations(sourceRoot, facial, root, bodyPath, context);
            
            resultToAdd.AddRange(facial);

            if (expressionComponent != null)
            {
                context.Observe(expressionComponent);
                expressionComponent.GetAnimations(resultToAdd, bodyPath);
            }

            foreach (var dataComponent in dataComponents)
            {
                context.Observe(dataComponent);
                dataComponent.GetAnimations(resultToAdd, bodyPath);
            }

            if (expressionComponent != null)
            {
                isLooping = context.Observe(expressionComponent, e => e.ExpressionSettings.LoopTime, (a, b) => a == b);
            }

            return true;
        }

        return false;
    }

    private static bool TryGetDataSource(ComputeContext context, GameObject gameObject, List<DataComponent> dataComponents, out FaceTuneComponent? expressionComponent, [NotNullWhen(true)]out GameObject? sourceRoot)
    {
        expressionComponent = null;
        sourceRoot = null;

        using var _expressionComponents = ListPool<FaceTuneComponent>.Get(out var expressionComponents);
        context.GetComponentsInChildren<FaceTuneComponent>(gameObject, true, expressionComponents);

        if (expressionComponents.Count > 1) return false;

        if (expressionComponents.Count == 1)
        {
            expressionComponent = expressionComponents[0];
            sourceRoot = expressionComponent.gameObject;

            using var _children = ListPool<DataComponent>.Get(out var children);
            context.GetComponentsInChildren(sourceRoot, true, children);
            dataComponents.AddRange(children);
            return true;
        }

        sourceRoot = gameObject;
        context.GetComponentsInChildren<DataComponent>(gameObject, true, dataComponents);
        return dataComponents.Count > 0;
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
            SkinnedMeshRenderer renderer,
            List<BlendShapeWeightAnimation> animations,
            bool isLooping,
            Action<SkinnedMeshRenderer, IReadOnlyBlendShapeSet> apply,
            Action<SkinnedMeshRenderer> clearPreview)
        {
            if (animations.Any(a => a.IsMultiFrame))
            {
                var multiFrame = new BlendShapeMultiFramePreview(renderer, animations, isLooping, apply);
                return new Writer(renderer, multiFrame, clearPreview);
            }

            apply(renderer, new BlendShapeWeightSet(animations.ToFirstFrameBlendShapes()));
            return new Writer(renderer, null, clearPreview);
        }

        public void Dispose()
        {
            _multiFrame?.Dispose();
            _clearPreview(_renderer);
        }
    }
}
