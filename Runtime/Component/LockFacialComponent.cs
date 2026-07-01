namespace Aoyon.FaceTune
{
    [AddComponentMenu(BaseMenuPath + "/" + ComponentName)]
    internal class DisbaleELockFacialComponentyeBlinkComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Loack Facial";
        
        public string ConditionParameterName = string.Empty;
    }
}