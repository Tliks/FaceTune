namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class LockFacialComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = ComponentNamePrefix + "Lock Facial";
        
        public string ConditionParameterName = string.Empty;
    }
}