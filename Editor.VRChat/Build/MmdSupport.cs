using Aoyon.FaceTune.Build;
using Aoyon.FaceTune.Platforms.VRChat;
using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;

namespace Aoyon.FaceTune.Platforms;

internal sealed class MmdSupport
{
    private readonly MmdPlaybackSettings _settings;

    public DnfCondition PlaybackWhen { get; }
    public bool DisableFxLayer { get; }

    public MmdSupport(MmdPlaybackSettings settings)
    {
        using var _ = new Utils.ProfilingSampleScope("Animator.InitializeMmdSupport");
        _settings = settings;
        PlaybackWhen = settings.PlaybackWhen;
        if (PlaybackWhen.IsNever) return;

        var mode = settings.DisableMode == MMDSupportSettings.Mode.Auto
            ? MMDSupportSettings.Mode.DisableFXlayer // 解析が膨大なので一旦FX無効化にfallback
            : settings.DisableMode;
        DisableFxLayer = mode switch
        {
            MMDSupportSettings.Mode.DisableLayers => false,
            MMDSupportSettings.Mode.DisableFXlayer => true,
            _ => throw new ArgumentOutOfRangeException(
                nameof(settings.DisableMode), settings.DisableMode, null)
        };
    }

    public void AddInitialMmdState(
        AnimatorGraph graph,
        VirtualLayer layer,
        VirtualState defaultState,
        DnfCondition playbackWhen,
        IReadOnlyList<BlendShapeWeight> blendShapes,
        Vector3 position,
        string bodyPath)
    {
        if (playbackWhen.IsNever) return;

        var root = layer.StateMachine!;
        var machine = graph.AddStateMachine(root, "MMD", position);

        if (DisableFxLayer)
            BuildFxPlayback(graph, machine, playbackWhen);
        else
            BuildLayerPlayback(graph, machine, playbackWhen, blendShapes, bodyPath);

        graph.AddEntryTransition(layer, machine, playbackWhen);
        graph.AddStateMachineExitTransition(root, machine);
        graph.AddExitTransitions(defaultState, playbackWhen, 0f);
    }

    private void BuildLayerPlayback(
        AnimatorGraph graph,
        VirtualStateMachine machine,
        DnfCondition playbackWhen,
        IReadOnlyList<BlendShapeWeight> blendShapes,
        string bodyPath)
    {
        var initial = graph.AddState(machine, "Initial", new Vector3(300, 0, 0));
        var initialClip = initial.SetNewClip("MMD Initial");
        initialClip.AddBlendShapeAnimations(bodyPath, blendShapes.ToBlendShapeAnimations());
        initialClip.SetAap(AapProtocol.ExpressionInactiveName, 1f, 1f);
        machine.DefaultState = initial;

        var mmdNames = ResolveMmdBlendShapeNames(_settings).ToHashSet(StringComparer.Ordinal);
        var nonMmdShapes = blendShapes.Where(shape => !mmdNames.Contains(shape.Name));

        var playback = graph.AddState(machine, "Playback", new Vector3(550, 0, 0));
        var clip = playback.SetNewClip("MMD Playback");
        clip.AddBlendShapeAnimations(bodyPath, nonMmdShapes.ToBlendShapeAnimations());
        clip.SetAap(AapProtocol.ExpressionInactiveName, 1f);

        graph.AddExitTransitions(initial, playbackWhen.Complement(), 0f);
        graph.AddExitTimeTransition(initial, playback);
        graph.AddExitTransitions(playback, playbackWhen.Complement(), 0f);
    }

    private static void BuildFxPlayback(
        AnimatorGraph graph,
        VirtualStateMachine machine,
        DnfCondition playbackWhen)
    {
        var playback = graph.AddState(machine, "Playback", new Vector3(300, 0, 0));
        graph.AsPassThrough(playback);
        SetFxPlayableWeight(playback, 0f);
        machine.DefaultState = playback;

        var restore = graph.AddState(machine, "Restore FX", new Vector3(550, 0, 0));
        graph.AsPassThrough(restore);
        SetFxPlayableWeight(restore, 1f);

        graph.AddStateTransition(playback, restore, playbackWhen.Complement(), 0f);
        graph.AddExitTransitions(restore, DnfCondition.Always, 0f);
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
