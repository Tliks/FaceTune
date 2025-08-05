using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune;

internal static class NDMFUtility
{
    public static bool TryGetComponent(this ComputeContext ctx, GameObject obj, Type type, [NotNullWhen(true)] out Component? component)
    {
        if (obj == null) { component = null; return false; }
        var c = ctx.GetComponent(obj, type);
        if (c == null) { component = null; return false; }
        component = c; return true;
    }
    public static bool TryGetComponent<T>(this ComputeContext ctx, GameObject obj, [NotNullWhen(true)] out T? component)
    where T : class
    {
        if (obj == null) { component = null; return false; }
        var c = ctx.GetComponent<T>(obj);
        if (c == null) { component = null; return false; }
        component = c; return true;
    }
}