using nadena.dev.modular_avatar.core;
using Aoyon.FaceTune.Build;
using nadena.dev.ndmf;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal static class VRChatParameterBuilder
{
    private const string GeneratedParameterRootName = FaceTuneConstants.Name + " Generated Parameter";

    public static void Build(BuildContext context, ParameterPlan plan)
    {
        if (plan.Items.Count == 0) return;

        var generatedRoot = new GameObject(GeneratedParameterRootName);
        generatedRoot.transform.SetParent(context.AvatarRootTransform, false);

        var parameters = generatedRoot.AddComponent<ModularAvatarParameters>();
        foreach (var parameter in plan.Items)
        {
            parameters.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = parameter.Name,
                syncType = !parameter.Synced
                    ? ParameterSyncType.NotSynced
                    : parameter.Type switch
                    {
                        ParameterValueType.Bool => ParameterSyncType.Bool,
                        ParameterValueType.Int => ParameterSyncType.Int,
                        ParameterValueType.Float => ParameterSyncType.Float,
                        _ => ParameterSyncType.NotSynced
                    },
                saved = parameter.Saved,
                defaultValue = parameter.DefaultValue,
                hasExplicitDefaultValue = true
            });
        }
    }
}
