using System;
using UnityEngine;

public class GlanceTarget : MVRScript
{
    private readonly JSONStorableFloat _weightJSON = new JSONStorableFloat("Weight", 1f, 0f, 1f, true);
    private JSONStorableBool _glanceTargetJSON;
    private Atom _containingAtom;

    public override void Init()
    {
        _containingAtom = containingAtom;
        CreateSlider(_weightJSON).label = "Weight (look probability & duration)";
        RegisterFloat(_weightJSON);
        OnEnable();
        _weightJSON.setCallbackFunction = _ => TriggerRescan();
    }

    private void OnEnable()
    {
        if (_containingAtom == null) return;
        if (!_containingAtom.IsBoolJSONParam("GlanceTarget"))
        {
            _glanceTargetJSON = new JSONStorableBool("GlanceTarget", true);
            _containingAtom.RegisterBool(_glanceTargetJSON);
        }
        TriggerRescan();
    }

    private void OnDisable()
    {
        if (_glanceTargetJSON != null)
        {
            _containingAtom.DeregisterBool(_glanceTargetJSON);
            _glanceTargetJSON = null;
            TriggerRescan();
        }
    }

    private static void TriggerRescan()
    {
        // NOTE: Continuously updating the weight will not perform well
        SuperController.singleton.transform.parent.BroadcastMessage("GlanceRescan", SendMessageOptions.DontRequireReceiver);
    }
}
