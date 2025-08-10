using Aoyon.FaceTune.Importer;
using UnityEditor.Animations;

namespace Aoyon.FaceTune.Gui
{
    internal enum HandGesturePatternType
    {
        LeftOnly,
        RightOnly,
        BasicRight,
        BasicLeft,
        Blending,
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
        internal const string BasicRightPatternGuid = "c259edc6efd4aaa4bba3b1636557cc3b";
        internal const string BasicLeftPatternGuid = "376099cca4d264b4fbfbeeb7901dc770";
        internal const string BlendingPatternGuid = "9eb5bf9eeb8dc81488fb9453d21f3510";
        internal const string HandSignPatternGuid = "e7a261d8cf051454ea0c41e427463276";

        private static readonly Dictionary<HandGesturePatternType, PatternInfo> _handGesturePatternDetails = new()
        {
            { HandGesturePatternType.LeftOnly, new PatternInfo("左手のみのパターンです。(8通り)", LeftOnlyPatternGuid) },
            { HandGesturePatternType.RightOnly, new PatternInfo("右手のみのパターンです。(8通り)", RightOnlyPatternGuid) },
            { HandGesturePatternType.BasicRight, new PatternInfo("右手が優先される基本的なパターンです。(16通り)", BasicRightPatternGuid) },
            { HandGesturePatternType.BasicLeft, new PatternInfo("左手が優先される基本的なパターンです。(16通り)", BasicLeftPatternGuid) },
            { HandGesturePatternType.HandSign, new PatternInfo("左手と右手の組み合わせで最大64通りのジェスチャーを作成できるパターンです。(64通り)", HandSignPatternGuid) },
            { HandGesturePatternType.Blending, new PatternInfo("片手ごとのアニメーションがブレンドさせるパターンです。目と口の制御をそれぞれの手に割り当てる際などに便利です。(64通り)", BlendingPatternGuid) },
        };

        private static readonly Dictionary<OtherPatternType, PatternInfo> _otherPatternDetails = new()
        {
            { OtherPatternType.ExclusiveMenu, new PatternInfo("排他のメニューを生成するサンプルです。", "9e1741e66ac069742976cf8c7e785a35") },
            { OtherPatternType.BlendingMenu, new PatternInfo("他の表情とブレンドするメニューを生成するサンプルです。", "557c13125870f764bb20173aa14b004f") },
            { OtherPatternType.HeadContact, new PatternInfo("コンタクトを用いたサンプルです。撫でられた際の表情を指定する際などに便利です。", "def9fc6b2a3e6204abe8182548963b41") },
        };

        private HandGesturePatternType _selectedHandGesturePattern = HandGesturePatternType.BasicRight;
        private OtherPatternType _selectedOtherPattern = OtherPatternType.ExclusiveMenu;
        private AnimatorController? _selectedAnimatorController;

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
            EditorGUILayout.LabelField("ExpressionコンポーネントはHierarhyにおいて下にあるほど優先度が高くなります。", EditorStyles.boldLabel);

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
            EditorGUILayout.LabelField("パターンを追加", EditorStyles.boldLabel);

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

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Animator Controllerをインポート", EditorStyles.boldLabel);
            _selectedAnimatorController = (AnimatorController)EditorGUILayout.ObjectField("選択中のAnimator Controller:", _selectedAnimatorController, typeof(AnimatorController), false);
            if (GUILayout.Button("追加"))
            {
                ImportAnimatorController();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
        }

        private void CreatePatternImpl(PatternInfo patternInfo)
        {
            PrefabUtility.InstantiatePrefab(patternInfo.Guid, true, Component.gameObject);
        }

        private void ImportAnimatorController()
        {
            if (_selectedAnimatorController == null)
            {
                throw new Exception("Animator Controller is not selected");
            }
            if (!CustomEditorUtility.TryGetContext(Component.gameObject, out var context))
            {
                throw new Exception("Failed to get context");
            }
            var importer = new AnimatorControllerImporter(context, _selectedAnimatorController);
            importer.Import(Component.gameObject);
        }
    }
}
