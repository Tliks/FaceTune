namespace Aoyon.FaceTune.Build;

internal class CollectBuildSettingsPass : FaceTunePass<CollectBuildSettingsPass>
{
    public override string QualifiedName => $"{FaceTuneConstants.QualifiedName}.collect-build-settings";
    public override string DisplayName => "Collect Build Settings";

    protected override void Execute(FaceTuneContext context)
    {
        context.SetSettings(Collect(context));
    }

    private static BuildSettings Collect(FaceTuneContext context)
    {
        var root = context.AvatarContext.Root;
        
        var settingsComponents = root.GetComponentsInChildren<SettingsComponent>(true);
        if (settingsComponents.Length > 1)
        {
            LocalizedLog.Warning("Log:warning:AvatarContext:MultipleSettingsComponent", null, settingsComponents);
        }
        var avatarSettings = settingsComponents.FirstOrDefault()?.Settings ?? AvatarSettings.Default;

        var excludedBlendShapeNames = context.PlatformSupport.GetExternallyControlledBlendShapeNames().ToHashSet();
        excludedBlendShapeNames.UnionWith(avatarSettings.ExcludedBlendShapeNames.Where(x => !string.IsNullOrWhiteSpace(x)));

        var mmdSupportComponents = root.GetComponentsInChildren<MMDSupportComponent>(true);
        if (mmdSupportComponents.Length > 1)
        {
            LocalizedLog.Warning("Log:warning:CollectBuildSettingsPass:MultipleMMDSupportComponent", null, mmdSupportComponents);
        }

        var mmdSupport = mmdSupportComponents.FirstOrDefault();
        var mmdPlayback = MmdPlaybackSettings.Disabled;
        if (mmdSupport != null)
        {
            excludedBlendShapeNames.UnionWith(ResolveMmdBlendShapeNames(mmdSupport.Settings));
            mmdPlayback = new MmdPlaybackSettings(
                true,
                mmdSupport.Settings.DisableParameterName,
                mmdSupport.Settings.DisableMode);
        }

        var disableEyeBlinkComponents = root.GetComponentsInChildren<DisableEyeBlinkComponent>(true);
        if (disableEyeBlinkComponents.Length > 1)
        {
            LocalizedLog.Warning("Log:warning:CollectBuildSettingsPass:MultipleDisableEyeBlinkComponent", null, disableEyeBlinkComponents);
        }
        var disableEyeBlinkParameter = disableEyeBlinkComponents.FirstOrDefault()?.DisableParameterName ?? string.Empty;

        var disableLipSyncComponents = root.GetComponentsInChildren<DisableLipSyncComponent>(true);
        if (disableLipSyncComponents.Length > 1)
        {
            LocalizedLog.Warning("Log:warning:CollectBuildSettingsPass:MultipleDisableLipSyncComponent", null, disableLipSyncComponents);
        }
        var disableLipSyncParameter = disableLipSyncComponents.FirstOrDefault()?.DisableParameterName ?? string.Empty;

        var lockFacialComponents = root.GetComponentsInChildren<LockFacialComponent>(true);
        if (lockFacialComponents.Length > 1)
        {
            LocalizedLog.Warning("Log:warning:CollectBuildSettingsPass:MultipleLockFacialComponent", null, lockFacialComponents);
        }
        var lockFacialParameter = lockFacialComponents.FirstOrDefault()?.ConditionParameterName ?? string.Empty;

        return new BuildSettings(
            context.AvatarContext,
            context.PlatformSupport,
            excludedBlendShapeNames.ToImmutableHashSet(),
            avatarSettings.DurationSeconds,
            avatarSettings.ParmaterCompression,
            avatarSettings.SupressTrackingControl,
            context.PlatformSupport.CreateBuiltInParameterDomains(),
            mmdPlayback,
            disableEyeBlinkParameter,
            disableLipSyncParameter,
            lockFacialParameter);
    }

    private static IEnumerable<string> ResolveMmdBlendShapeNames(
        MmdSupportSettings settings)
    {
        var explicitNames = settings.ExplicitMmdBlendShapeNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();
        if (explicitNames.Length > 0) return explicitNames;

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
