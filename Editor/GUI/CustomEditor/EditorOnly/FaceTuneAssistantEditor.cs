using UnityEditor.Animations;
using Aoyon.FaceTune.Importer;
using Aoyon.FaceTune.Platforms;

namespace Aoyon.FaceTune.Gui;

[CustomEditor(typeof(FaceTuneAssistantComponent))]
internal class FaceTuneAssistantEditor : FaceTuneIMGUIEditorBase<FaceTuneAssistantComponent>
{
    private PatternGUI _patternProvider = null!;
    // private SuggestionProvider _suggestionProvider = null!;

    public override void OnEnable()
    {
        base.OnEnable();
        _patternProvider = new PatternGUI(Component.gameObject);
        // _suggestionProvider = new SuggestionProvider(Component.gameObject);
    }

    public override void OnDisable()
    {
        base.OnDisable();
        _patternProvider.Dispose();
    }

    protected override void OnInnerInspectorGUI()
    {
        _patternProvider.Draw();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("FaceTuneAssistant:label:ExpressionDescription".LS(), MessageType.Info);
        // EditorGUILayout.Space();
        // _suggestionProvider.Draw();
    }
}

/*
internal class SuggestionProvider
{
    private readonly GameObject _root;
    private readonly PresetComponent[] _presets;
    private readonly ConditionComponent[] _conditions;

    public SuggestionProvider(GameObject root)
    {
        _root = root;
        _presets = _root.GetComponentsInChildren<PresetComponent>();
        _conditions = _root.GetComponentsInChildren<ConditionComponent>();
    }

    public void Draw()
    {
    }
}
*/

internal static class PatternInfos
{
    public enum HandGesturePatternType
    {
        LeftOnly,
        RightOnly,
        BasicRight,
        BasicLeft,
        HandSign,
        Blending
    }

    public enum MenuPatternType
    {
        ExclusiveMenu,
        BlendingMenu
    }

    public enum OtherPatternType
    {
        HeadContact
    }

    public struct PatternInfo
    {
        public string DescriptionKey;
        public string Guid;

        public PatternInfo(string descriptionKey, string guid)
        {
            DescriptionKey = descriptionKey;
            Guid = guid;
        }
    }

    public static PatternInfo GetInfo(HandGesturePatternType type) => handGestureInfos[type];
    public static PatternInfo GetInfo(MenuPatternType type) => menuInfos[type];
    public static PatternInfo GetInfo(OtherPatternType type) => otherInfos[type];

    private static readonly Dictionary<HandGesturePatternType, PatternInfo> handGestureInfos = new()
    {
        { HandGesturePatternType.LeftOnly, new("HandGesturePatternType:enum:LeftOnly:Desc", "8321eef2d75950543bc39fcdc9709128")    },
        { HandGesturePatternType.RightOnly, new("HandGesturePatternType:enum:RightOnly:Desc", "82eca51c1b5f4374da399ddf321509cb") },
        { HandGesturePatternType.BasicRight, new("HandGesturePatternType:enum:BasicRight:Desc", "c259edc6efd4aaa4bba3b1636557cc3b") },
        { HandGesturePatternType.BasicLeft, new("HandGesturePatternType:enum:BasicLeft:Desc", "376099cca4d264b4fbfbeeb7901dc770") },
        { HandGesturePatternType.HandSign, new("HandGesturePatternType:enum:HandSign:Desc", "e7a261d8cf051454ea0c41e427463276") },
        { HandGesturePatternType.Blending, new("HandGesturePatternType:enum:Blending:Desc", "9eb5bf9eeb8dc81488fb9453d21f3510") },
    };

    private static readonly Dictionary<MenuPatternType, PatternInfo> menuInfos = new()
    {
        { MenuPatternType.ExclusiveMenu, new("MenuPatternType:enum:ExclusiveMenu:Desc", "9e1741e66ac069742976cf8c7e785a35") },
        { MenuPatternType.BlendingMenu, new("MenuPatternType:enum:BlendingMenu:Desc", "557c13125870f764bb20173aa14b004f") },
    };

    private static readonly Dictionary<OtherPatternType, PatternInfo> otherInfos = new()
    {
        { OtherPatternType.HeadContact, new("OtherPatternType:enum:HeadContact:Desc", "def9fc6b2a3e6204abe8182548963b41") },
    };
}

internal sealed class PatternGUI : IDisposable
{
    private enum PatternGUIMode
    {
        Gesture,
        Menu,
        AnimatorController,
        Others
    }

    private readonly GameObject _root;

    private LocalizedToolbar _toolbar;
    private PatternGUIMode _currentMode;

    private LocalizedPopup _gesturePopup;
    private PatternInfos.HandGesturePatternType _selectedHandGesturePattern = PatternInfos.HandGesturePatternType.BasicRight;
    private LocalizedPopup _menuPopup;
    private PatternInfos.MenuPatternType _selectedMenuPattern = PatternInfos.MenuPatternType.ExclusiveMenu;
    private LocalizedPopup _otherPopup;
    private PatternInfos.OtherPatternType _selectedOtherPattern = PatternInfos.OtherPatternType.HeadContact;

    private AnimatorController? _selectedAnimatorController;

    public PatternGUI(GameObject root)
    {
        _root = root;

        _toolbar = new LocalizedToolbar(typeof(PatternGUIMode));

        _gesturePopup = new LocalizedPopup("PatternGUI:label:SelectedHandGesturePattern", typeof(PatternInfos.HandGesturePatternType));
        _menuPopup = new LocalizedPopup("PatternGUI:label:SelectedMenuPattern", typeof(PatternInfos.MenuPatternType));
        _otherPopup = new LocalizedPopup("PatternGUI:label:SelectedOtherPattern", typeof(PatternInfos.OtherPatternType));

        var support = MetabasePlatformSupport.GetSupportInParents(_root.transform);
        _selectedAnimatorController = support?.GetAnimatorController();
    }

    public void Draw()
    {
        EditorGUILayout.LabelField("PatternGUI:label:Title".LG(), EditorStyles.boldLabel);

        _currentMode = (PatternGUIMode)_toolbar.Draw((int)_currentMode);

        EditorGUILayout.Space();

        switch (_currentMode)
        {
            case PatternGUIMode.Gesture:
                DrawGestureSection();
                break;  
            case PatternGUIMode.Menu:
                DrawMenuSection();
                break;
            case PatternGUIMode.AnimatorController:
                DrawAnimatorControllerSection();
                break;
            case PatternGUIMode.Others:
                DrawHeadContactSection();
                break;
        }
    }

    private void DrawGestureSection()
    {
        _selectedHandGesturePattern = (PatternInfos.HandGesturePatternType)_gesturePopup.Draw((int)_selectedHandGesturePattern);
        var info = PatternInfos.GetInfo(_selectedHandGesturePattern);
        EditorGUILayout.HelpBox(info.DescriptionKey.LS(), MessageType.Info);
        if (GUILayout.Button("PatternGUI:button:Add".LS()))
        {
            CreatePattern(info.Guid);
        }
    }

    private void DrawMenuSection()
    {
        _selectedMenuPattern = (PatternInfos.MenuPatternType)_menuPopup.Draw((int)_selectedMenuPattern);
        var info = PatternInfos.GetInfo(_selectedMenuPattern);
        EditorGUILayout.HelpBox(info.DescriptionKey.LS(), MessageType.Info);
        if (GUILayout.Button("PatternGUI:button:Add".LS()))
        {
            CreatePattern(info.Guid, addInstaller: false); // Todo: テンプレートからInstallerが消された場合を想定して、Installerがない場合に追加すべき…?
        }
    }

    private void DrawAnimatorControllerSection()
    {
        _selectedAnimatorController = (AnimatorController)EditorGUILayout.ObjectField("PatternGUI:label:SelectedAnimatorControllerPattern".LS(), _selectedAnimatorController, typeof(AnimatorController), false);
        EditorGUILayout.HelpBox("PatternGUI:label:AnimatorControllerPattern:Desc".LS(), MessageType.Info);
        if (_selectedAnimatorController != null && GUILayout.Button("PatternGUI:button:Add".LS()))
        {
            ImportAnimatorController();
        }
    }

    private void DrawHeadContactSection()
    {
        _selectedOtherPattern = (PatternInfos.OtherPatternType)_otherPopup.Draw((int)_selectedOtherPattern);
        var info = PatternInfos.GetInfo(_selectedOtherPattern);
        EditorGUILayout.HelpBox(info.DescriptionKey.LS(), MessageType.Info);
        if (GUILayout.Button("PatternGUI:button:Add".LS()))
        {
            CreatePattern(info.Guid);
        }
    }

    private void CreatePattern(string guid, bool addInstaller = false)
    {   
        PrefabUtility.InstantiatePrefab(guid, true, _root, addInstaller: addInstaller);
    }

    private void ImportAnimatorController()
    {
        if (_selectedAnimatorController == null) throw new Exception("Animator Controller is not selected");
        if (!CustomEditorUtility.TryGetContext(_root, out var context)) throw new Exception("Failed to get context");
        var importer = new AnimatorControllerImporter(context, _selectedAnimatorController);
        importer.Import(_root);
    }

    public void Dispose()
    {
        _gesturePopup?.Dispose();
        _menuPopup?.Dispose();
        _otherPopup?.Dispose();
        _toolbar?.Dispose();
    }
}
