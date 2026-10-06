namespace Aoyon.FaceTune.Migration;

[FilePath($"ProjectSettings/Packages/{FaceTuneConstants.QualifiedName}/migration.json", FilePathAttribute.Location.ProjectFolder)]
internal sealed class FaceTuneProjectMigrationState : ScriptableSingleton<FaceTuneProjectMigrationState>
{
    internal const int CurrentVersion = 1;

    [SerializeField] private int lastStartedVersion;

    internal static int LastStartedVersion => instance.lastStartedVersion;

    internal static void MarkStarted(int version)
    {
        instance.lastStartedVersion = version;
        instance.Save(true);
    }
}
