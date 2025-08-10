using UnityEditor.Animations;

namespace Aoyon.FaceTune.Gui;

[InitializeOnLoad]
internal static class GenericAnimationEditor
{
    public class AnimationWindowSession
    {
        public readonly UnityEngine.Animator EditedAnimator;
        public readonly RuntimeAnimatorController? OriginalController;
        public readonly bool HasPrefabOverride;
        public readonly AnimationClip EditedClip;
        public readonly Action<AnimationClip>? OnClipModified;

        public AnimationWindowSession(UnityEngine.Animator editedAnimator, RuntimeAnimatorController? originalController, AnimationClip editedClip, Action<AnimationClip>? onClipModified)
        {
            EditedAnimator = editedAnimator;
            OriginalController = originalController;
            HasPrefabOverride = new SerializedObject(editedAnimator).FindProperty("m_Controller").prefabOverride;
            EditedClip = editedClip;
            OnClipModified = onClipModified;
        }
    }

    private static AnimationWindow? s_AnimationWindow;
    private static AnimationWindowSession? s_CurrentSession;
    private static Action<AnimationWindowSession>? s_OnSessionEnded;

    static GenericAnimationEditor()
    {
        UnityEditor.AnimationUtility.onCurveWasModified += OnCurveModified;
        AssemblyReloadEvents.beforeAssemblyReload += StopEditing;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            StopEditing();
        }
    }

    public static bool IsEditing()
    {
        return s_CurrentSession != null;
    }
    
    public static bool IsEditing(UnityEngine.Animator animator)
    {
        return IsEditing() && s_CurrentSession?.EditedAnimator == animator;
    }

    public static void StartEditingWithAnimations(UnityEngine.Animator animator, IReadOnlyList<GenericAnimation> animations, Action<AnimationClip>? onClipModified = null, Action<AnimationWindowSession>? onSessionEnded = null)
    {
        var tmpClip = new AnimationClip { name = $"{FaceTuneConstants.ComponentPrefix} Temporary Clip" };
        tmpClip.SetGenericAnimations(animations);
        Action<AnimationWindowSession> onSessionEnded_ = session =>
        {
            if (tmpClip != null)
            {
                Object.DestroyImmediate(tmpClip);
            }
            onSessionEnded?.Invoke(session);
        };
        StartEditing(animator, tmpClip, onClipModified, onSessionEnded_);
    }

    public static void StartEditing(UnityEngine.Animator animator, AnimationClip clipToEdit, Action<AnimationClip>? onClipModified = null, Action<AnimationWindowSession>? onSessionEnded = null)
    {
        if (animator == null)
        {
            Debug.LogError("Animator is null. Cannot start editing session.");
            return;
        }
        if (clipToEdit == null)
        {
            Debug.LogError("Clip to edit is null. Cannot start editing session.");
            return;
        }

        if (IsEditing())
        {
            StopEditing();
        }

        // Todo: stop NDMF Preview

        var originalController = new SerializedObject(animator).FindProperty("m_Controller").objectReferenceValue as RuntimeAnimatorController;

        s_CurrentSession = new AnimationWindowSession(animator, originalController, clipToEdit, onClipModified);
        s_OnSessionEnded = onSessionEnded;

        var tempController = new AnimatorController { name = $"{FaceTuneConstants.ComponentPrefix} Temporary Controller" };
        tempController.AddLayer("TempLayer");
        var state = tempController.layers[0].stateMachine.AddState("TempState");
        state.motion = clipToEdit;
        animator.runtimeAnimatorController = tempController;

        // Selection.activeObject = animator.gameObject;
        s_AnimationWindow = EditorWindow.GetWindow<AnimationWindow>();
        s_AnimationWindow.animationClip = clipToEdit;
    }

    public static void StopEditing()
    {
        if (!IsEditing())
        {
            return;
        }

        var session = s_CurrentSession!;
        s_CurrentSession = null;

        if (s_AnimationWindow != null)
        {
            s_AnimationWindow.animationClip = null;
            s_AnimationWindow.playing = false;
            s_AnimationWindow.recording = false;
        }

        if (session.EditedAnimator != null)
        {
            var so = new SerializedObject(session.EditedAnimator);
            var prop = so.FindProperty("m_Controller");

            if (session.HasPrefabOverride)
            {
                prop.objectReferenceValue = session.OriginalController;
            }
            else
            {
                UnityEditor.PrefabUtility.RevertPropertyOverride(prop, InteractionMode.AutomatedAction);
            }

            so.ApplyModifiedProperties();
        }

        s_OnSessionEnded?.Invoke(session);
    }
    
    private static void OnCurveModified(AnimationClip clip, EditorCurveBinding binding, UnityEditor.AnimationUtility.CurveModifiedType type)
    {
        if (s_CurrentSession == null) return;
        if (clip == s_CurrentSession.EditedClip)
        {
            s_CurrentSession.OnClipModified?.Invoke(clip);
        }
    }
}