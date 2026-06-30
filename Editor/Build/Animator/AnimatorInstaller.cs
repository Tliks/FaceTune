using UnityEditor.Animations;
using nadena.dev.ndmf.animator;

namespace Aoyon.FaceTune.Build.Animator;

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
        _controller.EnsureBoolParameterExists(FaceTuneConstants.LockFacialParameter, false);
    }

    public void Execute(ExpressionProgram expressionProgram)
    {
        if (expressionProgram.IsEmpty) return;

        CreateInitializationLayer(InitLayerPriority);
        InstallExpressionProgram(expressionProgram, LayerPriority);
        
        var expressions = expressionProgram.GetAllExpressions();
        if (expressions.Any(e => e.FacialSettings.AllowEyeBlink == TrackingPermission.Disallow
            || e.FacialSettings.AdvancedEyBlinkSettings.UseAdvancedEyeBlink))
        {
            _blinkInstaller.AddEyeBlinkLayer();
            AddBlendShapeInitialization(_blinkInstaller.ShapesToInitialize);
        }

        _lipSyncInstaller.MayAddLipSyncLayers();
    }

    private void CreateInitializationLayer(int priority)
    {
        var nonMMDLayer = AddLayer("Initial", priority, false);
        var nonMMDState = AddState(nonMMDLayer, "Initial", position: ExclusiveStatePosition);
        _nonMMDInitializationClip = nonMMDState.CreateClip(nonMMDState.Name);

        var MMDLayer = AddLayer("Initial (MMD)", priority, true);
        var MMDState = AddState(MMDLayer, "Initial (MMD)", position: ExclusiveStatePosition);
        _MMDInitializationClip = MMDState.CreateClip(MMDState.Name);

        var animations = new List<BlendShapeWeightAnimation>();
        var mmdAnimations = new List<BlendShapeWeightAnimation>();

        foreach (var shape in _avatarContext.FaceRenderer.GetBlendShapeWeights(_avatarContext.FaceMesh).Where(b => !_avatarContext.TrackedBlendShapes.Contains(b.Name)))
        {
            if (IsMMDBlendShapeName(shape.Name))
            {
                mmdAnimations.Add(shape.ToBlendShapeAnimation());
            }
            else
            {
                animations.Add(shape.ToBlendShapeAnimation());
            }
        }

        _MMDInitializationClip.AddBlendShapeAnimations(_avatarContext.BodyPath, mmdAnimations);
        _nonMMDInitializationClip.AddBlendShapeAnimations(_avatarContext.BodyPath, animations);
    }

    private void AddBlendShapeInitialization(IEnumerable<BlendShapeWeight> blendShapes)
    {
        foreach (var shape in blendShapes)
        {
            if (IsMMDBlendShapeName(shape.Name))
            {
                _MMDInitializationClip.AddBlendShapeAnimation(_avatarContext.BodyPath, shape.ToBlendShapeAnimation());
            }
            else
            {
                _nonMMDInitializationClip.AddBlendShapeAnimation(_avatarContext.BodyPath, shape.ToBlendShapeAnimation());
            }
        }
    }

    private void InstallExpressionProgram(ExpressionProgram expressionProgram, int priority)
    {
        foreach (var item in expressionProgram.Items)
        {
            InstallExpressionItem(item, priority);
        }
    }

    private void InstallExpressionItem(ExpressionItem item, int priority)
    {
        var layer = AddLayer(item.Expression.Name, priority);
        var defaultState = AddState(layer, "PassThrough", ExclusiveStatePosition);
        AsPassThrough(defaultState);

        AddExpressionStates(
            layer,
            defaultState,
            item.Expression,
            item.ActiveWhen,
            ExclusiveStatePosition + new Vector3(0, 2 * PositionYStep, 0));
    }

    private void AddExpressionStates(
        VirtualLayer layer,
        VirtualState defaultState,
        AvatarExpression expression,
        DnfCondition when,
        Vector3 basePosition)
    {
        if (when.IsNever) return;

        var duration = _transitionDurationSeconds;
        var states = AddStatesForDnf(layer, defaultState, when, duration, basePosition);
        foreach (var state in states)
        {
            state.Name = expression.Name;
            AddExpressionToState(state, expression);
        }
    }

    private VirtualState[] AddStatesForDnf(VirtualLayer layer, VirtualState defaultState, DnfCondition when, float duration, Vector3 basePosition)
    {
        var states = new List<VirtualState>();
        var newEntryTransitions = new List<VirtualTransition>();
        var position = basePosition;

        var lockFacialCondition = new AnimatorCondition { parameter = FaceTuneConstants.LockFacialParameter, mode = AnimatorConditionMode.IfNot };

        foreach (var whenCase in when.Cases)
        {
            var state = AddState(layer, "unnamed", position);
            states.Add(state);
            position.y += PositionYStep;

            var entryTransition = VirtualTransition.Create();
            entryTransition.SetDestination(state);
            entryTransition.Conditions = ToAnimatorConditions(whenCase).ToImmutableList();
            newEntryTransitions.Add(entryTransition);

            var exitTransitions = new List<VirtualStateTransition>();
            foreach (var exitCase in when.Not().Cases)
            {
                var exitTransition = AnimatorHelper.CreateTransitionWithDurationSeconds(duration);
                exitTransition.SetExitDestination();
                exitTransition.Conditions = ToAnimatorConditions(exitCase).Append(lockFacialCondition).ToImmutableList();
                exitTransitions.Add(exitTransition);
            }
            state.Transitions = ImmutableList.CreateRange(state.Transitions.Concat(exitTransitions));
        }

        var exitTransitionsFromDefault = new List<VirtualStateTransition>();
        foreach (var entryTransition in newEntryTransitions)
        {
            var exitTransition = AnimatorHelper.CreateTransitionWithDurationSeconds(duration);
            exitTransition.SetExitDestination();
            exitTransition.Conditions = entryTransition.Conditions.Add(lockFacialCondition);
            exitTransitionsFromDefault.Add(exitTransition);
        }
        defaultState.Transitions = ImmutableList.CreateRange(defaultState.Transitions.Concat(exitTransitionsFromDefault));

        layer.StateMachine!.EntryTransitions = ImmutableList.CreateRange(layer.StateMachine!.EntryTransitions.Concat(newEntryTransitions));

        return states.ToArray();
    }
    
    private IEnumerable<AnimatorCondition> ToAnimatorConditions(DnfCase conditionCase)
    {
        return conditionCase.Rules.Select(ToAnimatorCondition);
    }

    private AnimatorCondition ToAnimatorCondition(DnfRule rule)
    {
        var animatorConditionRule = (AnimatorConditionRule)rule;
        _controller.EnsureParameterExists(animatorConditionRule.ParameterType, animatorConditionRule.ParameterName);
        return animatorConditionRule.Condition;
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
            clip.AddBlendShapeAnimations(_avatarContext.BodyPath, expression.AnimationSet);
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
