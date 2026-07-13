using nadena.dev.modular_avatar.core;
using Aoyon.FaceTune.Build;
using nadena.dev.ndmf;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal static class VRChatParameterBuilder
{
    private const string GeneratedParameterRootName = FaceTuneConstants.Name + " Generated Parameter";

    public static void Emit(BuildContext context, MenuProgram program)
    {
        if (program.Parameters.Count == 0) return;

        var generatedRoot = new GameObject(GeneratedParameterRootName);
        generatedRoot.transform.SetParent(context.AvatarRootTransform, false);

        var parameters = generatedRoot.AddComponent<ModularAvatarParameters>();
        foreach (var parameter in program.Parameters)
        {
            parameters.parameters.Add(new ParameterConfig
            {
                nameOrPrefix = parameter.Name,
                syncType = parameter.Type switch
                {
                    MenuParameterType.Bool => ParameterSyncType.Bool,
                    MenuParameterType.Int => ParameterSyncType.Int,
                    MenuParameterType.Float => ParameterSyncType.Float,
                    _ => ParameterSyncType.NotSynced
                },
                saved = parameter.Saved,
                defaultValue = parameter.DefaultValue,
                hasExplicitDefaultValue = true
            });
        }
    }
}
