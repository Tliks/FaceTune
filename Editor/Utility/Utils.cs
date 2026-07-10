using Aoyon.FaceTune.Build;
using nadena.dev.ndmf.runtime;

namespace Aoyon.FaceTune;

internal static partial class Utils
{
    public static Transform? FindAvatarInParents(Transform transform)
    {
        return RuntimeUtil.FindAvatarInParents(transform); // NDMFが対応する範囲が上限
    }

    internal class ProfilingSampleScope : IDisposable
    {
        public ProfilingSampleScope(string name)
        {
            Profiler.BeginSample(name);
        }

        void IDisposable.Dispose()
        {
            Profiler.EndSample();
        }
    }

    public static Texture2D? RenderExpressionThumbnail(FaceTuneContext context, ExpressionItem expression)
    {
        // TODO: Render an expression thumbnail from the final avatar state.
        return null;
    }
}

