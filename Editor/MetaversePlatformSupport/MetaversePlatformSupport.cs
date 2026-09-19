using nadena.dev.ndmf;

namespace Aoyon.FaceTune.Platforms;

internal static class MetaversePlatformSupport
{
    internal delegate IMetaversePlatformSupport? Factory(Transform root);

    private static readonly Dictionary<string, Factory> s_factories = new();

    public static void Register(string platformId, Factory factory)
    {
        s_factories[platformId] = factory;
    }

    public static IMetaversePlatformSupport GetForBuild(BuildContext context)
    {
        if (s_factories.TryGetValue(context.PlatformProvider.QualifiedName, out var factory))
        {
            var support = factory(context.AvatarRootTransform);
            if (support != null) return support;
        }

        return new FallbackSupport(context.AvatarRootTransform);
    }

    public static IReadOnlyList<IMetaversePlatformSupport> GetForAvatar(Transform root)
    {
        return s_factories.Values
            .Select(factory => factory(root))
            .OfType<IMetaversePlatformSupport>()
            .ToArray();
    }
}
