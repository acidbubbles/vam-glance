using System;

public class GlanceTarget : MVRScript
{
    private readonly JSONStorableFloat _weightJSON = new JSONStorableFloat("Weight", 1f, 0f, 1f, true);
    private JSONStorableBool _glanceTargetJSON;

    public override void Init()
    {
        CreateSlider(_weightJSON).label = "Weight (look probability & duration)";
        RegisterFloat(_weightJSON);
    }

    private void OnEnable()
    {
        if (!containingAtom.IsBoolJSONParam("GlanceTarget"))
        {
            _glanceTargetJSON = new JSONStorableBool("GlanceTarget", true);
            containingAtom.RegisterBool(_glanceTargetJSON);
        }
    }

    private void OnDisable()
    {
        if (_glanceTargetJSON != null)
        {
            containingAtom.DeregisterBool(_glanceTargetJSON);
            _glanceTargetJSON = null;
        }
    }
}
