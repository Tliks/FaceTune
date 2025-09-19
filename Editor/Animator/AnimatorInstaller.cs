using UnityEditor.Animations;
using nadena.dev.ndmf.animator;
using Aoyon.FaceTune.Build;

namespace Aoyon.FaceTune.Animator;

internal class AnimatorInstaller : InstallerBase
{
    private const int InitLayerPriority = -1; // 上書きを意図しない初期化レイヤー。
    
    private VirtualClip _nonMMDInitializationClip = null!;
    private VirtualClip _MMDInitializationClip = null!;

    private readonly float _transitionDurationSeconds;

    private readonly Dictionary<AvatarExpression, VirtualClip> _expressionClipCache = new();

    private readonly LipSyncInstaller _lipSyncInstaller;
    private readonly BlinkInstaller _blinkInstaller;

    private static readonly Vector3 ExclusiveStatePosition = new Vector3(300, 0, 0);

    public AnimatorInstaller(VirtualAnimatorController virtualController, AvatarContext avatarContext, bool useWriteDefaults) : base(virtualController, avatarContext, useWriteDefaults)
    {
        _transitionDurationSeconds = 0.1f; // 変更可能にすべき？
        _lipSyncInstaller = new LipSyncInstaller(virtualController, avatarContext, useWriteDefaults);
        _blinkInstaller = new BlinkInstaller(virtualController, avatarContext, useWriteDefaults);
    }

    public void Execute(InstallerData installerData)
    {
        var patternData = installerData.PatternData;
        if (patternData.IsEmpty) return;

        CreateInitializationLayer(patternData, InitLayerPriority);

        foreach (var group in patternData.Groups)
        {
            var isBlending = group.IsBlending;
            if (isBlending)
            {
                InstallBlending(group);
            }
            else
            {
                InstallNonBlending(group);
            }
        }
        
        var patternExpressions = patternData.GetAllExpressions();
        if (patternExpressions.Any(e => e.FacialSettings.AllowEyeBlink == TrackingPermission.Disallow
            || e.FacialSettings.AdvancedEyBlinkSettings.UseAdvancedEyeBlink))
        {
            _blinkInstaller.AddEyeBlinkLayer();
            AddBlendShapeInitialization(_blinkInstaller.ShapesToInitialize);
        }

        _lipSyncInstaller.MayAddLipSyncLayers();
    }

    private void CreateInitializationLayer(PatternData patternData, int priority)
    {
        var nonMMDLayer = AddLayer("Initial", priority, false);
        var nonMMDState = AddState(nonMMDLayer, "Initial", position: ExclusiveStatePosition);
        _nonMMDInitializationClip = nonMMDState.CreateClip(nonMMDState.Name);

        var MMDLayer = AddLayer("Initial (MMD)", priority, true);
        var MMDState = AddState(MMDLayer, "Initial (MMD)", position: ExclusiveStatePosition);
        _MMDInitializationClip = MMDState.CreateClip(MMDState.Name);

        var animations = new List<GenericAnimation>();
        var mmdAnimations = new List<GenericAnimation>();

        foreach (var shape in _avatarContext.FaceRenderer.GetBlendShapes(_avatarContext.FaceMesh).Where(b => !_avatarContext.TrackedBlendShapes.Contains(b.Name)))
        {
            if (IsMMDBlendShapeName(shape.Name))
            {
                mmdAnimations.Add(shape.ToGenericAnimation(_avatarContext.BodyPath));
            }
            else
            {
                animations.Add(shape.ToGenericAnimation(_avatarContext.BodyPath));
            }
        }

        var allBindings = patternData.GetAllExpressions().SelectMany(e => e.AnimationSet.Animations).Select(a => a.CurveBinding).Distinct();
        var nonFacialBindings = new List<SerializableCurveBinding>();
        foreach (var binding in allBindings)
        {
            if (binding.Path == _avatarContext.BodyPath &&
                binding.Type == typeof(SkinnedMeshRenderer) &&
                binding.PropertyName.StartsWith(FaceTuneConstants.AnimatedBlendShapePrefix))
            {
                continue;
            }
            nonFacialBindings.Add(binding);
        }
        if (nonFacialBindings.Any())
        {
            var propertiesAnimations = AnimatorHelper.GetDefaultValueAnimations(_avatarContext.Root, nonFacialBindings);
            animations.AddRange(propertiesAnimations);
        }

        _MMDInitializationClip.AddAnimations(mmdAnimations);
        _nonMMDInitializationClip.AddAnimations(animations);
    }

    private void AddBlendShapeInitialization(IEnumerable<BlendShapeWeight> blendShapes)
    {
        foreach (var shape in blendShapes)
        {
            if (IsMMDBlendShapeName(shape.Name))
            {
                _MMDInitializationClip.AddAnimation(shape.ToGenericAnimation(_avatarContext.BodyPath));
            }
            else
            {
                _nonMMDInitializationClip.AddAnimation(shape.ToGenericAnimation(_avatarContext.BodyPath));
            }
        }
    }

    private void InstallBlending(ExpressionWithConditionGroup group)
    {
        foreach (var expressionWithNormalizedCondition in group.ExpressionWithConditions)
        {
            CreateLayerForExpressions(LayerPriority, "Blending: " + expressionWithNormalizedCondition.Expression.Name, expressionWithNormalizedCondition);
        }
    }

    private void InstallNonBlending(ExpressionWithConditionGroup group)
    {
        CreateLayerForExpressions(LayerPriority, "Non-Blending", group.ExpressionWithConditions.ToArray());
    }

    private void CreateLayerForExpressions(int priority, string layerName, params ExpressionWithNormalizedCondition[] expressionWithConditions)
    {
        if (expressionWithConditions.Length == 0) return;

        var layer = AddLayer(layerName, priority);

        var passThroughState = AddState(layer, "PassThrough", ExclusiveStatePosition);
        AsPassThrough(passThroughState);

        var stateMachine = layer.StateMachine!;

        var basePosition = ExclusiveStatePosition + new Vector3(0, 2 * PositionYStep, 0);
        var position = basePosition;

        ICondition passThroughToExitCondition = TrueCondition.Instance;

        for (int i = 0; i < expressionWithConditions.Length; i++)
        {
            var expressionWithNormalizedCondition = expressionWithConditions[i];

            var expression = expressionWithNormalizedCondition.Expression;
            var condition = expressionWithNormalizedCondition.Condition;

            var state = AddState(layer, expression.Name, position);
            position.y += PositionYStep;

            AddExpressionToState(state, expression);

            // Entry
            var entryTransition = VirtualTransition.Create();
            entryTransition.SetDestination(state);
            stateMachine.EntryTransitions = stateMachine.EntryTransitions.AddRange(NormalizedContiditnToTransitions(condition, entryTransition));

            // ExitはExntryのNegation
            var exitTransition = AnimatorHelper.CreateTransitionWithDurationSeconds(_transitionDurationSeconds);
            exitTransition.SetExitDestination();
            state.Transitions = state.Transitions.AddRange(NormalizedContiditnToTransitions(condition.ToNegation().Normalize().Optimize(), exitTransition));

            // PassThrough to Exitに、各ExpressionのEntry条件を追加
            // passThroughToExitCondition = passThroughToExitCondition.And(condition);
        }

        var defaultExitTransition = AnimatorHelper.CreateTransitionWithDurationSeconds(_transitionDurationSeconds);
        defaultExitTransition.SetExitDestination();
        passThroughState.Transitions = passThroughState.Transitions.AddRange(NormalizedContiditnToTransitions(passThroughToExitCondition.Normalize().Optimize(), defaultExitTransition));
    }

    public List<T> NormalizedContiditnToTransitions<T>(NormalizedCondition condition, T referenceTransition) where T : VirtualTransitionBase
    {
        var newTransitions = new List<T>();
        foreach (var clause in condition.Clauses) // OR
        {
            var duplicate = (referenceTransition.Clone() as T)!;
            var andConditions = clause.Conditions.Select(c => ToAnimatorCondition(c));
            duplicate.Conditions = ImmutableList.CreateRange(andConditions);
            newTransitions.Add(duplicate);
        }
        return newTransitions;
    }
    
    private AnimatorCondition ToAnimatorCondition(IBaseCondition condition)
    {
        var (animatorCondition, parameter, parameterType) = condition.ToAnimatorCondition();
        _controller.EnsureParameterExists(parameterType, parameter);
        return animatorCondition;
    }

    private void AddExpressionToState(VirtualState state, AvatarExpression expression)
    {
        if (state.TryGetClip(out var clip))
        {
            var duplicate = clip.Clone();
            Impl(duplicate);
            state.Motion = duplicate;
        }
        else
        {
            if (_expressionClipCache.TryGetValue(expression, out var cachedClip))
            {
                clip = cachedClip;
                state.Motion = clip;
            }
            else
            {
                clip = state.CreateClip(state.Name);
                Impl(clip);
                _expressionClipCache[expression] = clip;
            }
        }

        void Impl(VirtualClip clip)
        {
            clip.AddAnimations(expression.AnimationSet);
            SetExpressionSettings(state, clip, expression.ExpressionSettings);
            SetFacialSettings(clip, expression.FacialSettings);
        }
    }

    private void SetExpressionSettings(VirtualState state, VirtualClip clip, ExpressionSettings expressionSettings)
    {
        if (expressionSettings.LoopTime)
        {
            var settings = clip.Settings;
            settings.loopTime = true;
            clip.Settings = settings;
        }
        else if (!string.IsNullOrEmpty(expressionSettings.MotionTimeParameterName))
        {
            _controller.EnsureParameterExists(AnimatorControllerParameterType.Float, expressionSettings.MotionTimeParameterName);
            state.TimeParameter = expressionSettings.MotionTimeParameterName;
        }
    }

    private void SetFacialSettings(VirtualClip clip, FacialSettings? facialSettings)
    {
        if (facialSettings == null) return;
        _blinkInstaller.SetSettings(clip, facialSettings);
        _lipSyncInstaller.SetSettings(clip, facialSettings);
    }

    private bool IsMMDBlendShapeName(string name)
    {
        return MmdBlendShapeNames.Contains(name);
    }

#nullable disable
    private static readonly HashSet<string> MmdBlendShapeNames = new HashSet<string>
    {
        // New EN by Yi MMD World
        //  https://docs.google.com/spreadsheets/d/1mfE8s48pUfjP_rBIPN90_nNkAIBUNcqwIxAdVzPBJ-Q/edit?usp=sharing
        // Old EN by Xoriu
        //  https://booth.pm/ja/items/3341221
        //  https://images-wixmp-ed30a86b8c4ca887773594c2.wixmp.com/i/0b7b5e4b-c62e-41f7-8ced-1f3e58c4f5bf/d5nbmvp-5779f5ac-d476-426c-8ee6-2111eff8e76c.png
        // Old EN, New EN, JA,

        // ===== Mouth =====
        "a",            "Ah",               "あ",
        "i",            "Ch",               "い",
        "u",            "U",                "う",
        "e",            "E",                "え",
        "o",            "Oh",               "お",
        "Niyari",       "Grin",             "にやり",
        "Mouse_2",      "∧",                "∧",
        "Wa",           "Wa",               "ワ",
        "Omega",        "ω",                "ω",
        "Mouse_1",      "▲",                "▲",
        "MouseUP",      "Mouth Horn Raise", "口角上げ",
        "MouseDW",      "Mouth Horn Lower", "口角下げ",
        "MouseWD",      "Mouth Side Widen", "口横広げ", 
        "n",            null,               "ん",
        "Niyari2",      null,               "にやり２",
        // by Xoriu only
        "a 2",          null,               "あ２",
        "□",            null,               "□",
        "ω□",           null,               "ω□",
        "Smile",        null,               "にっこり",
        "Pero",         null,               "ぺろっ",
        "Bero-tehe",    null,               "てへぺろ",
        "Bero-tehe2",   null,               "てへぺろ２",

        // ===== Eyes =====
        "Blink",        "Blink",            "まばたき",
        "Smile",        "Blink Happy",      "笑い",
        "> <",          "Close><",          "はぅ",
        "EyeSmall",     "Pupil",            "瞳小",
        "Wink-c",       "Wink 2 Right",     "ｳｨﾝｸ２右",
        "Wink-b",       "Wink 2",           "ウィンク２",
        "Wink",         "Wink",             "ウィンク",
        "Wink-a",       "Wink Right",       "ウィンク右",
        "Howawa",       "Calm",             "なごみ",
        "Jito-eye",     "Stare",            "じと目",
        "Ha!!!",        "Surprised",        "びっくり",
        "Kiri-eye",     "Slant",            "ｷﾘｯ",
        "EyeHeart",     "Heart",            "はぁと",
        "EyeStar",      "Star Eye",         "星目",
        "EyeFunky",     null,               "恐ろしい子！",
        // by Xoriu only
        "O O",          null,               "はちゅ目",
        "EyeSmall-v",   null,               "瞳縦潰れ",
        "EyeUnderli",   null,               "光下",
        "EyHi-Off",     null,               "ハイライト消",
        "EyeRef-off",   null,               "映り込み消",

        // ===== Eyebrow =====
        "Smily",        "Cheerful",         "にこり",
        "Up",           "Upper",            "上",
        "Down",         "Lower",            "下",
        "Serious",      "Serious",          "真面目",
        "Trouble",      "Sadness",          "困る",
        "Get angry",    "Anger",            "怒り",
        null,           "Front",            "前",
        
        // ===== Eyes + Eyebrow Feeling =====
        // by Xoriu only
        "Joy",          null,               "喜び",
        "Wao!?",        null,               "わぉ!?",
        "Howawa ω",     null,               "なごみω",
        "Wail",         null,               "悲しむ",
        "Hostility",    null,               "敵意",

        // ===== Other ======
        null,           "Blush",            "照れ",
        "ToothAnon",    null,               "歯無し下",
        "ToothBnon",    null,               "歯無し上",
        null,           null,               "涙",

        // others

        // https://gist.github.com/lilxyzw/80608d9b16bf3458c61dec6b090805c5
        "しいたけ",

        // https://site.nicovideo.jp/ch/userblomaga_thanks/archive/ar1471249
        "なぬ！",
        "はんっ！",
        "えー",
        "睨み",
        "睨む",
        "白目",
        "瞳大",
        "頬染め",
        "青ざめ",
    }.Where(x => x != null).Distinct().ToHashSet(); // removed null with Where
#nullable restore
}
