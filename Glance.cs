// TODO: Snap when looking away, still apply randomize (e.g. random spots in the frustrum)
// TODO: Validate high view
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimpleJSON;
using UnityEngine;
using Random = UnityEngine.Random;

public class Glance : MVRScript
{
    private const float _mirrorScanSpan = 0.5f;
    private const float _objectScanSpan = 0.08f;
    private const float _syncCheckSpan = 2.19f;
    private const float _validateExtremesSpan = 0.04f;
    private const float _naturalLookDistance = 0.8f;
    private static readonly Vector3 _angularVelocityPredictiveMultiplier = new Vector3(0.3f, 0.5f, 1f);

    private static readonly HashSet<string> _mirrorAtomTypes = new HashSet<string>(new[]
    {
        "Glass",
        "Glass-Stained",
        "ReflectiveSlate",
        "ReflectiveWoodPanel",
    });

    private readonly JSONStorableBool _mirrorsJSON = new JSONStorableBool("Mirrors", false);
    private readonly JSONStorableFloat _playerEyesWeightJSON = new JSONStorableFloat("PlayerEyesWeight", 1f, 0f, 1f, true);
    private readonly JSONStorableFloat _playerMouthWeightJSON = new JSONStorableFloat("PlayerMouthWeight", 0.05f, 0f, 1f, true);
    private readonly JSONStorableFloat _windowCameraWeightJSON = new JSONStorableFloat("WindowCameraWeight", 1f, 0f, 1f, true);
    private readonly JSONStorableFloat _selfHandsWeightJSON = new JSONStorableFloat("SelfHandsWeight", 0f, 0f, 1f, true);
    private readonly JSONStorableFloat _selfGenitalsWeightJSON = new JSONStorableFloat("SelfGenitalsWeight", 1f, 0f, 1f, true);
    private readonly JSONStorableFloat _personsEyesWeightJSON = new JSONStorableFloat("PersonsEyesWeight", 1f, 0f, 1f, true);
    private readonly JSONStorableFloat _personsMouthWeightJSON = new JSONStorableFloat("PersonsMouthWeight", 0.05f, 0f, 1f, true);
    private readonly JSONStorableFloat _personsChestWeightJSON = new JSONStorableFloat("PersonsChestWeight", 0f, 0f, 1f, true);
    private readonly JSONStorableFloat _personsNipplesWeightJSON = new JSONStorableFloat("PersonsNipplesWeight", 0f, 0f, 1f, true);
    private readonly JSONStorableFloat _personsHandsWeightJSON = new JSONStorableFloat("PersonsHandsWeight", 0.05f, 0f, 1f, true);
    private readonly JSONStorableFloat _personsGenitalsWeightJSON = new JSONStorableFloat("PersonsGenitalsWeight", 0.5f, 0f, 1f, true);
    private readonly JSONStorableFloat _personsFeetWeightJSON = new JSONStorableFloat("PersonsFeetWeight", 0f, 0f, 1f, true);
    private readonly JSONStorableFloat _objectsWeightJSON = new JSONStorableFloat("ObjectsWeight", 0f, 0f, 1f, true);
    private readonly JSONStorableFloat _nothingWeightJSON = new JSONStorableFloat("NothingWeight", 0f, 0f, 1f, true);
    private readonly JSONStorableFloat _frustrumJSON = new JSONStorableFloat("FrustrumFOV", 25f, 0f, 45f, false);
    private readonly JSONStorableFloat _frustrumRatioJSON = new JSONStorableFloat("FrustrumRatio", 1.4f, 0.5f, 2f, false);
    private readonly JSONStorableFloat _frustrumTiltJSON = new JSONStorableFloat("FrustrumTilt", -5f, -45f, 45f, true);
    private readonly JSONStorableFloat _frustrumNearJSON = new JSONStorableFloat("FrustrumNear", 0.1f, 0f, 5f, false);
    private readonly JSONStorableFloat _frustrumFarJSON = new JSONStorableFloat("FrustrumFar", 5f, 0f, 5f, false);
    private readonly JSONStorableFloat _lockMinDurationJSON = new JSONStorableFloat("LockMinDuration", 0.5f, 0f, 10f, false);
    private readonly JSONStorableFloat _lockMaxDurationJSON = new JSONStorableFloat("LockMaxDuration", 2f, 0f, 10f, false);
    private readonly JSONStorableFloat _saccadeMinDurationJSON = new JSONStorableFloat("SaccadeMinDuration", 0.2f, 0f, 1f, false);
    private readonly JSONStorableFloat _saccadeMaxDurationJSON = new JSONStorableFloat("SaccadeMaxDuration", 0.5f, 0f, 1f, false);
    private readonly JSONStorableFloat _saccadeRangeJSON = new JSONStorableFloat("SaccadeRange", 0.015f, 0f, 0.1f, true);
    private readonly JSONStorableFloat _quickTurnThresholdJSON = new JSONStorableFloat("QuickTurnThreshold", 4f, 0f, 10f, false);
    private readonly JSONStorableFloat _quickTurnCooldownJSON = new JSONStorableFloat("QuickTurnCooldown", 0.5f, 0f, 2f, false);
    private readonly JSONStorableFloat _unlockedTiltJSON = new JSONStorableFloat("UnlockedTilt", 10f, -30f, 30f, false);
    private readonly JSONStorableFloat _blinkSpaceMinJSON = new JSONStorableFloat("BlinkSpaceMin", 1f, 0f, 10f, false);
    private readonly JSONStorableFloat _blinkSpaceMaxJSON = new JSONStorableFloat("BlinkSpaceMax", 7f, 0f, 10f, false);
    private readonly JSONStorableFloat _blinkTimeMinJSON = new JSONStorableFloat("BlinkTimeMin", 0.1f, 0f, 2f, false);
    private readonly JSONStorableFloat _blinkTimeMaxJSON = new JSONStorableFloat("BlinkTimeMax", 0.4f, 0f, 2f, false);
    private readonly JSONStorableFloat _cameraMouthDistanceJSON = new JSONStorableFloat("CameraMouthDistance", 0.053f, 0f, 0.1f, false);
    private readonly JSONStorableFloat _cameraEyesDistanceJSON = new JSONStorableFloat("CameraEyesDistance", 0.015f, 0f, 0.1f, false);
    private readonly JSONStorableBool _debugJSON = new JSONStorableBool("Debug", false);
    private readonly JSONStorableString _debugDisplayJSON = new JSONStorableString("DebugDisplay", "");

    private bool _ready;
    private bool _restored;
    private DAZBone[] _bones;
    private EyesControl _eyeBehavior;
    private DAZMeshEyelidControl _eyelidBehavior;
    private Transform _head;
    private FreeControllerV3 _headControl;
    private Transform _lEye;
    private LookAtWithLimits _lEyeLimits;
    private LookAtWithLimits _rEyeLimits;
    private Transform _rEye;
    private Rigidbody _headRB;
    private FreeControllerV3 _eyeTarget;
    private Quaternion _frustrumTilt = Quaternion.Euler(-5f, 0f, 0f);
    private Quaternion _unlockedTilt = Quaternion.Euler(10f, 0f, 0f);
    private bool _mirrorsSync;
    private readonly List<BoxCollider> _mirrors = new List<BoxCollider>();
    private readonly List<EyeTargetReference> _objects = new List<EyeTargetReference>();
    private bool _eyeTargetRestoreHidden;
    private Vector3 _eyeTargetRestorePosition;
    private EyesControl.LookMode _eyeBehaviorRestoreLookMode;
    private bool _blinkRestoreEnabled;
    private readonly Plane[] _frustrumPlanes = new Plane[6];
    private readonly List<EyeTargetReference> _lockTargetCandidates = new List<EyeTargetReference>();
    private float _lockTargetCandidatesScoreSum;
    private float _nextMirrorScanTime;
    private float _nextSyncCheckTime;
    private bool _windowCameraInObjects;
    private BoxCollider _lookAtMirror;
    private float _lookAtMirrorDistance;
    private float _nextObjectsScanTime;
    private float _nextValidateExtremesTime;
    private float _nextLockTargetTime;
    private Transform _lockTarget;
    private float _nextSaccadeTime;
    private Vector3 _saccadeOffset;
    private float _nextGazeTime;
    private Vector3 _gazeTarget;
    private float _angularVelocityBurstCooldown;
    private readonly StringBuilder _debugDisplaySb = new StringBuilder();
    private LineRenderer _frustrumLineRenderer;
    private LineRenderer _lockLineRenderer;
    private Vector3[] _frustrumLinePoints;
    private Vector3[] _lockLinePoints;
    private Transform _cameraMouth;
    private Transform _cameraLEye;
    private Transform _cameraREye;
    private JSONStorableBool _windowCameraControl;

    public override void Init()
    {
        if (containingAtom.type != "Person")
        {
            enabled = false;
            return;
        }

        try
        {
            _eyeBehavior = (EyesControl) containingAtom.GetStorableByID("Eyes");
            _eyelidBehavior = (DAZMeshEyelidControl) containingAtom.GetStorableByID("EyelidControl");
            _bones = containingAtom.transform.Find("rescale2").GetComponentsInChildren<DAZBone>();
            _headControl = containingAtom.freeControllers.FirstOrDefault(fc => fc.name == "headControl");
            _head = _bones.First(eye => eye.name == "head").transform;
            var lEyeBone = _bones.First(eye => eye.name == "lEye");
            _lEye = lEyeBone.transform;
            _lEyeLimits = lEyeBone.GetComponent<LookAtWithLimits>();
            var rEyeBone = _bones.First(eye => eye.name == "rEye");
            _rEye = rEyeBone.transform;
            _rEyeLimits = rEyeBone.GetComponent<LookAtWithLimits>();
            _headRB = _head.GetComponent<Rigidbody>();
            _eyeTarget = containingAtom.freeControllers.First(fc => fc.name == "eyeTargetControl");
            _windowCameraControl =  SuperController.singleton.GetAtoms().FirstOrDefault(a => a.type == "WindowCamera")?.GetStorableByID("CameraControl")?.GetBoolJSONParam("cameraOn");
            var presetsJSON = new JSONStorableStringChooser("Presets", new List<string>
            {
                "Defaults",
                "Horny",
                "Shy",
                "Focused",
                "Anime",
            }, "", "Apply preset") { isStorable = false };
            var focusOnPlayerJSON = new JSONStorableAction("FocusOnPlayer", FocusOnPlayerCallback);

            CreateToggle(_mirrorsJSON).label = "Mirrors (look at themselves)";
            CreateSlider(_playerEyesWeightJSON, false, "Eyes (you)", "F4");
            CreateSlider(_playerMouthWeightJSON, false, "Mouth (you)", "F4");
            CreateSlider(_windowCameraWeightJSON, false, "Window camera", "F4");
            CreateSlider(_selfHandsWeightJSON, false, "Hands (self)", "F4");
            CreateSlider(_selfGenitalsWeightJSON, false, "Genitals (self)", "F4");
            CreateSlider(_personsEyesWeightJSON , false, "Eyes (others)", "F4");
            CreateSlider(_personsMouthWeightJSON , false, "Mouth (others)", "F4");
            CreateSlider(_personsChestWeightJSON , false, "Chest (others)", "F4");
            CreateSlider(_personsNipplesWeightJSON , false, "Nipples (others)", "F4");
            CreateSlider(_personsHandsWeightJSON , false, "Hands (others)", "F4");
            CreateSlider(_personsGenitalsWeightJSON , false, "Genitals (others)", "F4");
            CreateSlider(_personsFeetWeightJSON , false, "Feet (others)", "F4");
            CreateSlider(_objectsWeightJSON, false, "Objects (toys, cua, shapes)", "F4");
            CreateSlider(_nothingWeightJSON, false, "Nothing (spacey)", "F4");
            CreateToggle(_debugJSON).label = "Show debug information";
            CreateTextField(_debugDisplayJSON);

            CreateScrollablePopup(presetsJSON, true);
            CreateSlider(_frustrumJSON, true, "Frustrum field of view", "F3");
            CreateSlider(_frustrumRatioJSON, true, "Frustrum ratio (multiply width)", "F3");
            CreateSlider(_frustrumTiltJSON, true, "Frustrum tilt", "F3");
            CreateSlider(_frustrumNearJSON, true, "Frustrum near (closest)", "F3");
            CreateSlider(_frustrumFarJSON, true, "Frustrum far (furthest)", "F3");
            CreateSlider(_lockMinDurationJSON, true, "Min target lock time", "F3");
            CreateSlider(_lockMaxDurationJSON, true, "Max target lock time", "F3");
            CreateSlider(_saccadeMinDurationJSON, true, "Min eye saccade time", "F4");
            CreateSlider(_saccadeMaxDurationJSON, true, "Max eye saccade time", "F4");
            CreateSlider(_saccadeRangeJSON, true, "Range of eye saccade", "F4");
            CreateSlider(_quickTurnThresholdJSON, true, "Quick turn threshold", "F3");
            CreateSlider(_quickTurnCooldownJSON, true, "Quick turn cooldown", "F3");
            CreateSlider(_unlockedTiltJSON, true, "Spacey tilt", "F2");
            CreateSlider(_blinkSpaceMinJSON, true, "Blink space min", "F2");
            CreateSlider(_blinkSpaceMaxJSON, true, "Blink space max", "F3");
            CreateSlider(_blinkTimeMinJSON, true, "Blink time min", "F4");
            CreateSlider(_blinkTimeMaxJSON, true, "Blink time max", "F4");
            CreateSlider(_cameraMouthDistanceJSON, true, "Camera mouth distance", "F4");
            CreateSlider(_cameraEyesDistanceJSON, true, "Camera eyes distance", "F4");

            RegisterStringChooser(presetsJSON);
            RegisterBool(_mirrorsJSON);
            RegisterFloat(_playerEyesWeightJSON);
            RegisterFloat(_playerMouthWeightJSON);
            RegisterFloat(_windowCameraWeightJSON);
            RegisterFloat(_selfHandsWeightJSON);
            RegisterFloat(_selfGenitalsWeightJSON);
            RegisterFloat(_personsEyesWeightJSON );
            RegisterFloat(_personsMouthWeightJSON );
            RegisterFloat(_personsChestWeightJSON );
            RegisterFloat(_personsNipplesWeightJSON );
            RegisterFloat(_personsHandsWeightJSON );
            RegisterFloat(_personsGenitalsWeightJSON );
            RegisterFloat(_personsFeetWeightJSON );
            RegisterFloat(_objectsWeightJSON);
            RegisterFloat(_nothingWeightJSON);
            RegisterFloat(_frustrumJSON);
            RegisterFloat(_frustrumRatioJSON);
            RegisterFloat(_frustrumTiltJSON);
            RegisterFloat(_frustrumNearJSON);
            RegisterFloat(_frustrumFarJSON);
            RegisterFloat(_lockMinDurationJSON);
            RegisterFloat(_lockMaxDurationJSON);
            RegisterFloat(_saccadeMinDurationJSON);
            RegisterFloat(_saccadeMaxDurationJSON);
            RegisterFloat(_saccadeRangeJSON);
            RegisterFloat(_quickTurnThresholdJSON);
            RegisterFloat(_quickTurnCooldownJSON);
            RegisterFloat(_unlockedTiltJSON);
            RegisterFloat(_blinkSpaceMinJSON);
            RegisterFloat(_blinkSpaceMaxJSON);
            RegisterFloat(_blinkTimeMinJSON);
            RegisterFloat(_blinkTimeMaxJSON);
            RegisterFloat(_cameraMouthDistanceJSON);
            RegisterFloat(_cameraEyesDistanceJSON);
            RegisterAction(focusOnPlayerJSON);

            _mirrorsJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _playerEyesWeightJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _playerMouthWeightJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _windowCameraWeightJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _selfHandsWeightJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _selfGenitalsWeightJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _personsEyesWeightJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _personsMouthWeightJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _personsChestWeightJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _personsNipplesWeightJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _personsHandsWeightJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _personsGenitalsWeightJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _personsFeetWeightJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _objectsWeightJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _nothingWeightJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            presetsJSON.setCallbackFunction = val => { ApplyPreset(val, presetsJSON); };
            _frustrumTiltJSON.setCallbackFunction = val => _frustrumTilt = Quaternion.Euler(val, 0f, 0f);
            _frustrumNearJSON.setCallbackFunction = val => _frustrumFarJSON.valNoCallback = Mathf.Max(val, _frustrumFarJSON.val);
            _frustrumFarJSON.setCallbackFunction = val => _frustrumNearJSON.valNoCallback = Mathf.Min(val, _frustrumNearJSON.val);
            _lockMinDurationJSON.setCallbackFunction = val => _lockMaxDurationJSON.valNoCallback = Mathf.Max(val, _lockMaxDurationJSON.val);
            _lockMaxDurationJSON.setCallbackFunction = val => _lockMinDurationJSON.valNoCallback = Mathf.Min(val, _lockMinDurationJSON.val);
            _saccadeMinDurationJSON.setCallbackFunction = val => _saccadeMaxDurationJSON.valNoCallback = Mathf.Max(val, _saccadeMaxDurationJSON.val);
            _saccadeMaxDurationJSON.setCallbackFunction = val => _saccadeMinDurationJSON.valNoCallback = Mathf.Min(val, _saccadeMinDurationJSON.val);
            _unlockedTiltJSON.setCallbackFunction = val => { _unlockedTilt = Quaternion.Euler(val, 0f, 0f); _nextLockTargetTime = 0f; _nextGazeTime = 0f; };
            _blinkSpaceMinJSON.setCallbackFunction = val => { _blinkSpaceMaxJSON.valNoCallback = Mathf.Max(val, _blinkSpaceMaxJSON.val); _eyelidBehavior.blinkSpaceMin = val; };
            _blinkSpaceMaxJSON.setCallbackFunction = val => { _blinkSpaceMinJSON.valNoCallback = Mathf.Min(val, _blinkSpaceMinJSON.val); _eyelidBehavior.blinkSpaceMax = val; };
            _blinkTimeMinJSON.setCallbackFunction = val => { _blinkTimeMaxJSON.valNoCallback = Mathf.Max(val, _blinkTimeMaxJSON.val); _eyelidBehavior.blinkTimeMin = val; };
            _blinkTimeMaxJSON.setCallbackFunction = val => { _blinkTimeMinJSON.valNoCallback = Mathf.Min(val, _blinkTimeMinJSON.val); _eyelidBehavior.blinkTimeMax = val; };
            _cameraMouthDistanceJSON.setCallbackFunction = _ => { if (_cameraMouth != null) _cameraMouth.localPosition = new Vector3(0, -_cameraMouthDistanceJSON.val, 0); };
            _cameraEyesDistanceJSON.setCallbackFunction = _ => { if (_cameraMouth != null) { _cameraLEye.localPosition = new Vector3(-_cameraEyesDistanceJSON.val, 0, 0); _cameraREye.localPosition = new Vector3(_cameraEyesDistanceJSON.val, 0, 0); } };
            _debugJSON.setCallbackFunction = SyncDebug;

            SuperController.singleton.StartCoroutine(DeferredInit());
        }
        catch (Exception e)
        {
            SuperController.LogError($"{nameof(Glance)}.{nameof(Init)}: {e}");
            enabled = false;
        }
    }

    private void ApplyPreset(string val, JSONStorableStringChooser presetsJSON)
    {
        if (!_ready) return;
        if (string.IsNullOrEmpty(val)) return;
        presetsJSON.valNoCallback = "";
        ResetToDefaults();
        switch (val)
        {
            case "Horny":
                _playerMouthWeightJSON.val = 0.8f;
                _personsMouthWeightJSON.val = 0.8f;
                _personsChestWeightJSON.val = 0.4f;
                _personsNipplesWeightJSON.val = 0.4f;
                _personsGenitalsWeightJSON.val = 1f;
                _lockMinDurationJSON.val = 0.4f;
                _lockMaxDurationJSON.val = 1.2f;
                _saccadeMinDurationJSON.val = 0.1f;
                _saccadeMaxDurationJSON.val = 0.4f;
                _saccadeRangeJSON.val = 0.015f;
                _blinkTimeMinJSON.val = 0.1f;
                _blinkTimeMaxJSON.val = 0.2f;
                _blinkSpaceMinJSON.val = 0.8f;
                _blinkSpaceMaxJSON.val = 3f;
                break;
            case "Shy":
                _frustrumJSON.val = 24f;
                _playerEyesWeightJSON.val = 0.2f;
                _personsEyesWeightJSON.val = 0.2f;
                _nothingWeightJSON.val = 0.4f;
                _lockMinDurationJSON.val = 0.6f;
                _lockMaxDurationJSON.val = 1.2f;
                _saccadeMinDurationJSON.val = 0.1f;
                _saccadeMaxDurationJSON.val = 0.4f;
                _saccadeRangeJSON.val = 0.025f;
                _unlockedTiltJSON.val = 12f;
                _blinkTimeMinJSON.val = 0.1f;
                _blinkTimeMaxJSON.val = 0.4f;
                _blinkSpaceMinJSON.val = 0.5f;
                _blinkSpaceMaxJSON.val = 4f;
                break;
            case "Focused":
                _playerMouthWeightJSON.val = 0.1f;
                _personsMouthWeightJSON.val = 0.1f;
                _lockMinDurationJSON.val = 2f;
                _lockMaxDurationJSON.val = 4f;
                _saccadeMinDurationJSON.val = 0.8f;
                _saccadeMaxDurationJSON.val = 1.4f;
                _saccadeRangeJSON.val = 0.01f;
                _unlockedTiltJSON.val = 4f;
                _blinkTimeMinJSON.val = 0.2f;
                _blinkTimeMaxJSON.val = 0.3f;
                _blinkSpaceMinJSON.val = 4f;
                _blinkSpaceMaxJSON.val = 8f;
                break;
            case "Anime":
                _personsMouthWeightJSON.val = 0f;
                _playerMouthWeightJSON.val = 0f;
                _frustrumJSON.val = 35f;
                _saccadeMinDurationJSON.val = 0.07f;
                _saccadeMaxDurationJSON.val = 0.07f;
                _saccadeRangeJSON.val = 0.035f;
                _blinkSpaceMinJSON.val = 0.3f;
                _blinkSpaceMaxJSON.val = 3f;
                _blinkTimeMinJSON.val = 0.15f;
                _blinkTimeMaxJSON.val = 0.15f;
                break;
        }
    }

    private void ResetToDefaults()
    {
        _mirrorsJSON.SetValToDefault();
        _playerEyesWeightJSON.SetValToDefault();
        _playerMouthWeightJSON.SetValToDefault();
        _windowCameraWeightJSON.SetValToDefault();
        _selfHandsWeightJSON.SetValToDefault();
        _selfGenitalsWeightJSON.SetValToDefault();
        _personsEyesWeightJSON .SetValToDefault();
        _personsMouthWeightJSON .SetValToDefault();
        _personsChestWeightJSON .SetValToDefault();
        _personsNipplesWeightJSON .SetValToDefault();
        _personsHandsWeightJSON .SetValToDefault();
        _personsGenitalsWeightJSON .SetValToDefault();
        _personsFeetWeightJSON .SetValToDefault();
        _objectsWeightJSON.SetValToDefault();
        _nothingWeightJSON.SetValToDefault();
        _frustrumJSON.SetValToDefault();
        _frustrumRatioJSON.SetValToDefault();
        _frustrumTiltJSON.SetValToDefault();
        _frustrumNearJSON.SetValToDefault();
        _frustrumFarJSON.SetValToDefault();
        _lockMinDurationJSON.SetValToDefault();
        _lockMaxDurationJSON.SetValToDefault();
        _saccadeMinDurationJSON.SetValToDefault();
        _saccadeMaxDurationJSON.SetValToDefault();
        _saccadeRangeJSON.SetValToDefault();
        _quickTurnThresholdJSON.SetValToDefault();
        _quickTurnCooldownJSON.SetValToDefault();
        _unlockedTiltJSON.SetValToDefault();
        _cameraMouthDistanceJSON.SetValToDefault();
        _cameraEyesDistanceJSON.SetValToDefault();
        _blinkSpaceMinJSON.SetValToDefault();
        _blinkSpaceMaxJSON.SetValToDefault();
        _blinkTimeMinJSON.SetValToDefault();
        _blinkTimeMaxJSON.SetValToDefault();
    }

    private void CreateSlider(JSONStorableFloat jsf, bool right, string label, string valueFormat = "F2")
    {
        var slider = CreateSlider(jsf, right);
        slider.label = label;
        slider.valueFormat = valueFormat;
    }

    private void SyncDebug(bool val)
    {
        if (!val)
        {
            if (_lockLineRenderer != null) Destroy(_lockLineRenderer.gameObject);
            _lockLineRenderer = null;
            _lockLinePoints = null;
            if (_frustrumLineRenderer != null) Destroy(_frustrumLineRenderer.gameObject);
            _frustrumLineRenderer = null;
            _frustrumLinePoints = null;
            return;
        }

        /*
        foreach (var jsf in GetFloatParamNames().Select(n => GetFloatJSONParam(n)))
        {
            if (Math.Abs(jsf.val - jsf.defaultVal) < 0.001f) continue;
            SuperController.LogMessage($"[DBG] {jsf.name}: {jsf.defaultVal:0.000} -> {jsf.val:0.000}");
        }
        */

        if (_frustrumLineRenderer != null) return;

        var lockLineGo = new GameObject("Gaze_Debug_Lock");
        _lockLineRenderer = lockLineGo.AddComponent<LineRenderer>();
        _lockLineRenderer.useWorldSpace = true;
        _lockLineRenderer.material = new Material(Shader.Find("Sprites/Default")) {renderQueue = 4000};
        SetLineColor(_lockLineRenderer, Color.green);
        _lockLineRenderer.widthMultiplier = 0.0004f;
        _lockLineRenderer.positionCount = 3;
        _lockLinePoints = new Vector3[3];

        var frustrumLineGo = new GameObject("Gaze_Debug_Frustrum");
        _frustrumLineRenderer = frustrumLineGo.AddComponent<LineRenderer>();
        _frustrumLineRenderer.useWorldSpace = true;
        _frustrumLineRenderer.material = new Material(Shader.Find("Sprites/Default")) {renderQueue = 4000};
        SetLineColor(_frustrumLineRenderer, Color.cyan);
        _frustrumLineRenderer.widthMultiplier = 0.0004f;
        _frustrumLineRenderer.positionCount = 16;
        _frustrumLinePoints = new Vector3[16];

        _nextLockTargetTime = 0f;
    }

    private static void SetLineColor(LineRenderer lineRenderer, Color color)
    {
        if (ReferenceEquals(lineRenderer, null)) return;
        lineRenderer.colorGradient = new Gradient
        {
            colorKeys = new[] {new GradientColorKey(color, 0f), new GradientColorKey(color, 1f)}
        };
    }

    private IEnumerator DeferredInit()
    {
        yield return new WaitForEndOfFrame();
        if (!_restored)
            containingAtom.RestoreFromLast(this);
        _ready = true;
        if (enabled)
            OnEnable();
    }

    public void OnEnable()
    {
        if (!_ready) return;

        try
        {
            var camera = SuperController.singleton.centerCameraTarget.transform;

            _cameraMouth = new GameObject("Glance_CameraMouth").transform;
            _cameraMouth.SetParent(camera, false);
            _cameraMouth.localPosition = new Vector3(0, -_cameraMouthDistanceJSON.val, 0);

            _cameraLEye = new GameObject("Glance_CameraLEye").transform;
            _cameraLEye.SetParent(camera, false);
            _cameraLEye.localPosition = new Vector3(-_cameraEyesDistanceJSON.val, 0, 0);

            _cameraREye = new GameObject("Glance_CameraREye").transform;
            _cameraREye.SetParent(camera, false);
            _cameraREye.localPosition = new Vector3(_cameraEyesDistanceJSON.val, 0, 0);

            _eyeTargetRestorePosition = _eyeTarget.control.position;
            _eyeTargetRestoreHidden = _eyeTarget.hidden;
            _eyeTarget.hidden = true;

            _eyeBehaviorRestoreLookMode = _eyeBehavior.currentLookMode;
            _eyeBehavior.currentLookMode = EyesControl.LookMode.Target;

            _blinkRestoreEnabled = _eyelidBehavior.GetBoolParamValue("blinkEnabled");
            _eyelidBehavior.SetBoolParamValue("blinkEnabled", true);

            SuperController.singleton.onAtomUIDsChangedHandlers += ONAtomUIDsChanged;

            Rescan();
        }
        catch (Exception e)
        {
            SuperController.LogError($"{nameof(Glance)}.{nameof(OnEnable)}: {e}");
            enabled = false;
        }
    }

    public void OnDisable()
    {
        try
        {
            _debugJSON.val = false;

            if (_cameraMouth != null) Destroy(_cameraMouth.gameObject);
            if (_cameraLEye != null) Destroy(_cameraLEye.gameObject);
            if (_cameraREye != null) Destroy(_cameraREye.gameObject);

            SuperController.singleton.onAtomUIDsChangedHandlers -= ONAtomUIDsChanged;

            _eyeTarget.hidden = _eyeTargetRestoreHidden;
            _eyeTarget.control.position = _eyeTargetRestorePosition;

            _eyeBehavior.currentLookMode = _eyeBehaviorRestoreLookMode;
            _eyelidBehavior.SetBoolParamValue("blinkEnabled", _blinkRestoreEnabled);

            _eyelidBehavior.blinkSpaceMin = 1f;
            _eyelidBehavior.blinkSpaceMax = 7f;
            _eyelidBehavior.blinkTimeMin = 0.1f;
            _eyelidBehavior.blinkTimeMax = 0.4f;

            ClearState();
        }
        catch (Exception e)
        {
            SuperController.LogError($"{nameof(Glance)}.{nameof(OnDisable)}: {e}");
        }
    }

    private void SyncMirrors()
    {
        _mirrorsSync = false;
        _mirrors.Clear();

        if (!_mirrorsJSON.val && !_headControl.possessed) return;

        _mirrors.AddRange(SuperController.singleton.GetAtoms()
            .Where(a => _mirrorAtomTypes.Contains(a.type))
            .Where(a => a.on)
            .Select(a => a.GetComponentInChildren<BoxCollider>())
            .Where(c => c != null));
        _mirrorsSync = true;
    }

    private void SyncObjects()
    {
        _objects.Clear();
        _windowCameraInObjects = false;
        _nextObjectsScanTime = 0f;
        _nextLockTargetTime = 0f;
        _nextMirrorScanTime = 0f;
        _nextSyncCheckTime = _syncCheckSpan;

        if (_playerEyesWeightJSON.val >= 0.01f)
        {
            _objects.Add(new EyeTargetReference(_cameraLEye, _playerEyesWeightJSON.val / 2f));
            _objects.Add(new EyeTargetReference(_cameraREye, _playerEyesWeightJSON.val / 2f));
        }

        if (_playerMouthWeightJSON.val >= 0.01f)
            _objects.Add(new EyeTargetReference(_cameraMouth, _playerMouthWeightJSON.val));

        foreach (var atom in SuperController.singleton.GetAtoms())
        {
            if (!atom.on) continue;

            switch (atom.type)
            {
                case "WindowCamera":
                {
                    if (_windowCameraWeightJSON.val < 0.01f) continue;
                    if (atom.GetStorableByID("CameraControl")?.GetBoolParamValue("cameraOn") != true) continue;
                    _objects.Add(new EyeTargetReference(atom.mainController.control, _windowCameraWeightJSON.val));
                    _windowCameraInObjects = true;
                    break;
                }
                case "Person":
                {
                    if (atom == containingAtom)
                    {
                        foreach (var bone in _bones)
                        {
                            if (_selfHandsWeightJSON.val >= 0.01f && (bone.name == "lHand" || bone.name == "rHand"))
                                _objects.Add(new EyeTargetReference(bone.transform, _selfHandsWeightJSON.val));
                            else if (_selfGenitalsWeightJSON.val >= 0.01f && (bone.name == "Gen1" || bone.name == "Gen3"))
                                _objects.Add(new EyeTargetReference(bone.transform, _selfGenitalsWeightJSON.val));
                        }

                        continue;
                    }

                    var bones = atom.transform.Find("rescale2").GetComponentsInChildren<DAZBone>();
                    if (_personsEyesWeightJSON.val > 0.01f)
                    {
                        _objects.Add(new EyeTargetReference(bones.First(b => b.name == "lEye").transform, _personsEyesWeightJSON.val / 2f));
                        _objects.Add(new EyeTargetReference(bones.First(b => b.name == "rEye").transform, _personsEyesWeightJSON.val / 2f));
                    }
                    if (_personsMouthWeightJSON.val > 0.01f)
                    {
                        _objects.Add(new EyeTargetReference(bones.First(b => b.name == "tongue03").transform, _personsMouthWeightJSON.val));
                    }
                    if (_personsChestWeightJSON.val > 0.01f)
                    {
                        _objects.Add(new EyeTargetReference(bones.First(b => b.name == "chest").transform, _personsChestWeightJSON.val));
                    }
                    if (_personsNipplesWeightJSON.val > 0.01f)
                    {
                        _objects.Add(new EyeTargetReference(atom.rigidbodies.First(b => b.name == "lNipple").transform, _personsNipplesWeightJSON.val / 2f));
                        _objects.Add(new EyeTargetReference(atom.rigidbodies.First(b => b.name == "rNipple").transform, _personsNipplesWeightJSON.val / 2f));
                    }
                    if (_personsHandsWeightJSON.val > 0.01f)
                    {
                        _objects.Add(new EyeTargetReference(bones.First(b => b.name == "lHand").transform, _personsHandsWeightJSON.val));
                        _objects.Add(new EyeTargetReference(bones.First(b => b.name == "rHand").transform, _personsHandsWeightJSON.val));
                    }
                    if (_personsGenitalsWeightJSON.val > 0.01f)
                    {
                        var selector = atom.GetComponentInChildren<DAZCharacterSelector>();
                        if (selector.selectedCharacter.isMale)
                        {
                            _objects.Add(new EyeTargetReference(bones.First(b => b.name == "Gen3").transform, _personsGenitalsWeightJSON.val * 0.8f));
                            _objects.Add(new EyeTargetReference(bones.First(b => b.name == "Testes").transform, _personsGenitalsWeightJSON.val * 0.2f));
                        }
                        else
                        {
                            _objects.Add(new EyeTargetReference(bones.First(b => b.name == "hip").transform, _personsGenitalsWeightJSON.val));
                        }
                    }
                    if (_personsFeetWeightJSON.val > 0.01f)
                    {
                        _objects.Add(new EyeTargetReference(bones.First(b => b.name == "lFoot").transform, _personsFeetWeightJSON.val / 2f));
                        _objects.Add(new EyeTargetReference(bones.First(b => b.name == "rFoot").transform, _personsFeetWeightJSON.val / 2f));
                    }

                    break;
                }
                case "Cube":
                case "Sphere":
                case "Dildo":
                case "Paddle":
                case "ToyAH":
                case "ToyBP":
                case "CustomUnityAsset":
                case "Torch":
                {
                    if (_objectsWeightJSON.val < 0.01f) continue;
                    _objects.Add(new EyeTargetReference(atom.mainController.control, _objectsWeightJSON.val));
                    break;
                }
                case "Empty":
                {
                    if (!atom.storeId.StartsWith("GlanceTarget_")) continue;
                        _objects.Add(new EyeTargetReference(atom.mainController.control));
                    break;
                }
            }
        }
    }

    public void Rescan()
    {
        ClearState();
        SyncMirrors();
        SyncObjects();
    }

    private void ClearState()
    {
        _lookAtMirror = null;
        _mirrorsSync = false;
        _mirrors.Clear();
        _objects.Clear();
        _lockTargetCandidates.Clear();
        _lockTargetCandidatesScoreSum = 0f;
        _nextMirrorScanTime = 0f;
        _nextSyncCheckTime = 0f;
        _nextObjectsScanTime = 0f;
        _nextValidateExtremesTime = 0f;
        _nextLockTargetTime = 0f;
        _nextSaccadeTime = 0f;
        _saccadeOffset = Vector3.zero;
        _nextGazeTime = 0f;
        _gazeTarget = Vector3.zero;
        _angularVelocityBurstCooldown = 0f;
    }

    public void Update()
    {
        var eyesCenter = (_lEye.position + _rEye.position) / 2f;

        CheckSyncNeeded();
        DetectHighAngularVelocity();
        ScanMirrors(eyesCenter);
        ScanObjects(eyesCenter);
        InvalidateExtremes();
        SelectLockTarget();

        var hasTarget = !ReferenceEquals(_lockTarget, null);
        Vector3 lockPosition;
        if (hasTarget)
        {
            lockPosition = _lockTarget.transform.position;
        }
        else if (!ReferenceEquals(_lookAtMirror, null))
        {
            var reflectPosition = ComputeMirrorLookback(eyesCenter);
            lockPosition = reflectPosition;
        }
        else
        {
            SelectGazeTarget(eyesCenter);
            lockPosition = _gazeTarget;
        }

        SelectSaccade();
        _eyeTarget.control.position = lockPosition + _saccadeOffset;

        if (_lockLinePoints != null)
        {
            _lockLinePoints[0] = _lEye.position;
            _lockLinePoints[1] = lockPosition;
            _lockLinePoints[2] = _rEye.position;
            _lockLineRenderer.SetPositions(_lockLinePoints);
            SetLineColor(_lockLineRenderer, hasTarget ? Color.green : Color.gray);
        }

        if (!_eyeTarget.hidden) _eyeTarget.hidden = true;
    }

    private void CheckSyncNeeded()
    {
        if (_nextSyncCheckTime > Time.time) return;
        _nextSyncCheckTime = Time.time + _syncCheckSpan;

        if (_windowCameraWeightJSON.val >= 0.01)
        {
            if (!_windowCameraInObjects && _windowCameraControl.val)
                SyncObjects();
            else if (_windowCameraInObjects && !_windowCameraControl.val)
                SyncObjects();
        }

        if (!_mirrorsJSON.val && (_headControl.possessed != _mirrorsSync))
        {
            SyncMirrors();
        }
    }

    private void InvalidateExtremes()
    {
        if (_nextValidateExtremesTime > Time.time) return;
        _nextValidateExtremesTime = Time.time + _validateExtremesSpan;

        if (AreEyesInRange()) return;

        _nextGazeTime = 0f;
        _nextLockTargetTime = 0f;
        _angularVelocityBurstCooldown = 0f;
    }

    private bool AreEyesInRange()
    {
        return IsEyeInRange(_lEye, _lEyeLimits) && IsEyeInRange(_rEye, _rEyeLimits);
    }

    private static bool IsEyeInRange(Transform eye, LookAtWithLimits limits)
    {
        var angles = eye.localEulerAngles;
        var y = angles.y;
        if (y < 180)
        {
            if (Mathf.Abs(y - limits.MaxRight) < 0.1f)
                return false;
        }
        else if (Mathf.Abs(360 - y - limits.MaxLeft) < 0.1f)
        {
            return false;
        }

        // NOTE: We don't validate vertical extremes because it's visually acceptable to go further than the eye limit
        /*
        var x = angles.x;
        if (x < 180)
        {
            if (Mathf.Abs(x - limits.MaxDown) < 1f)
                return false;
        }
        else if (Mathf.Abs(360 - x - limits.MaxUp) < 1f)
        {
            return false;
        }
        */

        return true;
    }

    private bool IsInAngleRange(Vector3 eyesCenter, Vector3 targetPosition)
    {
        var lookAngle = _head.InverseTransformDirection(targetPosition - eyesCenter);
        var yaw = Vector3.Angle(Vector3.ProjectOnPlane(lookAngle, Vector3.up), Vector3.forward);
        if (yaw > 26) return false;
        var pitch = Vector3.Angle(Vector3.ProjectOnPlane(lookAngle, Vector3.right), Vector3.forward);
        if (pitch > 30) return false;
        return true;
    }

    private Vector3 ComputeMirrorLookback(Vector3 eyesCenter)
    {
        var mirrorTransform = _lookAtMirror.transform;
        var mirrorPosition = mirrorTransform.position;
        var mirrorNormal = mirrorTransform.up;
        var plane = new Plane(mirrorNormal, mirrorPosition);
        var planePoint = plane.ClosestPointOnPlane(eyesCenter);
        var reflectPosition = planePoint - (eyesCenter - planePoint);
        return reflectPosition;
    }

    private void SelectGazeTarget(Vector3 eyesCenter)
    {
        if (_nextGazeTime > Time.time) return;
        _nextGazeTime = Time.time + Random.Range(_lockMinDurationJSON.val, _lockMaxDurationJSON.val);

        var localAngularVelocity = transform.InverseTransformDirection(_headRB.angularVelocity);
        var angularVelocity = Vector3.Scale(localAngularVelocity * Mathf.Rad2Deg, _angularVelocityPredictiveMultiplier);
        var angularVelocityQ = Quaternion.Euler(new Vector3(Mathf.Clamp(angularVelocity.x, -24, 24), Mathf.Clamp(angularVelocity.y, -25f, 25f), 0f));

        _gazeTarget = eyesCenter + (_head.rotation * _frustrumTilt * _unlockedTilt * angularVelocityQ * Vector3.forward) * _naturalLookDistance;
    }

    private void DetectHighAngularVelocity()
    {
        // Immediate recompute if the head moves fast
        if (_headRB.angularVelocity.sqrMagnitude > _quickTurnThresholdJSON.val)
        {
            var nextTime = Time.time + _quickTurnCooldownJSON.val;
            if (_angularVelocityBurstCooldown < Time.time)
            {
                _eyelidBehavior.Blink();
                _angularVelocityBurstCooldown = nextTime;
            }

            _lockTarget = null;
            _nextGazeTime = 0f;
            _nextObjectsScanTime = nextTime;
            _nextLockTargetTime = nextTime;
            _nextSaccadeTime = nextTime;
            _nextValidateExtremesTime = nextTime;
        }
        else if (_angularVelocityBurstCooldown != 0)
        {
            if (_angularVelocityBurstCooldown < Time.time)
                _angularVelocityBurstCooldown = 0f;
        }
    }

    private void SelectSaccade()
    {
        if (_nextSaccadeTime > Time.time) return;
        _nextSaccadeTime = Time.time + Random.Range(_saccadeMinDurationJSON.val, _saccadeMaxDurationJSON.val);

        _saccadeOffset = _head.rotation * (new Vector3(Random.value - 0.5f, Random.value - 0.5f, 0f) * _saccadeRangeJSON.val);
    }

    private void SelectLockTarget()
    {
        if (_nextLockTargetTime > Time.time) return;

        _saccadeOffset = Vector3.zero;
        _nextSaccadeTime = Time.time + Random.Range(_saccadeMinDurationJSON.val, _saccadeMaxDurationJSON.val);

        if (_lockTargetCandidates.Count == 0)
        {
            _lockTarget = null;
            _nextLockTargetTime = float.PositiveInfinity;
        }
        else if(_lockTargetCandidates.Count == 1)
        {
            _lockTarget = _lockTargetCandidates[0].transform;
            _nextLockTargetTime = float.PositiveInfinity;
        }
        else
        {
            var lockRoll = Random.Range(0f, _lockTargetCandidatesScoreSum);
            var lockTarget = new EyeTargetReference(null, 0f);
            var sum = 0f;
            for (var i = 0; i < _lockTargetCandidates.Count; i++)
            {
                lockTarget = _lockTargetCandidates[i];
                sum += lockTarget.weight;
                if (lockRoll < sum) break;
            }
            _lockTarget = lockTarget.transform;
            var gazeDuration = (_lockMaxDurationJSON.val - _lockMinDurationJSON.val) * lockTarget.weight;
            _nextLockTargetTime = Time.time + Random.Range(_lockMinDurationJSON.val, _lockMinDurationJSON.val + gazeDuration);
        }

        if (_debugJSON.val && UITransform.gameObject.activeInHierarchy)
            UpdateDebugDisplay();
    }

    private void UpdateDebugDisplay()
    {
        _debugDisplaySb.Length = 0;

        _debugDisplaySb.Append(_lockTargetCandidates.Count);
        _debugDisplaySb.Append(" in focus over ");
        _debugDisplaySb.Append(_objects.Count);
        _debugDisplaySb.Append(" potential targets.");
        _debugDisplaySb.AppendLine();

        if (!ReferenceEquals(_lockTarget, null))
        {
            var fc = _lockTarget.GetComponent<FreeControllerV3>();
            if (!ReferenceEquals(fc, null))
            {
                _debugDisplaySb.Append("Locked on '");
                _debugDisplaySb.Append(fc.name);
                _debugDisplaySb.Append("' of atom '");
                _debugDisplaySb.Append(fc.containingAtom.name);
                _debugDisplaySb.AppendLine("'");
            }
            else
            {
                _debugDisplaySb.Append("Locked on '");
                _debugDisplaySb.Append(_lockTarget.name);
                _debugDisplaySb.AppendLine("'");
            }
        }
        else
        {
            _debugDisplaySb.AppendLine("Not locked on a target.");
        }

        _debugDisplayJSON.val = _debugDisplaySb.ToString();
        _debugDisplaySb.Length = 0;
    }

    private void ScanObjects(Vector3 eyesCenter)
    {
        if (_nextObjectsScanTime > Time.time) return;
        _nextObjectsScanTime = Time.time + _objectScanSpan;

        var originalCount = _lockTargetCandidates.Count;
        _lockTargetCandidates.Clear();
        _lockTargetCandidatesScoreSum = 0f;

        if (_objects.Count == 0) return;

        //var planes = GeometryUtility.CalculateFrustumPlanes(SuperController.singleton.centerCameraTarget.targetCamera);
        CalculateFrustum(eyesCenter, _head.rotation * _frustrumTilt * Vector3.forward, _frustrumJSON.val * Mathf.Deg2Rad, _frustrumRatioJSON.val, _frustrumNearJSON.val, _frustrumFarJSON.val, _frustrumPlanes);

        Transform closest = null;
        var closestDistance = float.PositiveInfinity;
        foreach (var o in _objects)
        {
            var position = o.transform.position;
            var bounds = new Bounds(position, new Vector3(0.001f, 0.001f, 0.001f));
            if (!GeometryUtility.TestPlanesAABB(_frustrumPlanes, bounds)) continue;
            var distance = Vector3.SqrMagnitude(bounds.center - eyesCenter);
            if (distance > _lookAtMirrorDistance) continue;
            if (!IsInAngleRange(eyesCenter, position)) continue;
            var score = o.weight - (distance / 10f);
            _lockTargetCandidates.Add(new EyeTargetReference(
                o.transform,
                score
            ));
            _lockTargetCandidatesScoreSum += score;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = o.transform;
            }
        }

        if (_nothingWeightJSON.val > 0.01f)
        {
            _lockTargetCandidates.Add(new EyeTargetReference(
                null,
                _nothingWeightJSON.val
            ));
            _lockTargetCandidatesScoreSum += _nothingWeightJSON.val;
        }

        if (_lockTargetCandidates.Count != originalCount)
        {
            if (_lockTargetCandidates.Count > 0)
            {
                _lockTarget = closest;
                _nextLockTargetTime = Time.time + Random.Range(_lockMinDurationJSON.val, _lockMaxDurationJSON.val);
                SetLineColor(_frustrumLineRenderer, Color.cyan);
            }
            else
            {
                _nextLockTargetTime = 0;
                _nextGazeTime = 0;
                SetLineColor(_frustrumLineRenderer, Color.gray);
            }
        }
    }

    private void FocusOnPlayerCallback()
    {
        _nextObjectsScanTime = Time.time + _lockMaxDurationJSON.val;
        _nextSaccadeTime = Time.time + _saccadeMaxDurationJSON.val;
        _nextMirrorScanTime = Time.time + _lockMaxDurationJSON.val;
        _nextValidateExtremesTime = Time.time + _lockMaxDurationJSON.val;
        _lockTarget = SuperController.singleton.centerCameraTarget.transform;
        _lookAtMirror = null;
    }

    private void ScanMirrors(Vector3 eyesCenter)
    {
        if (_nextMirrorScanTime > Time.time) return;
        _nextMirrorScanTime = Time.time + _mirrorScanSpan;

        _lookAtMirror = null;
        _lookAtMirrorDistance = float.PositiveInfinity;

        if (_mirrors.Count <= 0)
            return;

        var headPosition = _head.position;

        if (_mirrors.Count == 1)
        {
            _lookAtMirror = _mirrors[0];
            _lookAtMirrorDistance = Vector3.Distance(headPosition, _lookAtMirror.transform.position);
            return;
        }

        var ray = new Ray(eyesCenter, _head.forward);
        var closestMirrorDistance = float.PositiveInfinity;
        BoxCollider closestMirror = null;
        for (var i = 0; i < _mirrors.Count; i++)
        {
            var potentialMirror = _mirrors[i];
            var potentialMirrorDistance = Vector3.Distance(headPosition, potentialMirror.transform.position);
            if (potentialMirrorDistance < closestMirrorDistance)
            {
                closestMirrorDistance = potentialMirrorDistance;
                closestMirror = potentialMirror;
            }

            RaycastHit hit;
            if (!potentialMirror.Raycast(ray, out hit, 20f))
                continue;
            if (hit.distance > _lookAtMirrorDistance) continue;
            _lookAtMirrorDistance = hit.distance;
            _lookAtMirror = potentialMirror;
        }

        if (ReferenceEquals(_lookAtMirror, null))
        {
            if (ReferenceEquals(closestMirror, null)) return;
            _lookAtMirror = closestMirror;
        }
    }

    // Source: http://answers.unity.com/answers/1024526/view.html
    private void CalculateFrustum(Vector3 origin, Vector3 direction, float fovRadians, float viewRatio, float near, float far, Plane[] frustrumPlanes)
    {
        var nearCenter = origin + direction * near;
        var farCenter = origin + direction * far;
        var camRight = Vector3.Cross(direction, Vector3.up) * -1;
        var camUp = Vector3.Cross(direction, camRight);
        var nearHeight = 2 * Mathf.Tan(fovRadians / 2) * near;
        var farHeight = 2 * Mathf.Tan(fovRadians / 2) * far;
        var nearWidth = nearHeight * viewRatio;
        var farWidth = farHeight * viewRatio;
        var farTopLeft = farCenter + camUp * (farHeight * 0.5f) - camRight * (farWidth * 0.5f);
        var farBottomLeft = farCenter - camUp * (farHeight * 0.5f) - camRight * (farWidth * 0.5f);
        var farBottomRight = farCenter - camUp * (farHeight * 0.5f) + camRight * (farWidth * 0.5f);
        var nearTopLeft = nearCenter + camUp * (nearHeight * 0.5f) - camRight * (nearWidth * 0.5f);
        var nearTopRight = nearCenter + camUp * (nearHeight * 0.5f) + camRight * (nearWidth * 0.5f);
        var nearBottomRight = nearCenter - camUp * (nearHeight * 0.5f) + camRight * (nearWidth * 0.5f);
        frustrumPlanes[0] = new Plane(nearTopLeft, farTopLeft, farBottomLeft);
        frustrumPlanes[1] = new Plane(nearTopRight, nearBottomRight, farBottomRight);
        frustrumPlanes[2] = new Plane(farBottomLeft, farBottomRight, nearBottomRight);
        frustrumPlanes[3] = new Plane(farTopLeft, nearTopLeft, nearTopRight);
        frustrumPlanes[4] = new Plane(nearBottomRight, nearTopRight, nearTopLeft);
        frustrumPlanes[5] = new Plane(farBottomRight, farBottomLeft, farTopLeft);

        if (_frustrumLinePoints != null)
        {
            //not needed; 6 points are sufficient to calculate the frustum
            var farTopRight = farCenter + camUp*(farHeight*0.5f) + camRight*(farWidth*0.5f);
            var nearBottomLeft  = nearCenter - camUp*(nearHeight*0.5f) - camRight*(nearWidth*0.5f);

            _frustrumLinePoints[0] = nearTopLeft;
            _frustrumLinePoints[1] = nearTopRight;
            _frustrumLinePoints[2] = farTopRight;
            _frustrumLinePoints[3] = nearTopRight;
            _frustrumLinePoints[4] = nearBottomRight;
            _frustrumLinePoints[5] = farBottomRight;
            _frustrumLinePoints[6] = nearBottomRight;
            _frustrumLinePoints[7] = nearBottomLeft;
            _frustrumLinePoints[8] = farBottomLeft;
            _frustrumLinePoints[9] = nearBottomLeft;
            _frustrumLinePoints[10] = nearTopLeft;
            _frustrumLinePoints[11] = farTopLeft;
            _frustrumLinePoints[12] = farTopRight;
            _frustrumLinePoints[13] = farBottomRight;
            _frustrumLinePoints[14] = farBottomLeft;
            _frustrumLinePoints[15] = farTopLeft;
            _frustrumLineRenderer.SetPositions(_frustrumLinePoints);
        }
    }

    private void ONAtomUIDsChanged(List<string> uids)
    {
        Rescan();
    }

    public override void RestoreFromJSON(JSONClass jc, bool restorePhysical = true, bool restoreAppearance = true, JSONArray presetAtoms = null, bool setMissingToDefault = true)
    {
        base.RestoreFromJSON(jc, restorePhysical, restoreAppearance, presetAtoms, setMissingToDefault);
            _restored = true;
    }

    private struct EyeTargetReference
    {
        public Transform transform;
        public float weight;

        public EyeTargetReference(Transform transform, float weight = 1f)
        {
            this.transform = transform;
            this.weight = weight;
        }
    }
}
