using Aoyon.FaceTune.Build;
using Aoyon.FaceTune.Platforms.VRChat;
using nadena.dev.ndmf;
using nadena.dev.modular_avatar.core;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace Aoyon.FaceTune.Platforms;

internal sealed class VRChatSupport : IMetabasePlatformSupport
{
    private const string GestureLeftParameter = "GestureLeft";
    private const string GestureRightParameter = "GestureRight";
    private const string GestureLeftWeightParameter = "GestureLeftWeight";
    private const string GestureRightWeightParameter = "GestureRightWeight";
    private const string VisemeParameter = "Viseme";
    private const string PreviewModeParameter = "PreviewMode";
    private const string TrackingTypeParameter = "TrackingType";
    private const string VRModeParameter = "VRMode";
    private const string AvatarVersionParameter = "AvatarVersion";
    private const string VrcEmoteParameter = "VRCEmote";

    private readonly VRCAvatarDescriptor _descriptor;

    public IPlatformBuildBackend BuildBackend => VRChatBuildBackend.Instance;

    [InitializeOnLoadMethod]
    private static void Register()
    {
        MetabasePlatformSupport.Register(
            WellKnownPlatforms.VRChatAvatar30,
            root => root.TryGetComponent<VRCAvatarDescriptor>(out var descriptor)
                ? new VRChatSupport(descriptor)
                : null);
    }

    internal VRChatSupport(VRCAvatarDescriptor descriptor)
    {
        _descriptor = descriptor;
    }

    public string ResolveGestureParameter(Hand hand)
        => hand == Hand.Left ? GestureLeftParameter : GestureRightParameter;

    public string ResolveGestureWeightParameter(Hand hand)
        => hand == Hand.Left ? GestureLeftWeightParameter : GestureRightWeightParameter;

    public SkinnedMeshRenderer? GetFaceRenderer()
    {
        if (_descriptor.lipSync == VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape
            && _descriptor.VisemeSkinnedMesh != null)
        {
            return _descriptor.VisemeSkinnedMesh;
        }

        if (_descriptor.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes
            && _descriptor.customEyeLookSettings.eyelidsSkinnedMesh != null)
        {
            return _descriptor.customEyeLookSettings.eyelidsSkinnedMesh;
        }

        return FindRenderer("Body", StringComparison.Ordinal)
               ?? FindRenderer("body", StringComparison.Ordinal)
               ?? FindRenderer("Face", StringComparison.OrdinalIgnoreCase);
    }

    private SkinnedMeshRenderer? FindRenderer(string name, StringComparison comparison)
    {
        var avatarRoot = _descriptor.transform;
        for (var i = 0; i < avatarRoot.childCount; i++)
        {
            var child = avatarRoot.GetChild(i);
            if (!string.Equals(child.name, name, comparison)) continue;
            if (child.TryGetComponent<SkinnedMeshRenderer>(out var renderer))
            {
                return renderer;
            }
        }

        return null;
    }

    public ParameterDomainRegistry CreateBuiltInParameterDomains()
    {
        return new ParameterDomainRegistry(
            new IntParameterDomain(0, 255),
            (PreviewModeParameter, new IntParameterDomain(0, 1)),
            (VisemeParameter, new IntParameterDomain(0, 14)),
            (GestureLeftParameter, new IntParameterDomain(0, 7)),
            (GestureRightParameter, new IntParameterDomain(0, 7)),
            (TrackingTypeParameter, new IntParameterDomain(0, 6)),
            (VRModeParameter, new IntParameterDomain(0, 1)),
            (AvatarVersionParameter, new IntParameterDomain(0, 3)),
            (VrcEmoteParameter, new IntParameterDomain(1, 16)));
    }

    public DnfCondition ResolveHandGestureCondition(
        HandGestureCondition condition,
        ParameterDomainRegistry parameterDomains)
    {
        var left = HandRule(GestureLeftParameter, true, condition.Gesture, parameterDomains);
        var right = HandRule(GestureRightParameter, true, condition.Gesture, parameterDomains);
        var matches = condition.Hand switch
        {
            HandGestureHand.Left => left,
            HandGestureHand.Right => right,
            HandGestureHand.Any => left.Or(right),
            HandGestureHand.Both => left.And(right),
            _ => throw new ArgumentOutOfRangeException(nameof(condition.Hand), condition.Hand, null)
        };
        return condition.Matches ? matches : matches.Complement();
    }

    public DnfCondition ResolveParameterCondition(
        ParameterCondition condition,
        ParameterDomainRegistry parameterDomains)
    {
        return DnfCondition.Single(
            AnimatorConditionRule.FromParameterCondition(condition),
            parameterDomains);
    }

    public IEnumerable<string> GetExternalEyeBlinkBlendShapeNames()
        => GetBlinkBlendShapes();

    public IEnumerable<string> GetExternalLipSyncBlendShapeNames()
        => GetLipSyncBlendShapes();

    public AnimatorController? GetAnimatorController()
    {
        return _descriptor.baseAnimationLayers
            .Where(layer => layer.type == VRCAvatarDescriptor.AnimLayerType.FX)
            .Select(layer => layer.animatorController)
            .OfType<AnimatorController>()
            .FirstOrDefault();
    }

    public void ImportAnimatorController(
        AvatarContext context,
        AnimatorController controller,
        GameObject parent)
    {
        new AnimatorControllerImporter(context, controller, this).Import(parent);
    }

    private static DnfCondition HandRule(
        string parameterName,
        bool equal,
        HandGesture handGesture,
        ParameterDomainRegistry parameterDomains)
    {
        return DnfCondition.Single(new AnimatorConditionRule(
            new AnimatorCondition
            {
                parameter = parameterName,
                mode = equal ? AnimatorConditionMode.Equals : AnimatorConditionMode.NotEqual,
                threshold = ToPlatformGestureValue(handGesture)
            },
            AnimatorControllerParameterType.Int), parameterDomains);
    }

    private static int ToPlatformGestureValue(HandGesture gesture)
    {
        return gesture switch
        {
            HandGesture.Neutral => 0,
            HandGesture.Fist => 1,
            HandGesture.HandOpen => 2,
            HandGesture.FingerPoint => 3,
            HandGesture.Victory => 4,
            HandGesture.RockNRoll => 5,
            HandGesture.HandGun => 6,
            HandGesture.ThumbsUp => 7,
            _ => throw new NotSupportedException($"Hand gesture '{gesture}' is not supported by VRChat.")
        };
    }

    private IEnumerable<string> GetBlinkBlendShapes()
    {
        var settings = _descriptor.customEyeLookSettings;
        var renderer = settings.eyelidsSkinnedMesh;
        if (settings.eyelidType != VRCAvatarDescriptor.EyelidType.Blendshapes
            || settings.eyelidsBlendshapes == null || renderer == null || renderer != GetFaceRenderer()
            || renderer.sharedMesh == null || settings.eyelidsBlendshapes.Length == 0)
        {
            return Array.Empty<string>();
        }

        return settings.eyelidsBlendshapes
            .Where(index => 0 <= index && index < renderer.sharedMesh.blendShapeCount)
            .Select(renderer.sharedMesh.GetBlendShapeName)
            .Distinct(StringComparer.Ordinal);
    }

    internal IEnumerable<string> GetLipSyncBlendShapes()
    {
        return _descriptor.lipSync == VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape
               && _descriptor.VisemeSkinnedMesh != null
               && _descriptor.VisemeSkinnedMesh == GetFaceRenderer()
               && _descriptor.VisemeBlendShapes != null
            ? _descriptor.VisemeBlendShapes.Where(name => !string.IsNullOrWhiteSpace(name))
            : Array.Empty<string>();
    }

    public IEnumerable<GameObject> GetMenuFolderObjects()
    {
        foreach (var group in _descriptor.GetComponentsInChildren<ModularAvatarMenuGroup>(true))
        {
            yield return group.targetObject != null ? group.targetObject : group.gameObject;
        }

        foreach (var item in _descriptor.GetComponentsInChildren<ModularAvatarMenuItem>(true))
        {
            if (item.PortableControl.Type != PortableControlType.SubMenu
                || item.MenuSource != SubmenuSource.Children) continue;
            yield return item.menuSource_otherObjectChildren != null
                ? item.menuSource_otherObjectChildren
                : item.gameObject;
        }
    }

    public void PostProcessDefaultBlendShapes(
        BuildSettings settings,
        AvatarControlSettings avatarControlSettings,
        BlendShapeWeightSet blendShapes)
    {
        blendShapes.AddRange(avatarControlSettings.MmdPlayback.BlendShapeNames
            .Where(name => !settings.IsBlendShapeExplicitlyExcluded(name))
            .Select(name => new BlendShapeWeight(name, 0f)));
    }

    public MmdPlaybackSettings ResolveMmdPlaybackSettings(MMDSupportSettings? settings, DnfCondition? disableWhen)
    {
        return settings == null
            ? MmdPlaybackSettings.Disabled
            : new MmdPlaybackSettings(
                true,
                ResolveMmdBlendShapeNames(settings).ToHashSet(),
                disableWhen,
                settings.SupportMode);
    }

    private static IEnumerable<string> ResolveMmdBlendShapeNames(
        MMDSupportSettings settings)
    {
        var explicitNames = settings.ExplicitBlendShapeNames
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
