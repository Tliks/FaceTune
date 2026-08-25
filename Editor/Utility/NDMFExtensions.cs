using nadena.dev.ndmf;
using nadena.dev.ndmf.preview;

namespace Aoyon.FaceTune;

internal static class NDMFExtensions
{
    public static bool EditorOnlyInHierarchy(this ComputeContext ctx, GameObject obj)
    {
        foreach (var node in ctx.ObservePath(obj.transform))
        {
            var result = ctx.Observe(node.gameObject, go => go.CompareTag("EditorOnly"), (a, b) => a == b);
            if (result) return true;
        }
        return false;
    }
}