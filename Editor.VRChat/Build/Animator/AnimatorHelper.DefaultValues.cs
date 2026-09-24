namespace Aoyon.FaceTune.Platforms.VRChat;

internal static partial class AnimatorHelper
{
    public static ResolvedNonFacialAnimationSet GetDefaultValueAnimations(
        GameObject root,
        IEnumerable<EditorCurveBinding> curveBindings)
    {
        using var _ = new Utils.ProfilingSampleScope(
            "Animator.ResolveNonFacialDefaults");
        var result = new ResolvedNonFacialAnimationSet();
        foreach (var binding in curveBindings.Distinct())
        {
            var target = AnimationUtility.GetAnimatedObject(root, binding);
            if (target == null || target is Animator) continue;

            if (target is SkinnedMeshRenderer renderer
                && binding.type == typeof(SkinnedMeshRenderer)
                && binding.propertyName.StartsWith(
                    AnimatedBlendShapePrefix,
                    StringComparison.Ordinal))
            {
                var mesh = renderer.sharedMesh;
                if (mesh == null) continue;

                var shapeName = binding.propertyName[AnimatedBlendShapePrefix.Length..];
                var index = mesh.GetBlendShapeIndex(shapeName);
                if (index < 0) continue;

                result.AddFloatCurve(
                    binding,
                    CreateSingleFrameCurve(renderer.GetBlendShapeWeight(index)));
                continue;
            }

            if (target is Transform transform
                && TryGetTransformValue(transform, binding.propertyName, out var transformValue))
            {
                result.AddFloatCurve(binding, CreateSingleFrameCurve(transformValue));
                continue;
            }

            using var serializedObject = new SerializedObject(target);
            serializedObject.UpdateIfRequiredOrScript();
            var property = serializedObject.FindProperty(binding.propertyName);
            if (property == null) continue;

            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    result.AddFloatCurve(
                        binding,
                        CreateSingleFrameCurve(property.boolValue ? 1f : 0f));
                    break;
                case SerializedPropertyType.Float:
                    result.AddFloatCurve(binding, CreateSingleFrameCurve(property.floatValue));
                    break;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.Character:
                    result.AddFloatCurve(binding, CreateSingleFrameCurve(property.intValue));
                    break;
                case SerializedPropertyType.ObjectReference:
                    result.AddObjectCurve(
                        binding,
                        new[]
                        {
                            new ObjectReferenceKeyframe
                            {
                                time = 0f,
                                value = property.objectReferenceValue
                            }
                        });
                    break;
            }
        }

        return result;
    }

    private static bool TryGetTransformValue(
        Transform transform,
        string propertyName,
        out float value)
    {
        switch (propertyName)
        {
            case "m_LocalPosition.x":
            case "localPosition.x":
                value = transform.localPosition.x;
                return true;
            case "m_LocalPosition.y":
            case "localPosition.y":
                value = transform.localPosition.y;
                return true;
            case "m_LocalPosition.z":
            case "localPosition.z":
                value = transform.localPosition.z;
                return true;
            case "m_LocalRotation.x":
            case "localRotation.x":
                value = transform.localRotation.x;
                return true;
            case "m_LocalRotation.y":
            case "localRotation.y":
                value = transform.localRotation.y;
                return true;
            case "m_LocalRotation.z":
            case "localRotation.z":
                value = transform.localRotation.z;
                return true;
            case "m_LocalRotation.w":
            case "localRotation.w":
                value = transform.localRotation.w;
                return true;
            case "m_LocalEulerAnglesRaw.x":
            case "localEulerAnglesRaw.x":
                value = transform.localEulerAngles.x;
                return true;
            case "m_LocalEulerAnglesRaw.y":
            case "localEulerAnglesRaw.y":
                value = transform.localEulerAngles.y;
                return true;
            case "m_LocalEulerAnglesRaw.z":
            case "localEulerAnglesRaw.z":
                value = transform.localEulerAngles.z;
                return true;
            case "m_LocalScale.x":
            case "localScale.x":
                value = transform.localScale.x;
                return true;
            case "m_LocalScale.y":
            case "localScale.y":
                value = transform.localScale.y;
                return true;
            case "m_LocalScale.z":
            case "localScale.z":
                value = transform.localScale.z;
                return true;
            default:
                value = 0f;
                return false;
        }
    }

    private static AnimationCurve CreateSingleFrameCurve(float value)
        => new(new Keyframe(0f, value));
}
