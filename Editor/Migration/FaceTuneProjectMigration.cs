using UnityEditor.SceneManagement;

namespace Aoyon.FaceTune.Migration;

internal static class FaceTuneProjectMigration
{
    private static bool running;

    [InitializeOnLoadMethod]
    private static void SchedulePending()
    {
        if (FaceTuneProjectMigrationState.LastStartedVersion < FaceTuneProjectMigrationState.CurrentVersion)
            EditorApplication.delayCall += RunPending;
    }

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
            // Record the attempt before scanning assets so an interrupted scan is not repeated on startup.
            FaceTuneProjectMigrationState.MarkStarted(FaceTuneProjectMigrationState.CurrentVersion);
            V0ToV1.Run();
        }
        catch (Exception exception) { Debug.LogException(exception); }
        finally { running = false; }
    }
}
