using Aoyon.FaceTune.Build;
using nadena.dev.ndmf.animator;
using VRC.SDK3.Avatars.Components;

namespace Aoyon.FaceTune.Platforms.VRChat;

// 優先度: Additive < Gesture < Station Action(MMD) < Action(AFK) < FX

// AdditiveがWD OFFのとき、ブレンドシェイプ3倍バグを引き起こす
// (なお、Gesture/Action/FXのいずれかでWD OFF Stateが有効だと保存値で上書きされるので回避される)

// 通常はFXの初期化レイヤーで常に初期値を書くことでこれを回避している
// 一方で、MMDやAFKが有効な際には、Station/ACtionをパススルーするために、FXの初期値再生を停止するので、3倍バグを引き起こす
// そのため、Gestureレイヤーで初期値を常に再生することで、3倍バグを回避する
internal static class VRChatGestureDefaultsBuilder
{
    private const int MinimumExistingLayerCount = 3;
    private const int LayerPriority = int.MaxValue - 1;

    public static void Build(FaceTuneContext context)
    {
        // MMDやAFK対応がないなら、常にFXが初期再生するので3倍バグは起きない。
        var controls = context.RequireAvatarControlSettings();
        if (!controls.SupportAfk && !controls.MmdPlayback.Enabled) return;

        var settings = context.RequireSettings();
        var blendShapes = context.BuildContext
            .GetState<VRChatInitialBlendShapeState>().BlendShapes;
        if (blendShapes.Count == 0) return;

        var controllerContext = context.BuildContext.Extension<VirtualControllerContext>();

        if (controllerContext.Controllers.TryGetValue(VRCAvatarDescriptor.AnimLayerType.Additive, out var additive))
        {
            if (AnimatorHelper.AnalyzeLayerWriteDefaults(additive) == true)
            {
                // AdditiveがWD ONで統一されてるなら3倍バグの根本原因がない
                return;
            }
        }

        if (!controllerContext.Controllers.TryGetValue(VRCAvatarDescriptor.AnimLayerType.Gesture, out var gesture))
        {
            Debug.LogWarning("FaceTune: Gesture controller was not found; facial defaults were not added.");
            return;
        }

        var graph = new AnimatorGraph(
            AnimatorHelper.AnalyzeLayerWriteDefaults(gesture) ?? true,
            controllerContext.CloneContext);
        
        // 一部のMMDワールドは、FX同様Gestureの1,2番目のレイヤーをWeight 0にする(0はweight 1固定)
        // これを回避するために、ダミーレイヤーを置き、初期値再生を3番目以降に配置する
        var paddingCount = Math.Max(0, MinimumExistingLayerCount - gesture.Layers.Count());
        for (var index = 0; index < paddingCount; index++)
        {
            var dummy = graph.AddLayer(gesture, "Dummy", LayerPriority);
            var idle = graph.AddState(dummy, "Empty", Vector3.zero);
            graph.AsPassThrough(idle);
            dummy.StateMachine!.DefaultState = idle;
        }

        var layer = graph.AddLayer(gesture, "Initial", LayerPriority);
        var state = graph.AddState(layer, "Initial", Vector3.zero);
        layer.StateMachine!.DefaultState = state;
        state.SetNewClip("Initial").AddBlendShapeAnimations(
            settings.AvatarContext.BodyPath,
            blendShapes.ToBlendShapeAnimations());
    }
}
