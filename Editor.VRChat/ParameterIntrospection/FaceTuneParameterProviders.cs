using Aoyon.FaceTune.Build;
using nadena.dev.ndmf;
using nadena.dev.ndmf.runtime;

namespace Aoyon.FaceTune;

[ParameterProviderFor(typeof(MenuComponent))]
internal sealed class MenuParameterProvider : IParameterProvider
{
    private readonly MenuComponent _component;

    public MenuParameterProvider(MenuComponent component)
    {
        _component = component;
    }

    public IEnumerable<ProvidedParameter> GetSuppliedParameters(BuildContext? context = null)
        => FaceTuneParameterProvider.Resolve(_component, context);
}

[ParameterProviderFor(typeof(ExpressionComponent))]
internal sealed class ExpressionParameterProvider : IParameterProvider
{
    private readonly ExpressionComponent _component;

    public ExpressionParameterProvider(ExpressionComponent component)
    {
        _component = component;
    }

    public IEnumerable<ProvidedParameter> GetSuppliedParameters(BuildContext? context = null)
        => FaceTuneParameterProvider.Resolve(_component, context);
}

[ParameterProviderFor(typeof(SettingsComponent))]
internal sealed class SettingsParameterProvider : IParameterProvider
{
    private readonly SettingsComponent _component;

    public SettingsParameterProvider(SettingsComponent component)
    {
        _component = component;
    }

    public IEnumerable<ProvidedParameter> GetSuppliedParameters(BuildContext? context = null)
        => FaceTuneParameterProvider.Resolve(_component, context);
}

internal static class FaceTuneParameterProvider
{
    public static IEnumerable<ProvidedParameter> Resolve(
        Component component,
        BuildContext? context)
    {
        var root = context?.AvatarRootObject
                   ?? RuntimeUtil.FindAvatarInParents(component.transform)?.gameObject;
        if (root == null) yield break;

        foreach (var declaration in ParameterResolver.ResolveParameters(root))
        {
            if (declaration.Source != component) continue;
            yield return new ProvidedParameter(
                declaration.Name,
                ParameterNamespace.Animator,
                component,
                PluginDefinition.Instance,
                ToAnimatorType(declaration.Type))
            {
                IsAnimatorOnly = false,
                IsHidden = true,
                WantSynced = declaration.Synced,
                DefaultValue = declaration.DefaultValue
            };
        }
    }

    private static AnimatorControllerParameterType ToAnimatorType(ParameterValueType type)
        => type switch
        {
            ParameterValueType.Bool => AnimatorControllerParameterType.Bool,
            ParameterValueType.Int => AnimatorControllerParameterType.Int,
            ParameterValueType.Float => AnimatorControllerParameterType.Float,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
}
