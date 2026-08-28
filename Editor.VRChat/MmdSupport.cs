using Aoyon.FaceTune.Build;
using Aoyon.FaceTune.Platforms.VRChat;
using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;

namespace Aoyon.FaceTune.Platforms;

internal sealed class MmdSupport
{
    private readonly AnimatorGraph _graph;
    private readonly MmdPlaybackSettings _settings;

    public DnfCondition? PlaybackWhen { get; }
    public DnfCondition? LayerPlaybackWhen { get; }
    public bool DisableFxLayer { get; }

    public MmdSupport(
        GameObject root,
        AnimatorGraph graph,
        MmdPlaybackSettings settings,
        IMetabasePlatformSupport platformSupport,
        ParameterDomainRegistry parameterDomains,
        bool? analyzedWriteDefaults)
    {
        _graph = graph;
        _settings = settings;

        var disableWhen = new ConditionResolver(root, platformSupport, parameterDomains)
            .Resolve(settings.Condition);
        var playbackWhen = settings.Enabled
            ? disableWhen ?? DnfCondition.Always
            : null;
        var disableLayers = false;
        var disableFxLayer = false;
        if (settings.Enabled && settings.Condition != null)
        {
            var mode = settings.DisableMode == MMDSupportSettings.Mode.Auto
                ? MMDSupportSettings.Mode.DisableFXlayer // 解析が膨大なので一旦FX無効化にfallback
                : settings.DisableMode;
            switch (mode)
            {
                case MMDSupportSettings.Mode.DisableLayers:
                    disableLayers = true;
                    break;
                case MMDSupportSettings.Mode.DisableFXlayer:
                    disableFxLayer = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(settings.DisableMode),
                        settings.DisableMode,
                        null);
            }
        }

        PlaybackWhen = playbackWhen;
        LayerPlaybackWhen = disableLayers ? playbackWhen : null;
        DisableFxLayer = disableFxLayer;
    }

    public VirtualState? AddPassThroughState(VirtualLayer layer, Vector3 position)
    {
        if (LayerPlaybackWhen is not { IsNever: false } playbackWhen) return null;

        var state = _graph.AddState(layer, "MMD Playback", position);
        _graph.AsPassThrough(state);
        _graph.SetAnyStateTransition(layer, state, playbackWhen, 0f);
        _graph.SetExitTransitions(state, playbackWhen.Complement(), 0f);
        return state;
    }

    public void AddInitialMmdState(
        VirtualLayer layer,
        VirtualState defaultState,
        IEnumerable<BlendShapeWeight> blendShapes,
        Vector3 position,
        string bodyPath)
    {
        _graph.SetExitTransitions(
            defaultState,
            PlaybackWhen ?? DnfCondition.Never,
            0f);
        if (PlaybackWhen is not { IsNever: false } playbackWhen) return;

        var state = _graph.AddState(layer, "MMD Playback", position);
        var mmdBlendShapes = blendShapes
            .Where(shape => !ResolveMmdBlendShapeNames(_settings).Contains(shape.Name));
        state.SetNewClip("MMD Playback").AddBlendShapeAnimations(
            bodyPath,
            mmdBlendShapes.ToBlendShapeAnimations());
        _graph.AddEntryTransition(layer, state, playbackWhen);
        _graph.SetExitTransitions(state, playbackWhen.Complement(), 0f);

        if (DisableFxLayer)
        {
            SetFxPlayableWeight(defaultState, 1f);
            SetFxPlayableWeight(state, 0f);
        }
    }

    private static void SetFxPlayableWeight(VirtualState state, float weight)
    {
        var control = state.EnsureBehavior<VRCPlayableLayerControl>();
        control.layer = VRCPlayableLayerControl.BlendableLayer.FX;
        control.goalWeight = weight;
        control.blendDuration = 0f;
    }

    public static void PostProcessDefaultBlendShapes(
        BuildSettings settings,
        AvatarControlSettings avatarControlSettings,
        BlendShapeWeightSet blendShapes)
    {
        blendShapes.AddRange(ResolveMmdBlendShapeNames(avatarControlSettings.MmdPlayback)
            .Where(name => !settings.IsBlendShapeExplicitlyExcluded(name))
            .Select(name => new BlendShapeWeight(name, 0f)));
    }

    private static IEnumerable<string> ResolveMmdBlendShapeNames(
        MmdPlaybackSettings settings)
    {
        if (!settings.Enabled) return Array.Empty<string>();

        var explicitNames = settings.ExplicitBlendShapeNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal);
        if (explicitNames.Count > 0) return explicitNames;

        return MmdBlendShapeNames;
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
