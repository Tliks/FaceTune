using UnityEditor.Animations;

namespace Aoyon.FaceTune.Animator;

// AAP(float)のヘルパー

// VRCではfloatは[-1, 1]の範囲に制限され、かつ1/127で区分される
internal static class VRCAAPHelper
{
    public static float IndexToValue(int index)
    {
        if (index < 0 || index > 255)
        {
            throw new ArgumentException($"Index must be between 0 and 255. {index}");
        }
        var value = (index / 127f) - 1f;
        return Mathf.Clamp(value, -1f, 1f);
    }

    public static int ValueToIndex(float value)
    {
        if (value < -1f || value > 1f)
        {
            throw new ArgumentException($"Value must be between -1.0 and 1.0. {value}");
        }
        int index = Mathf.RoundToInt((value + 1f) * 127f);
        return Mathf.Clamp(index, 0, 255);
    }

    public static IEnumerable<AnimatorCondition> IndexConditions(string parameter, bool equal, int index)
    {
        var conditions = new List<AnimatorCondition>();
        if (equal)
        {
            if (index > 0)
            {   
                conditions.Add(new AnimatorCondition()
                {
                    parameter = parameter,
                    mode = AnimatorConditionMode.Greater,
                    threshold = IndexToValue(index - 1)
                });
            }
            if (index < 255)
            {
                conditions.Add(new AnimatorCondition()
                {
                    parameter = parameter,
                    mode = AnimatorConditionMode.Less,
                    threshold = IndexToValue(index + 1)
                });
            }
        }
        else
        {
            conditions.Add(new AnimatorCondition()
            {
                parameter = parameter,
                mode = AnimatorConditionMode.Greater,
                threshold = IndexToValue(index)
            });
            conditions.Add(new AnimatorCondition()
            {
                parameter = parameter,
                mode = AnimatorConditionMode.Less,
                threshold = IndexToValue(index)
            });
        }
        return conditions;
    }
}