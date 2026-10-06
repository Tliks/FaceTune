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
            // 途中で失敗しても同じ移行を再実行しないよう、走査前に試行を記録する。
            FaceTuneProjectMigrationState.MarkStarted(FaceTuneProjectMigrationState.CurrentVersion);
            V0ToV1.Run();
        }
        catch (Exception exception) { Debug.LogException(exception); }
        finally { running = false; }
    }
}
