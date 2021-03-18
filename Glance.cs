// TODO: Change width ratio
// TODO: Vertical / Horizontal offset (look down)
// TODO: Look at empties?
// TODO: Player, look down
// TODO: Snap when looking away, still apply randomize (e.g. random spots in the frustrum)
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
    private const float _validateExtremesSpan = 0.04f;
    private const float _naturalLookDistance = 0.8f;
    private const float _angularVelocityPredictiveMultiplier = 0.5f;

    private static readonly HashSet<string> _mirrorAtomTypes = new HashSet<string>(new[]
    {
        "Glass",
        "Glass-Stained",
        "ReflectiveSlate",
        "ReflectiveWoodPanel",
    });

    private static readonly HashSet<string> _bonesLookAt = new HashSet<string>
    {
        "lHand",
        "rHand",
        "lEye",
        "rEye",
        "tongue03",
        "abdomen",
        "chest",
        "pelvis",
        "Gen3",
        "Testes",
    };

    private readonly JSONStorableBool _trackPlayerJSON = new JSONStorableBool("TrackPlayer", true);
    private readonly JSONStorableBool _trackMirrorsJSON = new JSONStorableBool("TrackMirrors", false);
    private readonly JSONStorableBool _trackWindowCameraJSON = new JSONStorableBool("TrackWindowCamera", false);
    private readonly JSONStorableBool _trackSelfHandsJSON = new JSONStorableBool("TrackSelfHands", false);
    private readonly JSONStorableBool _trackSelfGenitalsJSON = new JSONStorableBool("TrackSelfGenitals", true);
    private readonly JSONStorableBool _trackPersonsJSON = new JSONStorableBool("TrackPersons", true);
    private readonly JSONStorableBool _trackObjectsJSON = new JSONStorableBool("TrackObjects", true);
    private readonly JSONStorableFloat _frustrumJSON = new JSONStorableFloat("FrustrumFOV", 16f, 0f, 45f, true);
    private readonly JSONStorableFloat _frustrumRatioJSON = new JSONStorableFloat("FrustrumRatio", 1.3f, 0.5f, 2f, true);
    private readonly JSONStorableFloat _frustrumRotateJSON = new JSONStorableFloat("FrustrumRotate", -5f, -45f, 45f, true);
    private readonly JSONStorableFloat _frustrumNearJSON = new JSONStorableFloat("FrustrumNear", 0.35f, 0f, 5f, false);
    private readonly JSONStorableFloat _frustrumFarJSON = new JSONStorableFloat("FrustrumFar", 5f, 0f, 5f, false);
    private readonly JSONStorableFloat _gazeMinDurationJSON = new JSONStorableFloat("GazeMinDuration", 0.5f, 0f, 10f, false);
    private readonly JSONStorableFloat _gazeMaxDurationJSON = new JSONStorableFloat("GazeMaxDuration", 2f, 0f, 10f, false);
    private readonly JSONStorableFloat _shakeMinDurationJSON = new JSONStorableFloat("ShakeMinDuration", 0.2f, 0f, 1f, false);
    private readonly JSONStorableFloat _shakeMaxDurationJSON = new JSONStorableFloat("ShakeMaxDuration", 0.5f, 0f, 1f, false);
    private readonly JSONStorableFloat _shakeRangeJSON = new JSONStorableFloat("ShakeRange", 0.015f, 0f, 0.1f, true);
    private readonly JSONStorableFloat _quickTurnThresholdJSON = new JSONStorableFloat("QuickTurnThreshold", 3f, 0f, 10f, false);
    private readonly JSONStorableFloat _quickTurnCooldownJSON = new JSONStorableFloat("QuickTurnCooldown", 0.5f, 0f, 2f, false);
    private readonly JSONStorableBool _debugJSON = new JSONStorableBool("Debug", false);
    private readonly JSONStorableString _debugDisplayJSON = new JSONStorableString("DebugDisplay", "");

    private bool _ready;
    private bool _restored;
    private DAZBone[] _bones;
    private EyesControl _eyeBehavior;
    private DAZMeshEyelidControl _eyelidBehavior;
    private Transform _head;
    private Transform _lEye;
    private LookAtWithLimits _lEyeLimits;
    private LookAtWithLimits _rEyeLimits;
    private Transform _rEye;
    private Rigidbody _headRB;
    private FreeControllerV3 _eyeTarget;
    private Quaternion _frustrumRotation = Quaternion.Euler(-5f, 0f, 0f);
    private readonly List<BoxCollider> _mirrors = new List<BoxCollider>();
    private readonly List<EyeTargetReference> _objects = new List<EyeTargetReference>();
    private Vector3 _eyeTargetRestorePosition;
    private EyesControl.LookMode _eyeBehaviorRestoreLookMode;
    private bool _blinkRestoreEnabled;
    private readonly Plane[] _frustrumPlanes = new Plane[6];
    private readonly List<EyeTargetReference> _lockTargetCandidates = new List<EyeTargetReference>();
    private float _nextMirrorScanTime;
    private BoxCollider _lookAtMirror;
    private float _lookAtMirrorDistance;
    private float _nextObjectsScanTime;
    private float _nextValidateExtremesTime;
    private float _nextLockTargetTime;
    private Transform _lockTarget;
    private float _nextShakeTime;
    private Vector3 _shakeValue;
    private float _nextGazeTime;
    private Vector3 _gazeTarget;
    private float _angularVelocityBurstCooldown;
    private readonly StringBuilder _debugDisplaySb = new StringBuilder();
    private LineRenderer _lineRenderer;
    private Vector3[] _lineRendererPoints;

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
            _head = _bones.First(eye => eye.name == "head").transform;
            var lEyeBone = _bones.First(eye => eye.name == "lEye");
            _lEye = lEyeBone.transform;
            _lEyeLimits = lEyeBone.GetComponent<LookAtWithLimits>();
            var rEyeBone = _bones.First(eye => eye.name == "rEye");
            _rEye = rEyeBone.transform;
            _rEyeLimits = rEyeBone.GetComponent<LookAtWithLimits>();
            _headRB = _head.GetComponent<Rigidbody>();
            _eyeTarget = containingAtom.freeControllers.First(fc => fc.name == "eyeTargetControl");

            CreateToggle(_trackPlayerJSON).label = "Player (camera)";
            CreateToggle(_trackMirrorsJSON).label = "Mirrors (look at themselves)";
            CreateToggle(_trackWindowCameraJSON).label = "Window camera";
            CreateToggle(_trackSelfHandsJSON).label = "Hands (self)";
            CreateToggle(_trackSelfGenitalsJSON).label = "Genitals (self)";
            CreateToggle(_trackPersonsJSON).label = "Persons (eyes, hands, gens)";
            CreateToggle(_trackObjectsJSON).label = "Objects (toys, cua, shapes)";
            CreateToggle(_debugJSON).label = "Show debug information";
            CreateTextField(_debugDisplayJSON);

            CreateSlider(_frustrumJSON, true).label = "Frustrum field of view";
            CreateSlider(_frustrumRatioJSON, true).label = "Frustrum ratio (multiply width)";
            CreateSlider(_frustrumRotateJSON, true).label = "Frustrum rotation (tilt)";
            CreateSlider(_frustrumNearJSON, true).label = "Frustrum near (closest)";
            CreateSlider(_frustrumFarJSON, true).label = "Frustrum far (furthest)";
            CreateSlider(_gazeMinDurationJSON, true).label = "Min target lock time";
            CreateSlider(_gazeMaxDurationJSON, true).label = "Max target lock time";
            CreateSlider(_shakeMinDurationJSON, true).label = "Min eye saccade time";
            CreateSlider(_shakeMaxDurationJSON, true).label = "Max eye saccade time";
            CreateSlider(_shakeRangeJSON, true).label = "Range of eye saccade";
            CreateSlider(_quickTurnThresholdJSON, true).label = "Quick turn threshold";
            CreateSlider(_quickTurnCooldownJSON, true).label = "Quick turn cooldown";

            RegisterBool(_trackPlayerJSON);
            RegisterBool(_trackMirrorsJSON);
            RegisterBool(_trackWindowCameraJSON);
            RegisterBool(_trackSelfHandsJSON);
            RegisterBool(_trackSelfGenitalsJSON);
            RegisterBool(_trackPersonsJSON);
            RegisterBool(_trackObjectsJSON);
            RegisterFloat(_frustrumJSON);
            RegisterFloat(_frustrumRatioJSON);
            RegisterFloat(_frustrumRotateJSON);
            RegisterFloat(_frustrumNearJSON);
            RegisterFloat(_frustrumFarJSON);
            RegisterFloat(_gazeMinDurationJSON);
            RegisterFloat(_gazeMaxDurationJSON);
            RegisterFloat(_shakeMinDurationJSON);
            RegisterFloat(_shakeMaxDurationJSON);
            RegisterFloat(_shakeRangeJSON);
            RegisterFloat(_quickTurnThresholdJSON);
            RegisterFloat(_quickTurnCooldownJSON);

            _trackPlayerJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _trackMirrorsJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _trackWindowCameraJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _trackSelfHandsJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _trackSelfGenitalsJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _trackPersonsJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _trackObjectsJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _frustrumRotateJSON.setCallbackFunction = val => _frustrumRotation = Quaternion.Euler(_frustrumRotateJSON.val, 0f, 0f);
            _frustrumNearJSON.setCallbackFunction = val => _frustrumFarJSON.valNoCallback = Mathf.Max(val, _frustrumFarJSON.val);
            _frustrumFarJSON.setCallbackFunction = val => _frustrumNearJSON.valNoCallback = Mathf.Min(val, _frustrumNearJSON.val);
            _gazeMinDurationJSON.setCallbackFunction = val => _gazeMaxDurationJSON.valNoCallback = Mathf.Max(val, _gazeMaxDurationJSON.val);
            _gazeMaxDurationJSON.setCallbackFunction = val => _gazeMinDurationJSON.valNoCallback = Mathf.Min(val, _gazeMinDurationJSON.val);
            _shakeMinDurationJSON.setCallbackFunction = val => _shakeMaxDurationJSON.valNoCallback = Mathf.Max(val, _shakeMaxDurationJSON.val);
            _shakeMaxDurationJSON.setCallbackFunction = val => _shakeMinDurationJSON.valNoCallback = Mathf.Min(val, _shakeMinDurationJSON.val);
            _debugJSON.setCallbackFunction = SyncLineRenderer;

            SuperController.singleton.StartCoroutine(DeferredInit());
        }
        catch (Exception e)
        {
            SuperController.LogError($"{nameof(Glance)}.{nameof(Init)}: {e}");
            enabled = false;
        }
    }

    private void SyncLineRenderer(bool val)
    {
        var exists = _lineRenderer != null;
        if (!val)
        {
            if (exists) Destroy(_lineRenderer.gameObject);
            _lineRendererPoints = null;
            return;
        }
        if (exists) return;
        var go = new GameObject("Gaze_LineRenderer");
        _lineRenderer = go.AddComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.material = new Material(Shader.Find("Sprites/Default")) {renderQueue = 4000};
        _lineRenderer.colorGradient = new Gradient
        {
            colorKeys = new[] {new GradientColorKey(Color.cyan, 0f), new GradientColorKey(Color.cyan, 1f)}
        };
        _lineRenderer.widthMultiplier = 0.0004f;
        _lineRenderer.positionCount = 16;
        _lineRendererPoints = new Vector3[16];
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
            Rescan();

            _eyeTargetRestorePosition = _eyeTarget.control.position;
            _eyeBehaviorRestoreLookMode = _eyeBehavior.currentLookMode;
            _eyeBehavior.currentLookMode = EyesControl.LookMode.Target;

            _blinkRestoreEnabled = _eyelidBehavior.GetBoolParamValue("blinkEnabled");

            SuperController.singleton.onAtomUIDsChangedHandlers += ONAtomUIDsChanged;
        }
        catch (Exception e)
        {
            SuperController.LogError($"{nameof(Glance)}.{nameof(OnEnable)}: {e}");
        }
    }

    public void OnDisable()
    {
        try
        {
            _debugJSON.val = false;

            SuperController.singleton.onAtomUIDsChangedHandlers -= ONAtomUIDsChanged;

            _eyeTarget.control.position = _eyeTargetRestorePosition;
            if (_eyeBehavior.currentLookMode != EyesControl.LookMode.Target)
                _eyeBehavior.currentLookMode = _eyeBehaviorRestoreLookMode;
            _eyelidBehavior.SetBoolParamValue("blinkEnabled", _blinkRestoreEnabled);

            ClearState();
        }
        catch (Exception e)
        {
            SuperController.LogError($"{nameof(Glance)}.{nameof(OnDisable)}: {e}");
        }
    }

    private void SyncMirrors()
    {
        _mirrors.Clear();

        if (!_trackMirrorsJSON.val) return;

        _mirrors.AddRange(SuperController.singleton.GetAtoms()
            .Where(a => _mirrorAtomTypes.Contains(a.type))
            .Where(a => a.on)
            .Select(a => a.GetComponentInChildren<BoxCollider>())
            .Where(c => c != null));
    }

    private void SyncObjects()
    {
        _objects.Clear();

        if (_trackPlayerJSON.val)
        {
            _objects.Add(new EyeTargetReference(SuperController.singleton.centerCameraTarget.transform));
        }

        foreach (var atom in SuperController.singleton.GetAtoms())
        {
            if (!atom.on) continue;

            switch (atom.type)
            {
                case "WindowCamera":
                {
                    if (!_trackWindowCameraJSON.val) continue;
                    if (atom.GetStorableByID("CameraControl")?.GetBoolParamValue("cameraOn") != true) continue;
                    _objects.Add(new EyeTargetReference(atom.mainController.control));
                    break;
                }
                case "Person":
                {
                    if (atom == containingAtom)
                    {
                        foreach (var bone in _bones)
                        {
                            if (_trackSelfHandsJSON.val && (bone.name == "lHand" || bone.name == "rHand"))
                                _objects.Add(new EyeTargetReference(bone.transform));
                            else if (_trackSelfGenitalsJSON.val && (bone.name == "Gen1" || bone.name == "Gen3"))
                                _objects.Add(new EyeTargetReference(bone.transform));
                        }

                        continue;
                    }

                    if (!_trackPersonsJSON.val) continue;
                    foreach (var bone in atom.transform.Find("rescale2").GetComponentsInChildren<DAZBone>())
                    {
                        if (!_bonesLookAt.Contains(bone.name)) continue;
                        _objects.Add(new EyeTargetReference(bone.transform));
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
                    if (!_trackObjectsJSON.val) continue;
                    _objects.Add(new EyeTargetReference(atom.mainController.control));
                    break;
                }
                case "Empty":
                {
                    if (atom.storeId.StartsWith("GlanceTarget_"))
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
        _mirrors.Clear();
        _objects.Clear();
        _lockTargetCandidates.Clear();
        _nextMirrorScanTime = 0f;
        _nextObjectsScanTime = 0f;
        _nextValidateExtremesTime = 0f;
        _nextLockTargetTime = 0f;
        _nextShakeTime = 0f;
        _shakeValue = Vector3.zero;
        _nextGazeTime = 0f;
        _gazeTarget = Vector3.zero;
        _angularVelocityBurstCooldown = 0f;
    }

    public void Update()
    {
        var eyesCenter = (_lEye.position + _rEye.position) / 2f;

        ScanMirrors(eyesCenter);
        ScanObjects(eyesCenter);
        InvalidateExtremes();
        SelectLockTarget();
        SelectShake();

        if (!ReferenceEquals(_lockTarget, null))
        {
            _eyeTarget.control.position = _lockTarget.transform.position + _shakeValue;
            return;
        }

        if (!ReferenceEquals(_lookAtMirror, null))
        {
            var reflectPosition = ComputeMirrorLookback(eyesCenter);
            _eyeTarget.control.position = reflectPosition + _shakeValue;
            return;
        }

        SelectGazeTarget(eyesCenter);
        _eyeTarget.control.position = _gazeTarget + _shakeValue;
    }

    private void InvalidateExtremes()
    {
        if (_nextValidateExtremesTime > Time.time) return;
        _nextValidateExtremesTime = Time.time + _validateExtremesSpan;

        if (AreEyesInRange()) return;

        // TODO: Doesn't seem to be called in practice?
        _nextGazeTime = 0f;
        _nextLockTargetTime = 0f;
        _angularVelocityBurstCooldown = _quickTurnCooldownJSON.val;
    }

    private bool AreEyesInRange()
    {
        if (!IsEyeInRange(_lEye, _lEyeLimits)) return false;
        if (!IsEyeInRange(_rEye, _rEyeLimits)) return false;
        return true;
    }

    private bool IsEyeInRange(Transform eye, LookAtWithLimits limits)
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

        return true;
    }

    private bool IsInAngleRange(Vector3 eyesCenter, Vector3 targetPosition)
    {
        var lookAngle = _head.InverseTransformDirection(targetPosition - eyesCenter);
        var yaw = Vector3.Angle(Vector3.ProjectOnPlane(lookAngle, Vector3.up), Vector3.forward);
        if (yaw > 26) return false;
        var pitch = Vector3.Angle(Vector3.ProjectOnPlane(lookAngle, Vector3.right), Vector3.forward);
        if (pitch > 20) return false;
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
        // Immediate recompute if the head moves fast
        if (_angularVelocityBurstCooldown != 0)
        {
            if (_angularVelocityBurstCooldown > Time.time) return;
            _angularVelocityBurstCooldown = 0f;
        }

        if (_headRB.angularVelocity.sqrMagnitude > _quickTurnThresholdJSON.val)
        {
            _angularVelocityBurstCooldown = Time.time + _quickTurnCooldownJSON.val;
            _nextGazeTime = 0f;
            _eyelidBehavior.Blink();
        }

        if (_nextGazeTime > Time.time) return;
        _nextGazeTime = Time.time + Random.Range(_gazeMinDurationJSON.val, _gazeMaxDurationJSON.val);

        var localAngularVelocity = transform.InverseTransformDirection(_headRB.angularVelocity);
        var angularVelocity = Quaternion.Euler(localAngularVelocity * Mathf.Rad2Deg * _angularVelocityPredictiveMultiplier);

        _gazeTarget = eyesCenter + (_head.rotation * _frustrumRotation * angularVelocity * Vector3.forward) * _naturalLookDistance;
    }

    private void SelectShake()
    {
        if (_nextShakeTime > Time.time) return;
        _nextShakeTime = Time.time + Random.Range(_shakeMinDurationJSON.val, _shakeMaxDurationJSON.val);

        _shakeValue = Random.insideUnitSphere * _shakeRangeJSON.val;
    }

    private void SelectLockTarget()
    {
        if (_nextLockTargetTime > Time.time) return;
        _nextLockTargetTime = Time.time + Random.Range(_gazeMinDurationJSON.val, _gazeMaxDurationJSON.val);

        _lockTarget = _lockTargetCandidates.Count > 0
            ? _lockTargetCandidates[Random.Range(0, _lockTargetCandidates.Count)].transform
            : null;

        if (_debugJSON.val && UITransform.gameObject.activeInHierarchy) UpdateDebugDisplay();

        _shakeValue = Vector3.zero;
        _nextShakeTime = Time.time + Random.Range(_shakeMinDurationJSON.val, _shakeMaxDurationJSON.val);
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

        if (_objects.Count == 0) return;

        var originalCount = _lockTargetCandidates.Count;
        _lockTargetCandidates.Clear();

        //var planes = GeometryUtility.CalculateFrustumPlanes(SuperController.singleton.centerCameraTarget.targetCamera);
        CalculateFrustum(eyesCenter, _head.rotation * _frustrumRotation * Vector3.forward, _frustrumJSON.val * Mathf.Deg2Rad, _frustrumRatioJSON.val, _frustrumNearJSON.val, _frustrumFarJSON.val, _frustrumPlanes);

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
            _lockTargetCandidates.Add(new EyeTargetReference(
                o.transform,
                o.score - distance
            ));
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = o.transform;
            }
        }

        if (_lockTargetCandidates.Count != originalCount)
        {
            if (_lockTargetCandidates.Count > 0)
            {
                _lockTarget = closest;
                _nextLockTargetTime = Time.time + Random.Range(_gazeMinDurationJSON.val, _gazeMaxDurationJSON.val);
            }
            else
            {
                _nextLockTargetTime = 0;
                _nextGazeTime = 0;
            }
        }
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

        if (_lineRendererPoints != null)
        {
            //not needed; 6 points are sufficient to calculate the frustum
            var farTopRight = farCenter + camUp*(farHeight*0.5f) + camRight*(farWidth*0.5f);
            var nearBottomLeft  = nearCenter - camUp*(nearHeight*0.5f) - camRight*(nearWidth*0.5f);

            _lineRendererPoints[0] = nearTopLeft;
            _lineRendererPoints[1] = nearTopRight;
            _lineRendererPoints[2] = farTopRight;
            _lineRendererPoints[3] = nearTopRight;
            _lineRendererPoints[4] = nearBottomRight;
            _lineRendererPoints[5] = farBottomRight;
            _lineRendererPoints[6] = nearBottomRight;
            _lineRendererPoints[7] = nearBottomLeft;
            _lineRendererPoints[8] = farBottomLeft;
            _lineRendererPoints[9] = nearBottomLeft;
            _lineRendererPoints[10] = nearTopLeft;
            _lineRendererPoints[11] = farTopLeft;
            _lineRendererPoints[12] = farTopRight;
            _lineRendererPoints[13] = farBottomRight;
            _lineRendererPoints[14] = farBottomLeft;
            _lineRendererPoints[15] = farTopLeft;
            _lineRenderer.SetPositions(_lineRendererPoints);
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
        public float score;

        public EyeTargetReference(Transform transform, float score = 1f)
        {
            this.transform = transform;
            this.score = score;
        }
    }
}
