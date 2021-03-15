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
    private const float _objectScanSpan = 0.1f;
    private const float _naturalLookDistance = 0.4f;

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
    private readonly JSONStorableFloat _frustrumJSON = new JSONStorableFloat("FrustrumFOV", 6f, 0f, 45f, true);
    private readonly JSONStorableFloat _gazeMinDurationJSON = new JSONStorableFloat("GazeMinDuration", 0.5f, 0f, 10f, false);
    private readonly JSONStorableFloat _gazeMaxDurationJSON = new JSONStorableFloat("GazeMaxDuration", 2f, 0f, 10f, false);
    private readonly JSONStorableFloat _shakeMinDurationJSON = new JSONStorableFloat("ShakeMinDuration", 0.2f, 0f, 1f, false);
    private readonly JSONStorableFloat _shakeMaxDurationJSON = new JSONStorableFloat("ShakeMaxDuration", 0.5f, 0f, 1f, false);
    private readonly JSONStorableFloat _shakeRangeJSON = new JSONStorableFloat("ShakeRangeDuration", 0.015f, 0f, 0.1f, true);
    private readonly JSONStorableBool _debugJSON = new JSONStorableBool("Debug", false);
    private readonly JSONStorableString _debugDisplayJSON = new JSONStorableString("DebugDisplay", "");

    private bool _ready;
    private bool _restored;
    private DAZBone[] _bones;
    private EyesControl _eyeBehavior;
    private Transform _head;
    private Transform _lEye;
    private Transform _rEye;
    private FreeControllerV3 _eyeTarget;
    private readonly List<BoxCollider> _mirrors = new List<BoxCollider>();
    private readonly List<Transform> _objects = new List<Transform>();
    private Vector3 _eyeTargetRestorePosition;
    private EyesControl.LookMode _eyeBehaviorRestoreLookMode;
    private readonly Plane[] _frustrumPlanes = new Plane[6];
    private readonly List<EyeTargetReference> _lockTargetCandidates = new List<EyeTargetReference>();
    private BoxCollider _lookAtMirror;
    private float _lookAtMirrorDistance;
    private float _nextMirrorScanTime;
    private float _nextObjectsScanTime;
    private float _nextLockTargetTime;
    private float _nextShakeTime;
    private Vector3 _shakeValue;
    private Transform _lockTarget;
    private readonly StringBuilder _debugDisplaySb = new StringBuilder();

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
            _bones = containingAtom.transform.Find("rescale2").GetComponentsInChildren<DAZBone>();
            _head = _bones.First(eye => eye.name == "head").transform;
            _lEye = _bones.First(eye => eye.name == "lEye").transform;
            _rEye = _bones.First(eye => eye.name == "rEye").transform;
            _eyeTarget = containingAtom.freeControllers.First(fc => fc.name == "eyeTargetControl");

            CreateToggle(_trackPlayerJSON);
            CreateToggle(_trackMirrorsJSON);
            CreateToggle(_trackWindowCameraJSON);
            CreateToggle(_trackSelfHandsJSON);
            CreateToggle(_trackSelfGenitalsJSON);
            CreateToggle(_trackPersonsJSON);
            CreateToggle(_trackObjectsJSON);
            CreateToggle(_debugJSON);
            CreateTextField(_debugDisplayJSON);

            CreateSlider(_frustrumJSON, true);
            CreateSlider(_gazeMinDurationJSON, true);
            CreateSlider(_gazeMaxDurationJSON, true);
            CreateSlider(_shakeMinDurationJSON, true);
            CreateSlider(_shakeMaxDurationJSON, true);
            CreateSlider(_shakeRangeJSON, true);

            RegisterBool(_trackPlayerJSON);
            RegisterBool(_trackMirrorsJSON);
            RegisterBool(_trackWindowCameraJSON);
            RegisterBool(_trackSelfHandsJSON);
            RegisterBool(_trackSelfGenitalsJSON);
            RegisterBool(_trackPersonsJSON);
            RegisterBool(_trackObjectsJSON);
            RegisterFloat(_frustrumJSON);
            RegisterFloat(_gazeMinDurationJSON);
            RegisterFloat(_gazeMaxDurationJSON);
            RegisterFloat(_shakeMinDurationJSON);
            RegisterFloat(_shakeMaxDurationJSON);
            RegisterFloat(_shakeRangeJSON);

            _trackMirrorsJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _trackWindowCameraJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _trackSelfHandsJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _trackSelfGenitalsJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _trackPersonsJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _trackObjectsJSON.setCallbackFunction = _ => { if (enabled) Rescan(); };
            _gazeMinDurationJSON.setCallbackFunction = val => _gazeMaxDurationJSON.valNoCallback = Mathf.Max(val, _gazeMaxDurationJSON.val);
            _gazeMaxDurationJSON.setCallbackFunction = val => _gazeMinDurationJSON.valNoCallback = Mathf.Min(val, _gazeMinDurationJSON.val);
            _shakeMinDurationJSON.setCallbackFunction = val => _shakeMaxDurationJSON.valNoCallback = Mathf.Max(val, _shakeMaxDurationJSON.val);
            _shakeMaxDurationJSON.setCallbackFunction = val => _shakeMinDurationJSON.valNoCallback = Mathf.Min(val, _shakeMinDurationJSON.val);

            SuperController.singleton.StartCoroutine(DeferredInit());
        }
        catch (Exception e)
        {
            SuperController.LogError($"{nameof(Glance)}.{nameof(Init)}: {e}");
            enabled = false;
        }
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
            _eyeTargetRestorePosition = _eyeTarget.control.position;
            _eyeBehaviorRestoreLookMode = _eyeBehavior.currentLookMode;

            _eyeBehavior.currentLookMode = EyesControl.LookMode.Target;

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
            SuperController.singleton.onAtomUIDsChangedHandlers -= ONAtomUIDsChanged;

            _eyeTarget.control.position = _eyeTargetRestorePosition;
            if (_eyeBehavior.currentLookMode != EyesControl.LookMode.Target)
                _eyeBehavior.currentLookMode = _eyeBehaviorRestoreLookMode;

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
            _objects.Add(SuperController.singleton.centerCameraTarget.transform);
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
                    _objects.Add(atom.mainController.control);
                    break;
                }
                case "Person":
                {
                    if (atom == containingAtom)
                    {
                        foreach (var bone in _bones)
                        {
                            if (_trackSelfHandsJSON.val && (bone.name == "lHand" || bone.name == "rHand"))
                                _objects.Add(bone.transform);
                            else if (_trackSelfGenitalsJSON.val && (bone.name == "Gen1" || bone.name == "Gen3"))
                                _objects.Add(bone.transform);
                        }

                        continue;
                    }

                    if (!_trackPersonsJSON.val) continue;
                    foreach (var bone in atom.transform.Find("rescale2").GetComponentsInChildren<DAZBone>())
                    {
                        if (!_bonesLookAt.Contains(bone.name)) continue;
                        _objects.Add(bone.transform);
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
                    _objects.Add(atom.mainController.control);
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
        _nextLockTargetTime = 0f;
        _nextShakeTime = 0f;
        _shakeValue = Vector3.zero;
    }

    public void Update()
    {
        var eyesCenter = (_lEye.position + _rEye.position) / 2f;

        ScanMirrors(eyesCenter);
        ScanObjects(eyesCenter);
        SelectLockTarget();

        if (!ReferenceEquals(_lockTarget, null))
        {
            SelectShake();
            _eyeTarget.control.position = _lockTarget.transform.position + _shakeValue;
            return;
        }

        if (!ReferenceEquals(_lookAtMirror, null))
        {
            var mirrorTransform = _lookAtMirror.transform;
            var mirrorPosition = mirrorTransform.position;
            var mirrorNormal = mirrorTransform.up;
            var plane = new Plane(mirrorNormal, mirrorPosition);
            var planePoint = plane.ClosestPointOnPlane(eyesCenter);
            var reflectPosition = planePoint - (eyesCenter - planePoint);
            _eyeTarget.control.position = reflectPosition;
            return;
        }

        _eyeTarget.control.position = eyesCenter + _head.forward * _naturalLookDistance;
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
        _debugDisplaySb.Append(" candidates on ");
        _debugDisplaySb.Append(_objects.Count);
        _debugDisplaySb.Append(" potential targets.");
        _debugDisplaySb.AppendLine();

        if (!ReferenceEquals(_lockTarget, null))
        {
            var fc = _lockTarget.GetComponent<FreeControllerV3>();
            if (!ReferenceEquals(fc, null))
            {
                _debugDisplaySb.Append("Locked on ");
                _debugDisplaySb.Append(fc.name);
                _debugDisplaySb.Append(" of atom ");
                _debugDisplaySb.Append(fc.containingAtom.name);
                _debugDisplaySb.AppendLine();
            }
            else
            {
                _debugDisplaySb.Append("Locked on ");
                _debugDisplaySb.Append(_lockTarget.name);
                _debugDisplaySb.AppendLine();
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
        CalculateFrustum(eyesCenter, _head.forward, _frustrumJSON.val * Mathf.Deg2Rad, 1.3f, 0.35f, 100f, _frustrumPlanes);

        Transform closest = null;
        var closestDistance = float.PositiveInfinity;
        foreach (var o in _objects)
        {
            var position = o.position;
            var bounds = new Bounds(position, new Vector3(0.25f, 0.25f, 0.25f));
            if (!GeometryUtility.TestPlanesAABB(_frustrumPlanes, bounds)) continue;
            var distance = Vector3.SqrMagnitude(bounds.center - eyesCenter);
            if (distance > _lookAtMirrorDistance) continue;
            _lockTargetCandidates.Add(new EyeTargetReference
            {
                transform = o,
                distance = distance
            });
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = o;
            }
        }

        if (_lockTargetCandidates.Count != originalCount)
        {
            if (_lockTargetCandidates.Count > 0)
            {
                _lockTarget = closest;
                _nextLockTargetTime = Time.time + Random.Range(_gazeMinDurationJSON.val, _gazeMaxDurationJSON.val);
            }

            _nextLockTargetTime = 0;
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
    private static void CalculateFrustum(Vector3 origin, Vector3 direction, float fovRadians, float viewRatio, float near, float far, Plane[] frustrumPlanes)
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
        //not needed; 6 points are sufficient to calculate the frustum
        //Vector3 farTopRight = farCenter + camUp*(farHeight*0.5f) + camRight*(farWidth*0.5f);
        var farBottomLeft = farCenter - camUp * (farHeight * 0.5f) - camRight * (farWidth * 0.5f);
        var farBottomRight = farCenter - camUp * (farHeight * 0.5f) + camRight * (farWidth * 0.5f);
        var nearTopLeft = nearCenter + camUp * (nearHeight * 0.5f) - camRight * (nearWidth * 0.5f);
        var nearTopRight = nearCenter + camUp * (nearHeight * 0.5f) + camRight * (nearWidth * 0.5f);
        //not needed; 6 points are sufficient to calculate the frustum
        //Vector3 nearBottomLeft  = nearCenter - camUp*(nearHeight*0.5f) - camRight*(nearWidth*0.5f);
        var nearBottomRight = nearCenter - camUp * (nearHeight * 0.5f) + camRight * (nearWidth * 0.5f);
        frustrumPlanes[0] = new Plane(nearTopLeft, farTopLeft, farBottomLeft);
        frustrumPlanes[1] = new Plane(nearTopRight, nearBottomRight, farBottomRight);
        frustrumPlanes[2] = new Plane(farBottomLeft, farBottomRight, nearBottomRight);
        frustrumPlanes[3] = new Plane(farTopLeft, nearTopLeft, nearTopRight);
        frustrumPlanes[4] = new Plane(nearBottomRight, nearTopRight, nearTopLeft);
        frustrumPlanes[5] = new Plane(farBottomRight, farBottomLeft, farTopLeft);
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
        public float distance;
        public Transform transform;
    }
}
