namespace com.aoyon.facetune.ui
{
    internal enum HandGesturePatternType
    {
        LeftOnly,
        RightOnly,
        Basic,
        Blending,
        FaceMorphFirst,
        HandSign
    }

    internal enum OtherPatternType
    {
        ExclusiveMenu,
        BlendingMenu,
        HeadContact
    }

    internal struct PatternInfo
    {
        public string Description { get; }
        public string Guid { get; }
        public bool ShouldUnpack { get; }

        public PatternInfo(string description, string guid, bool shouldUnpack = false)
        {
            Description = description;
            Guid = guid;
            ShouldUnpack = shouldUnpack;
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

        private static readonly Dictionary<HandGesturePatternType, PatternInfo> _handGesturePatternDetails = new()
        {
            { HandGesturePatternType.LeftOnly, new PatternInfo("左手のみのパターンです。", LeftOnlyPatternGuid) },
            { HandGesturePatternType.RightOnly, new PatternInfo("右手のみのパターンです。", RightOnlyPatternGuid) },
            { HandGesturePatternType.Basic, new PatternInfo("片手が優先される基本的なパターンです。", BasicPatternGuid, true) },
            { HandGesturePatternType.Blending, new PatternInfo("片手ごとのアニメーションがブレンドさせるパターンです。目と口の制御をそれぞれの手に割り当てる際などに便利です。", BlendingPatternGuid) },
            { HandGesturePatternType.FaceMorphFirst, new PatternInfo("右手と左手に優先度を付けず、最初に実行したジェスチャーを優先させるパターンです。", FaceMorphFirstPatternGuid) },
            { HandGesturePatternType.HandSign, new PatternInfo("左手と右手の組み合わせで最大64通りのジェスチャーを作成できるパターンです。", HandSignPatternGuid) },
        };

        private static readonly Dictionary<OtherPatternType, PatternInfo> _otherPatternDetails = new()
        {
            { OtherPatternType.ExclusiveMenu, new PatternInfo("排他のメニューを生成するサンプルです。", "9e1741e66ac069742976cf8c7e785a35") },
            { OtherPatternType.BlendingMenu, new PatternInfo("他の表情とブレンドするメニューを生成するサンプルです。", "557c13125870f764bb20173aa14b004f") },
            { OtherPatternType.HeadContact, new PatternInfo("コンタクトを用いたサンプルです。撫でられた際の表情を指定する際などに便利です。", "def9fc6b2a3e6204abe8182548963b41") },
        };

        private HandGesturePatternType _selectedHandGesturePattern = HandGesturePatternType.Basic;
        private OtherPatternType _selectedOtherPattern = OtherPatternType.ExclusiveMenu;

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
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Suggestion", EditorStyles.boldLabel);

            SuggestCreatePattern();
            SuggestAsPreset();
            EditorGUILayout.EndVertical();
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
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("サンプルパターンを追加", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("ハンドジェスチャー", EditorStyles.boldLabel);
            _selectedHandGesturePattern = (HandGesturePatternType)EditorGUILayout.EnumPopup("選択中のパターン:", _selectedHandGesturePattern);
            if (_handGesturePatternDetails.TryGetValue(_selectedHandGesturePattern, out var handGesturePatternInfo))
            {
                EditorGUILayout.HelpBox($"説明: {handGesturePatternInfo.Description}", MessageType.Info);
            }
            if (GUILayout.Button("追加"))
            {
                CreatePatternImpl(_handGesturePatternDetails[_selectedHandGesturePattern]);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("その他", EditorStyles.boldLabel);
            _selectedOtherPattern = (OtherPatternType)EditorGUILayout.EnumPopup("選択中のパターン:", _selectedOtherPattern);
            if (_otherPatternDetails.TryGetValue(_selectedOtherPattern, out var otherPatternInfo))
            {
                EditorGUILayout.HelpBox($"説明: {otherPatternInfo.Description}", MessageType.Info);
            }
            if (GUILayout.Button("追加"))
            {
                CreatePatternImpl(_otherPatternDetails[_selectedOtherPattern]);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
        }

        internal void CreatePatternImpl(PatternInfo patternInfo)
        {
            var patternGuid = patternInfo.Guid;
            var patternPath = AssetDatabase.GUIDToAssetPath(patternGuid);
            var patternObject = AssetDatabase.LoadAssetAtPath<GameObject>(patternPath);
            if (patternObject == null)
            {
                Debug.LogError($"failed to load pattern: {patternGuid}");
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(patternObject);
            if (patternInfo.ShouldUnpack)
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);
            }
            instance.transform.SetParent(Component.transform, false);
            Undo.RegisterCreatedObjectUndo(instance, "Create Pattern");
            Selection.activeObject = instance;
        }
    }
}
