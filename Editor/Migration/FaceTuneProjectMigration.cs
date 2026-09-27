using UnityEditor.SceneManagement;

namespace Aoyon.FaceTune.Migration;

internal static class FaceTuneProjectMigration
{
    private static bool running;

    internal static void RunPending()
    {
        if (running || EditorApplication.isPlayingOrWillChangePlaymode) return;
        running = true;
        try
        {
            if (FaceTuneProjectMigrationState.LastStartedVersion >= FaceTuneProjectMigrationState.CurrentVersion)
            {
                Debug.Log("FaceTune migration has already been started for this project.");
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunPending;
                return;
            }
            if (!EditorSceneManager.SaveOpenScenes())
            {
                Debug.LogWarning("FaceTune migration was not started because open scenes could not be saved.");
                return;
            }
            // The version records an attempted project migration, not a Component schema.
            // Advance before writing assets; interrupted runs must not apply twice.
            if (FaceTuneProjectMigrationState.LastStartedVersion < 1)
                V0ToV1.Run(() => FaceTuneProjectMigrationState.MarkStarted(FaceTuneProjectMigrationState.CurrentVersion));
        }
        catch (Exception exception) { Debug.LogException(exception); }
        finally { running = false; }
    }
}
