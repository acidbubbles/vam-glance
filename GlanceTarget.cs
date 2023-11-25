using System;
using System.Collections;
using UnityEngine;

public class GlanceTarget : MVRScript
{
    private readonly JSONStorableBool  _onJSON     = new JSONStorableBool("GlanceOn", true);
    private readonly JSONStorableFloat _weightJSON = new JSONStorableFloat("Weight", 0.8f, 0f, 1f, true);
    private JSONStorableBool _glanceTargetJSON;
    private Atom             _containingAtom;
    
    public override void Init()
    {
        _containingAtom = containingAtom;

        _onJSON.setCallbackFunction = val => { StartCoroutine(TriggerRescanCo()); };
        CreateToggle(_onJSON).label = "On (will be looked at)";
        RegisterBool(_onJSON);

        _weightJSON.setCallbackFunction = val => { StartCoroutine(TriggerRescanCo()); };
        CreateSlider(_weightJSON).label = "Weight (probability/duration)";
        RegisterFloat(_weightJSON);

        OnEnable();
    }

    private void OnEnable()
    {
        if (_containingAtom == null) return;
        if (!_containingAtom.IsBoolJSONParam("GlanceTarget"))
        {
            _glanceTargetJSON = new JSONStorableBool("GlanceTarget", true);
            _containingAtom.RegisterBool(_glanceTargetJSON);
        }
        StartCoroutine(TriggerRescanCo());
    }

    private void OnDisable()
    {
        if (_glanceTargetJSON != null)
        {
            _containingAtom.DeregisterBool(_glanceTargetJSON);
            _glanceTargetJSON = null;
        }
        TriggerRescan();
    }

    private IEnumerator TriggerRescanCo()
    {
        yield return new WaitForEndOfFrame();
        TriggerRescan();
    }

    private static void TriggerRescan()
    {
        SuperController.singleton.transform.parent.BroadcastMessage("Refocus", SendMessageOptions.DontRequireReceiver);
    }
}
