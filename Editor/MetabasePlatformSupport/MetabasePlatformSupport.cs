using nadena.dev.ndmf;

namespace Aoyon.FaceTune.Platforms;

internal static class MetabasePlatformSupport
{
    internal delegate IMetabasePlatformSupport? Factory(Transform root);

    private static readonly Dictionary<string, Factory> s_factories = new();

    public static void Register(string platformId, Factory factory)
    {
        s_factories[platformId] = factory;
    }

    public static IMetabasePlatformSupport GetSupport(BuildContext context)
    {
        if (s_factories.TryGetValue(context.PlatformProvider.QualifiedName, out var factory))
        {
            var support = factory(context.AvatarRootTransform);
            if (support != null) return support;
        }

        return new FallbackSupport(context.AvatarRootTransform);
    }

    public static IMetabasePlatformSupport GetSupport(Transform root)
    {
        foreach (var factory in s_factories.Values)
        {
            var support = factory(root);
            if (support != null) return support;
        }

        return new FallbackSupport(root);
    }
}
