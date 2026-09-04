using System.Runtime.CompilerServices;

namespace Aoyon.FaceTune;

internal static partial class Utils
{
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

}

internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
{
    internal static readonly ReferenceEqualityComparer<T> Instance = new();

    private ReferenceEqualityComparer()
    {
    }

    public bool Equals(T x, T y)
    {
        return ReferenceEquals(x, y);
    }

    public int GetHashCode(T obj)
    {
        return RuntimeHelpers.GetHashCode(obj);
    }
}
