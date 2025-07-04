namespace aoyon.facetune.ui;

internal class FacialAnimationClipEditor : FacialShapesEditor
{
    private GameObject? _root;
    private AnimationClip? _clip = null!;

    public static FacialAnimationClipEditor OpenEditor(SkinnedMeshRenderer renderer, Mesh mesh, HashSet<string> allKeys, BlendShapeSet defaultOverrides, AnimationClip clip)
    {
        var window = GetWindow<FacialAnimationClipEditor>();
        window.Init(renderer, mesh, allKeys, defaultOverrides);
        window._clip = clip;
        return window;
    }

    public override void OnGUI()
    {
        base.OnGUI();

        _root = EditorGUILayout.ObjectField("Root", _root, typeof(GameObject), true) as GameObject;
        _clip = EditorGUILayout.ObjectField("Clip", _clip, typeof(AnimationClip), false) as AnimationClip;

        GUI.enabled = _root != null && _clip != null;
        if (GUILayout.Button("Save"))
        {
            var result = new BlendShapeSet();
            GetResult(result);
            SaveClip(_root!, _clip!, Renderer, result.BlendShapes);
            ForceClose();
        }
        GUI.enabled = true;
    }

    private static void SaveClip(GameObject root, AnimationClip clip, SkinnedMeshRenderer renderer, IEnumerable<BlendShape> blendShapes)
    {
        clip.ClearCurves(); // 消す以外のオプション
        var relativePath = HierarchyUtility.GetRelativePath(root, renderer.gameObject) ?? throw new Exception("Failed to get relative path");
        clip.SetBlendShapes(relativePath, blendShapes.Where(s => s.Weight > 0.0f));
    }
}