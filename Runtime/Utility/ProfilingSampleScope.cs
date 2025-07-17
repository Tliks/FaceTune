namespace aoyon.facetune;

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