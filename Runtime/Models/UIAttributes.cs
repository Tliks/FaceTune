namespace Aoyon.FaceTune;

[AttributeUsage(AttributeTargets.Field)]
internal sealed class ToggleLeftAttribute : PropertyAttribute
{
}

[AttributeUsage(AttributeTargets.Field)]
internal sealed class MenuNameAttribute : PropertyAttribute
{
}

[AttributeUsage(AttributeTargets.Field)]
internal sealed class MenuInstallContainerAttribute : PropertyAttribute
{
}

[AttributeUsage(AttributeTargets.Field)]
internal sealed class ExclusiveToggleMenuGroupAttribute : PropertyAttribute
{
}