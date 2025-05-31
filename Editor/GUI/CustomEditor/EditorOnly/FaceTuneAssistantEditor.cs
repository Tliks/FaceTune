namespace com.aoyon.facetune.ui
{
    public enum PatternType
    {
        LeftOnly,
        RightOnly,
        Basic,
        Blending,
        FaceMorphFirst,
        HandSign
    }

    internal struct PatternInfo
    {
        public string Description { get; }
        public string Guid { get; }

        public PatternInfo(string description, string guid)
        {
            Description = description;
            Guid = guid;
        }
    }

    [CustomEditor(typeof(FaceTuneAssistantComponent))]
    internal class FaceTuneAssistantEditor : FaceTuneCustomEditorBase<FaceTuneAssistantComponent>
    {
        internal const string LeftOnlyPatternGuid = "8321eef2d75950543bc39fcdc9709128";
        internal const string RightOnlyPatternGuid = "82eca51c1b5f4374da399ddf321509cb";
        internal const string BasicPatternGuid = "c259edc6efd4aaa4bba3b1636557cc3b";
        internal const string BlendingPatternGuid = "9eb5bf9eeb8dc81488fb9453d21f3510";
        internal const string FaceMorphFirstPatternGuid = "618bf06062904004f99355468c34ac7c";
        internal const string HandSignPatternGuid = "e7a261d8cf051454ea0c41e427463276";

        private static readonly Dictionary<PatternType, PatternInfo> _patternDetails = new()
        {
            { PatternType.LeftOnly, new PatternInfo("左手のみのパターンです。", LeftOnlyPatternGuid) },
            { PatternType.RightOnly, new PatternInfo("右手のみのパターンです。", RightOnlyPatternGuid) },
            { PatternType.Basic, new PatternInfo("片手が優先される最も基本的なパターンです。", BasicPatternGuid) },
            { PatternType.Blending, new PatternInfo("片手ごとのアニメーションがブレンドさせるパターンです。目と口の制御をそれぞれの手に割り当てる際などに便利です。", BlendingPatternGuid) },
            { PatternType.FaceMorphFirst, new PatternInfo("右手と左手に優先度を付けず、最初に実行したジェスチャーを優先させるパターンです。", FaceMorphFirstPatternGuid) },
            { PatternType.HandSign, new PatternInfo("左手と右手の組み合わせで最大64通りのジェスチャーを作成できるパターンです。", HandSignPatternGuid) },
        };

        private PatternType _selectedPattern = PatternType.Basic;

        private PresetComponent[] _presets = Array.Empty<PresetComponent>();
        private ConditionComponent[] _conditions = Array.Empty<ConditionComponent>();
        public override void OnEnable()
        {
            base.OnEnable();
            _presets = Component.transform.GetComponentsInChildren<PresetComponent>();
            _conditions = Component.transform.GetComponentsInChildren<ConditionComponent>();
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Pattern, ConditionはHierarhyで下にあるほど優先されます。", EditorStyles.boldLabel);

            // --- サジェスチョン表示エリアここから ---
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Suggestion", EditorStyles.boldLabel);

            SuggestCreatePattern();
            // SuggestAsPreset();

            // --- サジェスチョン表示エリアここまで ---

            EditorGUILayout.Space();
            CreatePatternGUI();
        }

        internal void SuggestCreatePattern()
        {
            if (_conditions.Length == 0)
            {
                EditorGUILayout.HelpBox("条件が一つも追加されていません。以下からサンプルパターンを追加できます。", MessageType.Info);
            }
        }

        internal void SuggestAsPreset()
        {
            if (_presets.Length == 0 && _conditions.Length > 0)
            {
                EditorGUILayout.HelpBox("PresetComponentを各制御の親にアタッチするとメニューなどから切り替えられるようになります。", MessageType.Info);
            }
        }

        internal void CreatePatternGUI()
        {
            EditorGUILayout.LabelField("サンプルパターンを追加", EditorStyles.boldLabel);

            _selectedPattern = (PatternType)EditorGUILayout.EnumPopup("選択中のパターン:", _selectedPattern);
            if (_patternDetails.TryGetValue(_selectedPattern, out var patternInfo))
            {
                EditorGUILayout.HelpBox($"説明: {patternInfo.Description}", MessageType.Info);
            }

            if (GUILayout.Button("追加"))
            {
                CreatePatternImpl(_selectedPattern);
            }
        }

        internal void CreatePatternImpl(PatternType patternType)
        {
            var patternGuid = _patternDetails[patternType].Guid;
            var patternPath = AssetDatabase.GUIDToAssetPath(patternGuid);
            var patternObject = AssetDatabase.LoadAssetAtPath<GameObject>(patternPath);
            if (patternObject == null)
            {
                Debug.LogError($"failed to load pattern: {patternGuid}");
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(patternObject);
            if (patternType == PatternType.Basic)
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);
            }
            instance.transform.SetParent(Component.transform, false);
            Undo.RegisterCreatedObjectUndo(instance, "Create Pattern");
            Selection.activeObject = instance;
        }
    }
}
