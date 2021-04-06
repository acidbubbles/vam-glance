using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimpleJSON;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class Glance : MVRScript
{
    private const float _mirrorScanSpan = 0.5f;
    private const float _objectScanSpan = 0.16f;
    private const float _syncCheckSpan = 2.19f;
    private const float _validateExtremesSpan = 0.04f;

    private static readonly JSONStorableFloat _nullWeightJSON = new JSONStorableFloat("UndefinedWeight", 0f, 0f, 1f);
    private static readonly HashSet<string> _mirrorAtomTypes = new HashSet<string>(new[]
    {
        "Glass",
        "Glass-Stained",
        "ReflectiveSlate",
        "ReflectiveWoodPanel",
    });

    private readonly JSONStorableBool _disableAutoTarget = new JSONStorableBool("DisableAutoTarget", false);
    private readonly JSONStorableBool _mirrorsJSON = new JSONStorableBool("Mirrors", false);
    private readonly JSONStorableFloat _playerEyesWeightJSON = new JSONStorableFloat("PlayerEyesWeight", 1f, 0f, 1f, true);
    private readonly JSONStorableFloat _playerMouthWeightJSON = new JSONStorableFloat("PlayerMouthWeight", 0.05f, 0f, 1f, true);
    private readonly JSONStorableFloat _playerHandsWeightJSON = new JSONStorableFloat("PlayerHandsWeight", 0.1f, 0f, 1f, true);
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
    private readonly JSONStorableFloat _nothingWeightJSON = new JSONStorableFloat("NothingWeight", 0f, 0f, 1f, true);
    private readonly JSONStorableFloat _frustumJSON = new JSONStorableFloat("FrustumFOV", 35f, 0f, 45f, false);
    private readonly JSONStorableFloat _frustumRatioJSON = new JSONStorableFloat("FrustumRatio", 1.4f, 0.5f, 2f, false);
    private readonly JSONStorableFloat _frustumTiltJSON = new JSONStorableFloat("FrustumTilt", -5f, -45f, 45f, true);
    private readonly JSONStorableFloat _frustumNearJSON = new JSONStorableFloat("FrustumNear", 0.1f, 0f, 5f, false);
    private readonly JSONStorableFloat _frustumFarJSON = new JSONStorableFloat("FrustumFar", 5f, 0f, 5f, false);
    private readonly JSONStorableFloat _lockMinDurationJSON = new JSONStorableFloat("LockMinDuration", 0.5f, 0f, 10f, false);
    private readonly JSONStorableFloat _lockMaxDurationJSON = new JSONStorableFloat("LockMaxDuration", 2f, 0f, 10f, false);
    private readonly JSONStorableFloat _saccadeMinDurationJSON = new JSONStorableFloat("SaccadeMinDuration", 0.2f, 0f, 1f, false);
    private readonly JSONStorableFloat _saccadeMaxDurationJSON = new JSONStorableFloat("SaccadeMaxDuration", 0.5f, 0f, 1f, false);
    private readonly JSONStorableFloat _saccadeRangeJSON = new JSONStorableFloat("SaccadeRange", 0.015f, 0f, 0.1f, true);
    private readonly JSONStorableFloat _quickTurnThresholdJSON = new JSONStorableFloat("QuickTurnThreshold", 4f, 0f, 10f, false);
    private readonly JSONStorableFloat _quickTurnCooldownJSON = new JSONStorableFloat("QuickTurnCooldown", 0.5f, 0f, 2f, false);
    private readonly JSONStorableFloat _quickTurnMaxXJSON = new JSONStorableFloat("QuickTurnMaxX", 24f, 0f, 26f, false);
    private readonly JSONStorableFloat _quickTurnMaxYJSON = new JSONStorableFloat("QuickTurnMaxY", 25f, 0f, 24f, false);
    private readonly JSONStorableFloat _quickTurnMultiplierXJSON = new JSONStorableFloat("QuickTurnMultiplierX", 0.5f, 0f, 1f, false);
    private readonly JSONStorableFloat _quickTurnMultiplierYJSON = new JSONStorableFloat("QuickTurnMultiplierY", 0.3f, 0f, 1f, false);
    private readonly JSONStorableFloat _unlockedTiltJSON = new JSONStorableFloat("UnlockedTilt", 10f, -30f, 30f, false);
    private readonly JSONStorableFloat _unlockedDistanceJSON = new JSONStorableFloat("UnlockedDistance", 0.8f, 0f, 2f, false);
    private readonly JSONStorableFloat _blinkSpaceMinJSON = new JSONStorableFloat("BlinkSpaceMin", 1f, 0f, 10f, false);
    private readonly JSONStorableFloat _blinkSpaceMaxJSON = new JSONStorableFloat("BlinkSpaceMax", 7f, 0f, 10f, false);
    private readonly JSONStorableFloat _blinkTimeMinJSON = new JSONStorableFloat("BlinkTimeMin", 0.1f, 0f, 2f, false);
    private readonly JSONStorableFloat _blinkTimeMaxJSON = new JSONStorableFloat("BlinkTimeMax", 0.4f, 0f, 2f, false);
    private readonly JSONStorableFloat _cameraMouthDistanceJSON = new JSONStorableFloat("CameraMouthDistance", 0.053f, 0f, 0.1f, false);
    private readonly JSONStorableFloat _cameraEyesDistanceJSON = new JSONStorableFloat("CameraEyesDistance", 0.015f, 0f, 0.1f, false);
    private readonly JSONStorableFloat _objectsInViewChangedCooldownJSON = new JSONStorableFloat("ObjectsInViewChangedCooldown", 0.5f, 0f, 10f, false);
    private readonly JSONStorableBool _preventUnnaturalEyeAngle = new JSONStorableBool("PreventUnnaturalEyeAngle", true);
    private readonly JSONStorableBool _debugJSON = new JSONStorableBool("Debug", false);
    private readonly JSONStorableString _debugDisplayJSON = new JSONStorableString("DebugDisplay", "");

    private bool _ready;
    private bool _restored;
	private bool _needRescan;
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
    private Quaternion _frustumTilt = Quaternion.Euler(-5f, 0f, 0f);
    private Quaternion _unlockedTilt = Quaternion.Euler(10f, 0f, 0f);
    private Vector2 _angularVelocityPredictiveMultiplier = new Vector2(0.3f, 0.5f);
    private bool _mirrorsSync;
    private readonly List<BoxCollider> _mirrors = new List<BoxCollider>();
    private readonly List<EyeTargetCandidate> _objects = new List<EyeTargetCandidate>();
    private bool _eyeTargetRestoreHidden;
    private Vector3 _eyeTargetRestorePosition;
    private EyesControl.LookMode _eyeBehaviorRestoreLookMode;
    private bool _blinkRestoreEnabled;
    private readonly Plane[] _frustumPlanes = new Plane[6];
    private readonly List<EyeTargetCandidate> _lockTargetCandidates = new List<EyeTargetCandidate>();
    private float _lockTargetCandidatesScoredWeightSum;
    private float _nextMirrorScanTime;
    private float _nextSyncCheckTime;
    private bool _windowCameraInObjects;
    private BoxCollider _lookAtMirror;
    private float _lookAtMirrorDistance;
    private float _nextObjectsScanTime;
    private float _nextValidateExtremesTime;
    private float _nextLockTargetTime;
    private EyeTargetCandidate _lockTarget = new EyeTargetCandidate(null, _nullWeightJSON);
    private float _nextSaccadeTime;
    private Vector3 _saccadeOffset;
    private float _nextGazeTime;
    private Vector3 _gazeTarget;
    private float _angularVelocityBurstCooldown;
    private float _objectsInViewChangedExpire;
    private readonly StringBuilder _debugDisplaySb = new StringBuilder();
    private LineRenderer _frustumLineRenderer;
    private LineRenderer _lockLineRenderer;
    private Vector3[] _frustumLinePoints;
    private Vector3[] _lockLinePoints;
    private Transform _cameraMouth;
    private Transform _cameraLEye;
    private Transform _cameraREye;
    private JSONStorableBool _windowCameraControl;
    private readonly List<GlanceTargetReference> _watchedGlanceTargets = new List<GlanceTargetReference>();

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

            CreateTitle("Presets", false);
            var presetsJSON = new JSONStorableStringChooser("Presets", new List<string>
            {
                "Defaults",
                "Horny",
                "Shy",
                "Focused",
                "Anime",
            }, "", "Apply preset") { isStorable = false };
            CreateScrollablePopup(presetsJSON, false);

            CreateTitle("Auto targeting", false);
            CreateToggle(_disableAutoTarget, false).label = "Disable automatic targeting";
            CreateToggle(_mirrorsJSON, false).label = "Mirrors (look at themselves)";

            CreateTitle("Auto targeting priorities (you)", false);
            CreateSlider(_playerEyesWeightJSON, false, "Eyes (you)", "F4");
            CreateSlider(_playerMouthWeightJSON, false, "Mouth (you)", "F4");
            CreateSlider(_playerHandsWeightJSON, false, "Hands (you)", "F4");

            CreateTitle("Auto targeting priorities (camera)", false);
            CreateSlider(_windowCameraWeightJSON, false, "Window camera", "F4");

            CreateTitle("Auto targeting priorities (self)", false);
            CreateSlider(_selfHandsWeightJSON, false, "Hands (self)", "F4");
            CreateSlider(_selfGenitalsWeightJSON, false, "Genitals (self)", "F4");

            CreateTitle("Auto targeting priorities (others)", false);
            CreateSlider(_personsEyesWeightJSON , false, "Eyes (others)", "F4");
            CreateSlider(_personsMouthWeightJSON , false, "Mouth (others)", "F4");
            CreateSlider(_personsChestWeightJSON , false, "Chest (others)", "F4");
            CreateSlider(_personsNipplesWeightJSON , false, "Nipples (others)", "F4");
            CreateSlider(_personsHandsWeightJSON , false, "Hands (others)", "F4");
            CreateSlider(_personsGenitalsWeightJSON , false, "Genitals (others)", "F4");
            CreateSlider(_personsFeetWeightJSON , false, "Feet (others)", "F4");

            CreateTitle("Auto targeting priorities (nothing)", false);
            CreateSlider(_nothingWeightJSON, false, "Nothing (spacey)", "F4");

            CreateTitle("Debugging", false);
            CreateToggle(_debugJSON).label = "Show debug information";
            CreateTextField(_debugDisplayJSON);

            CreateTitle("Frustum settings (angle of view)", true);
            CreateSlider(_frustumJSON, true, "Frustum field of view", "F3");
            CreateSlider(_frustumRatioJSON, true, "Frustum ratio (multiply width)", "F3");
            CreateSlider(_frustumTiltJSON, true, "Frustum tilt", "F3");
            CreateSlider(_frustumNearJSON, true, "Frustum near (closest)", "F3");
            CreateSlider(_frustumFarJSON, true, "Frustum far (furthest)", "F3");

            CreateTitle("Timing", true);
            CreateSlider(_lockMinDurationJSON, true, "Min target lock time", "F3");
            CreateSlider(_lockMaxDurationJSON, true, "Max target lock time", "F3");

            CreateTitle("Eye saccade", true);
            CreateSlider(_saccadeMinDurationJSON, true, "Min eye saccade time", "F4");
            CreateSlider(_saccadeMaxDurationJSON, true, "Max eye saccade time", "F4");
            CreateSlider(_saccadeRangeJSON, true, "Range of eye saccade", "F4");

            CreateTitle("Quick turning", true);
            CreateSlider(_quickTurnThresholdJSON, true, "Quick turn threshold", "F3");
            CreateSlider(_quickTurnCooldownJSON, true, "Quick turn cooldown", "F3");
            CreateSlider(_quickTurnMaxXJSON, true).label = "Quick turn max X";
            CreateSlider(_quickTurnMaxYJSON, true).label = "Quick turn max Y";
            CreateSlider(_quickTurnMultiplierXJSON, true).label = "Quick turn multiplier X";
            CreateSlider(_quickTurnMultiplierYJSON, true).label = "Quick turn multiplier Y";

            CreateTitle("Spacey (no targets)", true);
            CreateSlider(_unlockedTiltJSON, true, "Spacey tilt", "F2");
            CreateSlider(_unlockedDistanceJSON, true, "Spacey distance", "F3");

            CreateTitle("Blinking", true);
            CreateSlider(_blinkSpaceMinJSON, true, "Blink space min", "F2");
            CreateSlider(_blinkSpaceMaxJSON, true, "Blink space max", "F3");
            CreateSlider(_blinkTimeMinJSON, true, "Blink time min", "F4");
            CreateSlider(_blinkTimeMaxJSON, true, "Blink time max", "F4");

            CreateTitle("Player eyes and mouth", true);
            CreateSlider(_cameraMouthDistanceJSON, true, "Camera mouth distance", "F4");
            CreateSlider(_cameraEyesDistanceJSON, true, "Camera eyes distance", "F4");

            CreateTitle("Other settings", true);
            CreateSlider(_objectsInViewChangedCooldownJSON, true, "Objects in view changed cooldown", "F4");
            CreateToggle(_preventUnnaturalEyeAngle, true).label = "Prevent unnatural eye angle";

            RegisterStringChooser(presetsJSON);
            RegisterBool(_disableAutoTarget);
            RegisterBool(_mirrorsJSON);
            RegisterFloat(_playerEyesWeightJSON);
            RegisterFloat(_playerMouthWeightJSON);
            RegisterFloat(_playerHandsWeightJSON);
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
            RegisterFloat(_nothingWeightJSON);
            RegisterFloat(_frustumJSON);
            RegisterFloat(_frustumRatioJSON);
            RegisterFloat(_frustumTiltJSON);
            RegisterFloat(_frustumNearJSON);
            RegisterFloat(_frustumFarJSON);
            RegisterFloat(_lockMinDurationJSON);
            RegisterFloat(_lockMaxDurationJSON);
            RegisterFloat(_saccadeMinDurationJSON);
            RegisterFloat(_saccadeMaxDurationJSON);
            RegisterFloat(_saccadeRangeJSON);
            RegisterFloat(_quickTurnThresholdJSON);
            RegisterFloat(_quickTurnCooldownJSON);
            RegisterFloat(_quickTurnMaxXJSON);
            RegisterFloat(_quickTurnMaxYJSON);
            RegisterFloat(_quickTurnMultiplierXJSON);
            RegisterFloat(_quickTurnMultiplierYJSON);
            RegisterFloat(_unlockedTiltJSON);
            RegisterFloat(_unlockedDistanceJSON);
            RegisterFloat(_blinkSpaceMinJSON);
            RegisterFloat(_blinkSpaceMaxJSON);
            RegisterFloat(_blinkTimeMinJSON);
            RegisterFloat(_blinkTimeMaxJSON);
            RegisterFloat(_cameraMouthDistanceJSON);
            RegisterFloat(_cameraEyesDistanceJSON);
            RegisterFloat(_objectsInViewChangedCooldownJSON);
            RegisterBool(_preventUnnaturalEyeAngle);
            RegisterAction(new JSONStorableAction("FocusOnPlayer", FocusOnPlayer));

            _disableAutoTarget.setCallbackFunction = ValueChangedScheduleRescan;
            _mirrorsJSON.setCallbackFunction = ValueChangedScheduleRescan;
            _playerEyesWeightJSON.setCallbackFunction = ValueChangedScheduleRescan;
            _playerMouthWeightJSON.setCallbackFunction = ValueChangedScheduleRescan;
            _playerHandsWeightJSON.setCallbackFunction = ValueChangedScheduleRescan;
            _windowCameraWeightJSON.setCallbackFunction = ValueChangedScheduleRescan;
            _selfHandsWeightJSON.setCallbackFunction = ValueChangedScheduleRescan;
            _selfGenitalsWeightJSON.setCallbackFunction = ValueChangedScheduleRescan;
            _personsEyesWeightJSON.setCallbackFunction = ValueChangedScheduleRescan;
            _personsMouthWeightJSON.setCallbackFunction = ValueChangedScheduleRescan;
            _personsChestWeightJSON.setCallbackFunction = ValueChangedScheduleRescan;
            _personsNipplesWeightJSON.setCallbackFunction = ValueChangedScheduleRescan;
            _personsHandsWeightJSON.setCallbackFunction = ValueChangedScheduleRescan;
            _personsGenitalsWeightJSON.setCallbackFunction = ValueChangedScheduleRescan;
            _personsFeetWeightJSON.setCallbackFunction = ValueChangedScheduleRescan;
            _nothingWeightJSON.setCallbackFunction = ValueChangedScheduleRescan;
            presetsJSON.setCallbackFunction = val => { ApplyPreset(val, presetsJSON); };
            _frustumJSON.setCallbackFunction = val => _frustumJSON.valNoCallback = Mathf.Clamp(val, 0.001f, 179.999f);
            _frustumTiltJSON.setCallbackFunction = val => _frustumTilt = Quaternion.Euler(val, 0f, 0f);
            _frustumNearJSON.setCallbackFunction = val => _frustumFarJSON.valNoCallback = Mathf.Max(val, _frustumFarJSON.val);
            _frustumFarJSON.setCallbackFunction = val => _frustumNearJSON.valNoCallback = Mathf.Min(val, _frustumNearJSON.val);
            _lockMinDurationJSON.setCallbackFunction = val => _lockMaxDurationJSON.valNoCallback = Mathf.Max(val, _lockMaxDurationJSON.val);
            _lockMaxDurationJSON.setCallbackFunction = val => _lockMinDurationJSON.valNoCallback = Mathf.Min(val, _lockMinDurationJSON.val);
            _saccadeMinDurationJSON.setCallbackFunction = val => _saccadeMaxDurationJSON.valNoCallback = Mathf.Max(val, _saccadeMaxDurationJSON.val);
            _saccadeMaxDurationJSON.setCallbackFunction = val => _saccadeMinDurationJSON.valNoCallback = Mathf.Min(val, _saccadeMinDurationJSON.val);
            _quickTurnMultiplierXJSON.setCallbackFunction = val => _angularVelocityPredictiveMultiplier = new Vector3(_quickTurnMultiplierXJSON.val, _quickTurnMultiplierYJSON.val, 0);
            _quickTurnMultiplierYJSON.setCallbackFunction = val => _angularVelocityPredictiveMultiplier = new Vector3(_quickTurnMultiplierXJSON.val, _quickTurnMultiplierYJSON.val, 0);
            _unlockedTiltJSON.setCallbackFunction = val => { _unlockedTilt = Quaternion.Euler(val, 0f, 0f); _nextLockTargetTime = 0f; _nextGazeTime = 0f; };
            _unlockedDistanceJSON.setCallbackFunction = val => { _nextGazeTime = 0f; };
            _blinkSpaceMinJSON.setCallbackFunction = val => { _blinkSpaceMaxJSON.valNoCallback = Mathf.Max(val, _blinkSpaceMaxJSON.val); _eyelidBehavior.blinkSpaceMin = val; };
            _blinkSpaceMaxJSON.setCallbackFunction = val => { _blinkSpaceMinJSON.valNoCallback = Mathf.Min(val, _blinkSpaceMinJSON.val); _eyelidBehavior.blinkSpaceMax = val; };
            _blinkTimeMinJSON.setCallbackFunction = val => { _blinkTimeMaxJSON.valNoCallback = Mathf.Max(val, _blinkTimeMaxJSON.val); _eyelidBehavior.blinkTimeMin = val; };
            _blinkTimeMaxJSON.setCallbackFunction = val => { _blinkTimeMinJSON.valNoCallback = Mathf.Min(val, _blinkTimeMinJSON.val); _eyelidBehavior.blinkTimeMax = val; };
            _cameraMouthDistanceJSON.setCallbackFunction = _ => { if (_cameraMouth != null) _cameraMouth.localPosition = new Vector3(0, -_cameraMouthDistanceJSON.val, 0); };
            _cameraEyesDistanceJSON.setCallbackFunction = _ => { if (_cameraMouth != null) { _cameraLEye.localPosition = new Vector3(-_cameraEyesDistanceJSON.val, 0, 0); _cameraREye.localPosition = new Vector3(_cameraEyesDistanceJSON.val, 0, 0); } };
            _objectsInViewChangedCooldownJSON.setCallbackFunction = _ => { _objectsInViewChangedExpire = 0f; };
            _preventUnnaturalEyeAngle.setCallbackFunction = ValueChangedScheduleRescan;
            _debugJSON.setCallbackFunction = SyncDebug;

            SuperController.singleton.StartCoroutine(DeferredInit());
        }
        catch (Exception e)
        {
            SuperController.LogError($"{nameof(Glance)}.{nameof(Init)}: {e}");
            enabled = false;
        }
    }

    private void CreateTitle(string text, bool rightSide = false)
    {
        var spacer = CreateSpacer(rightSide);
        spacer.height = 40f;

        var textComponent = spacer.gameObject.AddComponent<Text>();
        textComponent.text = text;
        textComponent.font = manager.configurableTextFieldPrefab.GetComponentInChildren<Text>().font;
        textComponent.fontSize = 30;
        textComponent.fontStyle = FontStyle.Bold;
        textComponent.color = new Color(0.95f, 0.9f, 0.92f);
    }

	private void ValueChangedScheduleRescan(float v)
	{
		_needRescan = true;
	}

	private void ValueChangedScheduleRescan(bool v)
	{
		_needRescan = true;
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
                _frustumJSON.val = 24f;
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
                _frustumJSON.val = 35f;
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
        _disableAutoTarget.SetValToDefault();
        _mirrorsJSON.SetValToDefault();
        _playerEyesWeightJSON.SetValToDefault();
        _playerMouthWeightJSON.SetValToDefault();
        _playerHandsWeightJSON.SetValToDefault();
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
        _nothingWeightJSON.SetValToDefault();
        _frustumJSON.SetValToDefault();
        _frustumRatioJSON.SetValToDefault();
        _frustumTiltJSON.SetValToDefault();
        _frustumNearJSON.SetValToDefault();
        _frustumFarJSON.SetValToDefault();
        _lockMinDurationJSON.SetValToDefault();
        _lockMaxDurationJSON.SetValToDefault();
        _saccadeMinDurationJSON.SetValToDefault();
        _saccadeMaxDurationJSON.SetValToDefault();
        _saccadeRangeJSON.SetValToDefault();
        _quickTurnThresholdJSON.SetValToDefault();
        _quickTurnCooldownJSON.SetValToDefault();
        _unlockedTiltJSON.SetValToDefault();
        _unlockedDistanceJSON.SetValToDefault();
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
            if (_frustumLineRenderer != null) Destroy(_frustumLineRenderer.gameObject);
            _frustumLineRenderer = null;
            _frustumLinePoints = null;
            return;
        }

        if (_frustumLineRenderer != null) return;

        var lockLineGo = new GameObject("Gaze_Debug_Lock");
        _lockLineRenderer = lockLineGo.AddComponent<LineRenderer>();
        _lockLineRenderer.useWorldSpace = true;
        _lockLineRenderer.material = new Material(Shader.Find("Sprites/Default")) {renderQueue = 4000};
        SetLineColor(_lockLineRenderer, Color.green);
        _lockLineRenderer.widthMultiplier = 0.0004f;
        _lockLineRenderer.positionCount = 3;
        _lockLinePoints = new Vector3[3];

        var frustumLineGo = new GameObject("Gaze_Debug_Frustum");
        _frustumLineRenderer = frustumLineGo.AddComponent<LineRenderer>();
        _frustumLineRenderer.useWorldSpace = true;
        _frustumLineRenderer.material = new Material(Shader.Find("Sprites/Default")) {renderQueue = 4000};
        SetLineColor(_frustumLineRenderer, Color.cyan);
        _frustumLineRenderer.widthMultiplier = 0.0004f;
        _frustumLineRenderer.positionCount = 16;
        _frustumLinePoints = new Vector3[16];

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
        SuperController.singleton.BroadcastMessage("OnActionsProviderAvailable", this, SendMessageOptions.DontRequireReceiver);
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

            GlanceRescan();
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
        _nextSyncCheckTime = Time.time + _syncCheckSpan;
        _watchedGlanceTargets.Clear();

        if (!_disableAutoTarget.val)
        {
            if (_playerEyesWeightJSON.val >= 0.01f)
            {
                _objects.Add(new EyeTargetCandidate(_cameraLEye, _playerEyesWeightJSON, 0.5f));
                _objects.Add(new EyeTargetCandidate(_cameraREye, _playerEyesWeightJSON, 0.5f));
            }

            if (_playerMouthWeightJSON.val >= 0.01f)
            {
                _objects.Add(new EyeTargetCandidate(_cameraMouth, _playerMouthWeightJSON));
            }

            if (_playerHandsWeightJSON.val >= 0.01f)
            {
                _objects.Add(new EyeTargetCandidate(SuperController.singleton.leftHand, _playerHandsWeightJSON, 0.5f));
                _objects.Add(new EyeTargetCandidate(SuperController.singleton.rightHand, _playerHandsWeightJSON, 0.5f));
            }
        }

        foreach (var atom in SuperController.singleton.GetAtoms())
        {
            if (!atom.on) continue;

            switch (atom.type)
            {
                case "WindowCamera":
                {
                    if (_disableAutoTarget.val) continue;
                    if (_windowCameraWeightJSON.val < 0.01f) continue;
                    if (atom.GetStorableByID("CameraControl")?.GetBoolParamValue("cameraOn") != true) continue;
                    _objects.Add(new EyeTargetCandidate(atom.mainController.control, _windowCameraWeightJSON));
                    _windowCameraInObjects = true;
                    break;
                }
                case "Person":
                {
                    if (_disableAutoTarget.val) continue;

                    if (atom == containingAtom)
                    {
                        foreach (var bone in _bones)
                        {
                            if (bone.name == "lHand" || bone.name == "rHand")
                            {
                                if (_selfHandsWeightJSON.val >= 0.01f)
                                    _objects.Add(new EyeTargetCandidate(bone.transform, _selfHandsWeightJSON));
                            }
                            else if (bone.name == "Gen1" || bone.name == "Gen3")
                            {
                                if (_selfGenitalsWeightJSON.val >= 0.01f)
                                    _objects.Add(new EyeTargetCandidate(bone.transform, _selfGenitalsWeightJSON, 0.5f));
                            }
                        }

                        continue;
                    }

                    var bones = atom.transform.Find("rescale2").GetComponentsInChildren<DAZBone>();
                    if (_personsEyesWeightJSON.val > 0.01f)
                    {
                        _objects.Add(new EyeTargetCandidate(bones.First(b => b.name == "lEye").transform, _personsEyesWeightJSON, 0.5f));
                        _objects.Add(new EyeTargetCandidate(bones.First(b => b.name == "rEye").transform, _personsEyesWeightJSON, 0.5f));
                    }
                    if (_personsMouthWeightJSON.val > 0.01f)
                    {
                        _objects.Add(new EyeTargetCandidate(bones.First(b => b.name == "tongue03").transform, _personsMouthWeightJSON));
                    }
                    if (_personsChestWeightJSON.val > 0.01f)
                    {
                        _objects.Add(new EyeTargetCandidate(bones.First(b => b.name == "chest").transform, _personsChestWeightJSON));
                    }
                    if (_personsNipplesWeightJSON.val > 0.01f)
                    {
                        _objects.Add(new EyeTargetCandidate(atom.rigidbodies.First(b => b.name == "lNipple").transform, _personsNipplesWeightJSON, 0.5f));
                        _objects.Add(new EyeTargetCandidate(atom.rigidbodies.First(b => b.name == "rNipple").transform, _personsNipplesWeightJSON, 0.5f));
                    }
                    if (_personsHandsWeightJSON.val > 0.01f)
                    {
                        _objects.Add(new EyeTargetCandidate(bones.First(b => b.name == "lHand").transform, _personsHandsWeightJSON));
                        _objects.Add(new EyeTargetCandidate(bones.First(b => b.name == "rHand").transform, _personsHandsWeightJSON));
                    }
                    if (_personsGenitalsWeightJSON.val > 0.01f)
                    {
                        var selector = atom.GetComponentInChildren<DAZCharacterSelector>();
                        if (selector.selectedCharacter.isMale)
                        {
                            _objects.Add(new EyeTargetCandidate(bones.First(b => b.name == "Gen3").transform, _personsGenitalsWeightJSON, 0.8f));
                            _objects.Add(new EyeTargetCandidate(bones.First(b => b.name == "Testes").transform, _personsGenitalsWeightJSON, 0.2f));
                        }
                        else
                        {
                            _objects.Add(new EyeTargetCandidate(bones.First(b => b.name == "hip").transform, _personsGenitalsWeightJSON));
                        }
                    }
                    if (_personsFeetWeightJSON.val > 0.01f)
                    {
                        _objects.Add(new EyeTargetCandidate(bones.First(b => b.name == "lFoot").transform, _personsFeetWeightJSON, 0.5f));
                        _objects.Add(new EyeTargetCandidate(bones.First(b => b.name == "rFoot").transform, _personsFeetWeightJSON, 0.5f));
                    }

                    break;
                }
                default:
                {
                    if (!atom.IsBoolJSONParam("GlanceTarget"))
                        continue;

                    var storables = atom.GetStorableIDs();
                    for (var i = 0; i < storables.Count; i++)
                    {
                        var storableId = storables[i];
                        var storable = atom.GetStorableByID(storableId);
                        var glanceOn = storable.GetBoolJSONParam("GlanceOn");
                        if (glanceOn == null) continue;
                        _watchedGlanceTargets.Add(new GlanceTargetReference
                        {
                            onJSON = glanceOn,
                            onLast = glanceOn.val
                        });
                        if (!glanceOn.val) break;
                        var weightJSON = storable.GetFloatJSONParam("Weight");
                        _objects.Add(new EyeTargetCandidate(atom.mainController.control, weightJSON));

                        break;
                    }

                    break;
                }
            }
        }
    }

    public void GlanceRescan()
    {
		_needRescan = false;
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
        _lockTargetCandidatesScoredWeightSum = 0f;
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
        _objectsInViewChangedExpire = 0f;
    }

    public void Update()
    {
		if (_needRescan)
			GlanceRescan();

        var eyesCenter = (_lEye.position + _rEye.position) / 2f;

        CheckSyncNeeded();
        DetectHighAngularVelocity();
        ScanMirrors(eyesCenter);
        ScanObjects(eyesCenter);
        InvalidateExtremes();
        SelectLockTarget();

        var hasTarget = !ReferenceEquals(_lockTarget.transform, null);
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
        if (!_preventUnnaturalEyeAngle.val) return;
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
        if (!_preventUnnaturalEyeAngle.val) return true;
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

        var angularRotation = GetGazeRotation();
        _gazeTarget = eyesCenter + (_head.rotation * _frustumTilt * _unlockedTilt * angularRotation * Vector3.forward) * _unlockedDistanceJSON.val;
    }

    private Quaternion GetGazeRotation()
    {
        var localAngularVelocity = transform.InverseTransformDirection(_headRB.angularVelocity);
        var angularVelocity = Vector2.Scale(localAngularVelocity * Mathf.Rad2Deg, _angularVelocityPredictiveMultiplier);
        var maxX = _quickTurnMaxXJSON.val;
        var maxY = _quickTurnMaxYJSON.val;
        var clampedAngularVelocity = new Vector2(Mathf.Clamp(angularVelocity.x, -maxX, maxX), Mathf.Clamp(angularVelocity.y, -maxY, maxY));
        var largestClamp = Mathf.Max(maxX, maxY);
        clampedAngularVelocity = Vector2.ClampMagnitude(clampedAngularVelocity / largestClamp, 1f) * largestClamp;
        return Quaternion.Euler(clampedAngularVelocity);
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

            _lockTarget = new EyeTargetCandidate(null, _nullWeightJSON);
            // Rescan projected direction immediately
            _nextObjectsScanTime = 0f;
            _nextLockTargetTime = 0f;
            _nextGazeTime = 0f;
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
            _lockTarget = new EyeTargetCandidate(null, _nullWeightJSON);
            _nextLockTargetTime = float.PositiveInfinity;
        }
        else if(_lockTargetCandidates.Count == 1)
        {
            _lockTarget = _lockTargetCandidates[0];
            _nextLockTargetTime = float.PositiveInfinity;
        }
        else
        {
            var lockRoll = Random.Range(0f, _lockTargetCandidatesScoredWeightSum);
            var lockTarget = new EyeTargetCandidate(null, _nullWeightJSON);
            var sum = 0f;
            for (var i = 0; i < _lockTargetCandidates.Count; i++)
            {
                lockTarget = _lockTargetCandidates[i];
                sum += lockTarget.scoredWeight;
                if (lockRoll < sum) break;
            }
            _lockTarget = lockTarget;
            var gazeDuration = (_lockMaxDurationJSON.val - _lockMinDurationJSON.val) * (lockTarget.weightJSON.val * lockTarget.ratio);
            _nextLockTargetTime = Time.time + Random.Range(_lockMinDurationJSON.val, _lockMinDurationJSON.val + gazeDuration);
        }

        if (_debugJSON.val && UITransform.gameObject.activeInHierarchy)
            UpdateDebugDisplay();
    }

    private void UpdateDebugDisplay()
    {
        _debugDisplaySb.Length = 0;

        // _debugDisplaySb.AppendLine($"lock:{_nextLockTargetTime} obj:{_nextObjectsScanTime}");

        _debugDisplaySb.Append(_lockTargetCandidates.Count);
        _debugDisplaySb.Append(" in focus over ");
        _debugDisplaySb.Append(_objects.Count);
        _debugDisplaySb.Append(" potential targets.");
        _debugDisplaySb.AppendLine();

        if (!ReferenceEquals(_lockTarget.transform, null))
        {
            var fc = _lockTarget.transform.GetComponent<FreeControllerV3>();
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
                _debugDisplaySb.Append(_lockTarget.transform.name);
                _debugDisplaySb.AppendLine("'");
            }
        }
        else
        {
            _debugDisplaySb.AppendLine("Not locked on a target.");
        }

        /*
        foreach (var o in _lockTargetCandidates)
        {
            _debugDisplaySb.AppendLine($"Object: {o.transform.name} {o.weight * 100f:0.00}%");
        }
        */

        _debugDisplayJSON.val = _debugDisplaySb.ToString();
        _debugDisplaySb.Length = 0;
    }

    private void ScanObjects(Vector3 eyesCenter)
    {
        if (_nextObjectsScanTime > Time.time) return;
        _nextObjectsScanTime = Time.time + _objectScanSpan;

        var previousLockTargetCount = _lockTargetCandidates.Count;
        _lockTargetCandidates.Clear();
        _lockTargetCandidatesScoredWeightSum = 0f;

        for (var i = 0; i < _watchedGlanceTargets.Count; i++)
        {
            var glanceTargetReference = _watchedGlanceTargets[i];
            if (glanceTargetReference.onLast == glanceTargetReference.onJSON.val) continue;
            SyncObjects();
            break;
        }

        if (_objects.Count == 0) return;

        // NOTE: Average expected direction and actual direction, since we don't know if the head will stop or not
        var angularRotation = GetGazeRotation();
        var frustumBaseRotation = _head.rotation * _frustumTilt;
        var naturalTarget = frustumBaseRotation * angularRotation * Vector3.forward;
        var headTarget = frustumBaseRotation * Vector3.forward;
        var lookDirection = ((headTarget.normalized + naturalTarget.normalized) / 2f).normalized;

        //var planes = GeometryUtility.CalculateFrustumPlanes(SuperController.singleton.centerCameraTarget.targetCamera);
        CalculateFrustum(eyesCenter, lookDirection, _head.up, _frustumJSON.val * Mathf.Deg2Rad, _frustumRatioJSON.val, _frustumNearJSON.val, _frustumFarJSON.val, _frustumPlanes);

        var bestCandidate = new EyeTargetCandidate(null, _nullWeightJSON);
        for (var i = 0; i < _objects.Count; i++)
        {
            var o = _objects[i];
            Vector3 position;
            try
            {
                position = o.transform.position;
            }
            catch (NullReferenceException)
            {
                _nextObjectsScanTime = 0f;
                return;
            }
            var bounds = new Bounds(position, new Vector3(0.001f, 0.001f, 0.001f));
            if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds)) continue;
            var distance = Vector3.SqrMagnitude(bounds.center - eyesCenter);
            if (distance > _lookAtMirrorDistance) continue;
            if (!IsInAngleRange(eyesCenter, position)) continue;
            // Distance affects weight from 0.5f at far frustum to 1f at near frustum
            const float distanceWeight = 0.5f;
            var distanceScore = 1f - Mathf.Clamp((distance - _frustumNearJSON.val) / (_frustumFarJSON.val - _frustumNearJSON.val), 0f, distanceWeight);
            // Angle affects weight from 0.5f at 20 degrees to 1f at perfect forward
            const float angleWeight = 0.7f;
            var angleScore = (1f - angleWeight) + (1f - (Mathf.Clamp(Vector3.Angle(lookDirection, position - eyesCenter), 0, _frustumJSON.val) / _frustumJSON.val)) * angleWeight;
            var score = distanceScore * angleScore;
            var probabilityWeight = o.weightJSON.val * score;
            var candidate = new EyeTargetCandidate(
                o,
                probabilityWeight
            );
            _lockTargetCandidates.Add(candidate);
            _lockTargetCandidatesScoredWeightSum += probabilityWeight;
            if (o.weightJSON.val > bestCandidate.weightJSON.val)
            {
                bestCandidate = candidate;
            }
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            else if (o.weightJSON.val == bestCandidate.weightJSON.val && o.scoredWeight > bestCandidate.scoredWeight)
            {
                bestCandidate = candidate;
            }
        }

        if (_nothingWeightJSON.val > 0.01f)
        {
            _lockTargetCandidates.Add(new EyeTargetCandidate(
                null,
                _nothingWeightJSON
            ));
            _lockTargetCandidatesScoredWeightSum += _nothingWeightJSON.val;
        }

        if (_lockTargetCandidates.Count > 0)
        {
            if (_objectsInViewChangedExpire < Time.time)
            {
                if (_lockTargetCandidates.Count > previousLockTargetCount)
                {
                    // A better target entered view
                    // ReSharper disable once CompareOfFloatsByEqualityOperator
                    if (ReferenceEquals(_lockTarget.transform, null) || bestCandidate.transform != _lockTarget.transform &&
                        (bestCandidate.weightJSON.val > _lockTarget.weightJSON.val ||
                         bestCandidate.weightJSON.val == _lockTarget.weightJSON.val && bestCandidate.scoredWeight > _lockTarget.scoredWeight))
                    {
                        _objectsInViewChangedExpire = Time.time + _objectsInViewChangedCooldownJSON.val;
                        _lockTarget = bestCandidate;
                        _nextGazeTime = 0f;
                    }

                    if (float.IsPositiveInfinity(_nextLockTargetTime))
                        _nextLockTargetTime = Time.time + Random.Range(_lockMinDurationJSON.val, _lockMaxDurationJSON.val);
                }
                else if (_lockTargetCandidates.Count < previousLockTargetCount)
                {
                    // The current target left view
                    if (_lockTargetCandidates.FindIndex(c => c.transform == _lockTarget.transform) == -1)
                    {
                        _lockTarget = bestCandidate;
                        _nextGazeTime = 0f;
                    }

                    if (float.IsPositiveInfinity(_nextLockTargetTime))
                        _nextLockTargetTime = Time.time + Random.Range(_lockMinDurationJSON.val, _lockMaxDurationJSON.val);
                }
            }
            SetLineColor(_frustumLineRenderer, Color.cyan);
        }
        else
        {
            if (!ReferenceEquals(_lockTarget.transform, null))
                _nextGazeTime = 0f;
            _lockTarget = new EyeTargetCandidate(null, _nullWeightJSON);
            _nextLockTargetTime = float.PositiveInfinity;
            SetLineColor(_frustumLineRenderer, Color.gray);
        }

        if (_debugJSON.val && UITransform.gameObject.activeInHierarchy)
            UpdateDebugDisplay();
    }

    private void FocusOnPlayer()
    {
        _nextObjectsScanTime = Time.time + _lockMaxDurationJSON.val;
        _nextSaccadeTime = Time.time + _saccadeMaxDurationJSON.val;
        _nextMirrorScanTime = Time.time + _lockMaxDurationJSON.val;
        _nextValidateExtremesTime = Time.time + _lockMaxDurationJSON.val;
        _lockTarget = new EyeTargetCandidate(SuperController.singleton.centerCameraTarget.transform, _nullWeightJSON);
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
    private void CalculateFrustum(Vector3 origin, Vector3 direction, Vector3 up, float fovRadians, float viewRatio, float near, float far, Plane[] frustumPlanes)
    {
        var nearCenter = origin + direction * near;
        var farCenter = origin + direction * far;
        var camRight = Vector3.Cross(up, direction).normalized;
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
        frustumPlanes[0] = new Plane(nearTopLeft, farTopLeft, farBottomLeft);
        frustumPlanes[1] = new Plane(nearTopRight, nearBottomRight, farBottomRight);
        frustumPlanes[2] = new Plane(farBottomLeft, farBottomRight, nearBottomRight);
        frustumPlanes[3] = new Plane(farTopLeft, nearTopLeft, nearTopRight);
        frustumPlanes[4] = new Plane(nearBottomRight, nearTopRight, nearTopLeft);
        frustumPlanes[5] = new Plane(farBottomRight, farBottomLeft, farTopLeft);

        if (_frustumLinePoints != null)
        {
            //not needed; 6 points are sufficient to calculate the frustum
            var farTopRight = farCenter + camUp*(farHeight*0.5f) + camRight*(farWidth*0.5f);
            var nearBottomLeft  = nearCenter - camUp*(nearHeight*0.5f) - camRight*(nearWidth*0.5f);

            _frustumLinePoints[0] = nearTopLeft;
            _frustumLinePoints[1] = nearTopRight;
            _frustumLinePoints[2] = farTopRight;
            _frustumLinePoints[3] = nearTopRight;
            _frustumLinePoints[4] = nearBottomRight;
            _frustumLinePoints[5] = farBottomRight;
            _frustumLinePoints[6] = nearBottomRight;
            _frustumLinePoints[7] = nearBottomLeft;
            _frustumLinePoints[8] = farBottomLeft;
            _frustumLinePoints[9] = nearBottomLeft;
            _frustumLinePoints[10] = nearTopLeft;
            _frustumLinePoints[11] = farTopLeft;
            _frustumLinePoints[12] = farTopRight;
            _frustumLinePoints[13] = farBottomRight;
            _frustumLinePoints[14] = farBottomLeft;
            _frustumLinePoints[15] = farTopLeft;
            _frustumLineRenderer.SetPositions(_frustumLinePoints);
        }
    }

    private void ONAtomUIDsChanged(List<string> uids)
    {
        GlanceRescan();
    }

    public override void RestoreFromJSON(JSONClass jc, bool restorePhysical = true, bool restoreAppearance = true, JSONArray presetAtoms = null, bool setMissingToDefault = true)
    {
        if (jc.HasKey("FrustrumFOV"))
        {
            // 1.x backward compatibility
            jc["FrustumFOV"] = jc["FrustrumFOV"];
            jc["FrustumRatio"] = jc["FrustrumRatio"];
            jc["FrustumTilt"] = jc["FrustrumTilt"];
            jc["FrustumNear"] = jc["FrustrumNear"];
            jc["FrustumFar"] = jc["FrustrumFar"];
        }
        base.RestoreFromJSON(jc, restorePhysical, restoreAppearance, presetAtoms, setMissingToDefault);
        _restored = true;
    }

    public void OnBindingsListRequested(List<object> bindings)
    {
        bindings.Add(new[]
        {
                new KeyValuePair<string, string>("Namespace", "Glance")
            });
        bindings.Add(new JSONStorableAction("Toggle_FrustumDebug", () => _debugJSON.val = !_debugJSON.val));
        bindings.Add(new JSONStorableAction("FocusOnPlayer", FocusOnPlayer));
    }

    public void OnDestroy()
    {
        SuperController.singleton.BroadcastMessage("OnActionsProviderDestroyed", this, SendMessageOptions.DontRequireReceiver);
    }

    private struct EyeTargetCandidate
    {
        public readonly Transform transform;
        public readonly JSONStorableFloat weightJSON;
        public readonly float ratio;
        public readonly float scoredWeight;

        public EyeTargetCandidate(Transform transform, JSONStorableFloat weightJSON, float ratio = 1f)
        {
            this.transform = transform;
            this.weightJSON = weightJSON;
            this.ratio = ratio;
            scoredWeight = 0f;
        }

        public EyeTargetCandidate(EyeTargetCandidate source, float scoredWeight)
        {
            transform = source.transform;
            weightJSON = source.weightJSON;
            ratio = source.ratio;
            this.scoredWeight = scoredWeight;
        }
    }

    private struct GlanceTargetReference
    {
        public JSONStorableBool onJSON;
        public bool onLast;
    }
}
