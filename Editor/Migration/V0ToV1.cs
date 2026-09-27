using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Aoyon.FaceTune.Migration;

internal static class V0ToV1
{
    internal static void Run()
    {
        var errors = new List<string>();
        var sceneCount = 0;
        var prefabCount = 0;

        // Scene instances must read their inherited values before the source prefabs change.
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = SceneManager.GetSceneByPath(path);
            var opened = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                var changes = V0ToV1ComponentConverter.Prepare(scene.GetRootGameObjects());
                if (changes.Count == 0) continue;
                foreach (var apply in changes) apply();
                if (EditorSceneManager.SaveScene(scene)) sceneCount++;
                else errors.Add($"{path}: saving scene failed");
            }
            catch (Exception exception) { errors.Add($"{path}: {exception}"); }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        var paths = new HashSet<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponentInChildren<FaceTuneTagComponent>(true) == null)
                    continue;
            }
            catch (Exception exception) { errors.Add($"{path}: checking prefab failed: {exception}"); }
            paths.Add(path);
        }
        var visited = new HashSet<string>();
        var dependenciesFirst = new List<string>();
        void Visit(string path)
        {
            if (!visited.Add(path)) return;
            foreach (var dependency in AssetDatabase.GetDependencies(path, false))
                if (paths.Contains(dependency)) Visit(dependency);
            dependenciesFirst.Add(path);
        }
        foreach (var path in paths) Visit(path);
        dependenciesFirst.Reverse();
        foreach (var path in dependenciesFirst)
        {
            GameObject? root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                var changes = V0ToV1ComponentConverter.Prepare(new[] { root });
                if (changes.Count == 0) continue;
                foreach (var apply in changes) apply();
                if (PrefabUtility.SaveAsPrefabAsset(root, path) != null) prefabCount++;
                else errors.Add($"{path}: saving prefab failed");
            }
            catch (Exception exception) { errors.Add($"{path}: {exception}"); }
            finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
        }

        if (errors.Count > 0)
            Debug.LogError($"FaceTune V0 to V1 migration could not convert some assets. No automatic rerun will occur.\n{string.Join("\n", errors)}");
        else
            Debug.Log($"FaceTune V0 to V1 migration finished: {sceneCount} scenes, {prefabCount} prefabs.");
    }
}
