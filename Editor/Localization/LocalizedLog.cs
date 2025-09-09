using nadena.dev.ndmf;

namespace Aoyon.FaceTune;

internal static class LocalizedLog
{
    public static void Info(string key, string? details = null, params object[] args)
    {
        ErrorReport.ReportError(Localization.NdmfLocalizer, ErrorSeverity.Information, key, args);
        if (details != null) Debug.Log(details);
    }

    public static void Warning(string key, string? details = null, params object[] args)
    {
        ErrorReport.ReportError(Localization.NdmfLocalizer, ErrorSeverity.NonFatal, key, args);
        if (details != null) Debug.LogWarning(details);
    }

    public static void Error(string key, string? details = null, params object[] args)
    {
        ErrorReport.ReportError(Localization.NdmfLocalizer, ErrorSeverity.Error, key, args);
        if (details != null) Debug.LogError(details);
    }
}