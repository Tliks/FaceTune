using nadena.dev.ndmf.preview;
using Aoyon.FaceTune.Settings;


namespace Aoyon.FaceTune.Preview;

internal class SelectedShapesPreview : DirectBlendShapePreview<SelectedShapesPreview>
{
    // 一時的に無効化出来るようにするために、必ずしもProjectSettings.EnableSelectedExpressionPreviewとは一致しない
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
        _disabledDepth = ProjectSettings.EnableSelectedExpressionPreview ? 0 : 1;
        ProjectSettings.EnableSelectedExpressionPreviewChanged += (value) => { if (value) MayEnable(); else Disable(); };
        
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

        _session = new SelectedShapesPreviewSession(
            selection,
            _targets,
            SetCurrentNodeDirectly,
            ClearCurrentNodeDirectly,
            () => RebuildSession(selection)
        );
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
            if (!AvatarContextBuilder.TryGetFaceRenderer(root, out var faceRenderer, out var path, null, context)) continue;
            if (!_hasAnyComponent.Get(context, root)) continue;
            _targets.Add((root, faceRenderer, path));
            targetRenderers.Add(faceRenderer);
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

    public SelectedShapesPreviewSession(
        Object selection,
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
        _writers = CreateWriters(selection);
        _context.InvokeOnInvalidate(this, s => s.OnInvalidate());
    }

    private List<Writer> CreateWriters(Object selection)
    {
        var writers = new List<Writer>();

        if (selection is AnimationClip clip)
        {
            AddWriterForClip(clip, writers);
        }
        else if (selection is GameObject obj)
        {
            AddWriterForGameObject(obj, writers);
        }
        else
        {
            // no-op
        }

        return writers;
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
        if (TryGetExpressionData(context, target, root, dataComponents, out var expressionComponent))
        { 
            // dataCompononentのデータ取得用および、代入用にに顔つきを取得する
            using var _facial = ListPool<BlendShapeWeightAnimation>.Get(out var facial);
            FacialStyleContext.TryGetFacialStyleAnimations(dataComponents[0].gameObject, facial, root, context);
            
            resultToAdd.AddRange(facial);

            foreach (var dataComponent in dataComponents)
            {
                context.Observe(dataComponent);
                ExpressionDataUtility.ResolveAnimations(dataComponent, resultToAdd, facial, bodyPath);
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
    private static bool TryGetExpressionData(ComputeContext context, GameObject gameObject, GameObject root, List<DataComponent> dataComponents, out FaceTuneComponent? expressionComponent)
    {
        expressionComponent = null;

        var dataComponent = context.GetComponent<DataComponent>(gameObject);
        if (dataComponent != null)
        {
            var targetGameObject = dataComponent.gameObject;
            if (!TryGetDataComponentsInChildren(context, targetGameObject, dataComponents)) return false;
            context.TryGetComponentInParent(targetGameObject, root, true, out expressionComponent);
            return true;
        }

        var _expressionComponent = context.GetComponent<FaceTuneComponent>(gameObject);
        if (_expressionComponent != null)
        {
            var targetGameObject = _expressionComponent.gameObject;
            if (!TryGetDataComponentsInChildren(context, targetGameObject, dataComponents)) return false;
            expressionComponent = _expressionComponent;
            return true;
        }

        var conditionComponent = context.GetComponent<ConditionComponent>(gameObject);
        if (conditionComponent != null)
        {
            using var _ = ListPool<ConditionComponent>.Get(out var childrenConditionComponents);
            conditionComponent.gameObject.GetComponentsInChildren(true, childrenConditionComponents);
            // 末端のConditionのみを対象にする。上のConditioを対象にすると、本来別の表情用のDataが混ざる可能性がある。
            if (childrenConditionComponents.All(x => x.gameObject == conditionComponent.gameObject))
            {
                var targetGameObject = conditionComponent.gameObject;
                if (!TryGetDataComponentsInChildren(context, targetGameObject, dataComponents)) return false;
                context.TryGetComponentInParent(targetGameObject, root, true, out expressionComponent);
                return true;
            }
        }

        return false;

        static bool TryGetDataComponentsInChildren(ComputeContext context, GameObject gameObject, List<DataComponent> dataComponents)
        {
            context.GetComponentsInChildren(gameObject, true, dataComponents);
            if (dataComponents.Count == 0) return false;
            return true;
        }
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
