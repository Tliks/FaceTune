using Aoyon.FaceTune.Importing;
using Aoyon.FaceTune.Platforms;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;

namespace Aoyon.FaceTune.Platforms.VRChat;

internal sealed class VRChatAnimatorImporterProvider : IFaceTuneImporterProvider
{
    public FaceTuneImporterDescriptor Descriptor { get; } = new(
        "vrchat-animator-controller",
        100,
        "window.import.vrchatAnimator.title",
        "window.import.vrchatAnimator.description",
        "window.result.animatorImport.guide",
        true);

    [InitializeOnLoadMethod]
    private static void Register()
        => FaceTuneImporterRegistry.Register(new VRChatAnimatorImporterProvider());

    public bool IsAvailable(GameObject avatarRoot)
        => avatarRoot.TryGetComponent<VRCAvatarDescriptor>(out _);

    public IFaceTuneImportSession CreateSession(GameObject avatarRoot)
        => new Session(avatarRoot.GetComponent<VRCAvatarDescriptor>());

    private sealed class Session : IFaceTuneImportSession
    {
        private readonly VRCAvatarDescriptor _descriptor;
        private AnimatorController? _controller;

        public Session(VRCAvatarDescriptor descriptor)
        {
            _descriptor = descriptor;
            _controller = descriptor.baseAnimationLayers
                .Where(layer => layer.type == VRCAvatarDescriptor.AnimLayerType.FX)
                .Select(layer => layer.animatorController)
                .OfType<AnimatorController>()
                .FirstOrDefault();
        }

        public bool CanImport => _controller != null;

        public void DrawConfiguration()
        {
            _controller = (AnimatorController?)EditorGUILayout.ObjectField(
                "window.import.controller.label".LS(),
                _controller,
                typeof(AnimatorController),
                false);
            if (_controller == null)
                EditorGUILayout.HelpBox("window.import.controller.empty".LS(), MessageType.Warning);
        }

        public GameObject? Import(AvatarContext context, GameObject destination)
        {
            if (_controller == null) return null;
            var platformSupport = MetabasePlatformSupport.GetForAvatar(_descriptor.transform)
                .OfType<VRChatSupport>()
                .FirstOrDefault();
            if (platformSupport == null)
                throw new InvalidOperationException("VRChat platform support is unavailable.");
            return new AnimatorControllerImporter(context, _controller, platformSupport).Import(destination);
        }

        public void Dispose()
        {
        }
    }
}
