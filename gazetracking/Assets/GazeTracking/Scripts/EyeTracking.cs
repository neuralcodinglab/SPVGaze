using System;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Assertions;
using Valve.VR;
using ViveSR.anipal;
using ViveSR.anipal.Eye;
using EyeFramework = ViveSR.anipal.Eye.SRanipal_Eye_Framework;

public class EyeTracking : MonoBehaviour
{
    [SerializeField] private LayerMask HallwayLayerMask;

    [Header("Eye Tracking Parameter")]
    [SerializeAs("Sphere Cast Radius")]public float spCastR = 0.01f;
    [SerializeAs("Sphere Cast Max Distance")]public float spCastDist = 20f;
    
    private static EyeData_v2 _eyeData;
    private EyeParameter eyeParameter;
    private bool eyeCallbackRegistered;
    private PhospheneSimulator sim;
    internal bool eyeTrackingAvailable { get; private set; }
    
    private void Start()
    {
        // Headset needs a few frames to register and initialise
        // start register and check functions with a delay
        // ToDo: Is there a "VR-Ready" Event to subscribe to?
        Invoke(nameof(SystemCheck), .5f);
        Invoke(nameof(RegisterCallback), .5f);

        // find reference to simulator
        sim = GetComponent<PhospheneSimulator>();
    }
    
    private void FixedUpdate()
    {
        if (!CheckFrameworkStatusErrors())
        {
            eyeTrackingAvailable = false;
            Debug.LogWarning("Framework Responded failure to work.");
            Release();
        }
    }
    
    private void Update()
    {
        FocusInfo focusInfo;
        // try to get focus point from combined gaze origin
        if (GetFocusPoint(GazeIndex.COMBINE, out focusInfo)) {}
        // if that fails, try to get focus point using left eye
        else if (GetFocusPoint(GazeIndex.LEFT, out focusInfo)) {}
        // if left also fails try right eye
        else if (GetFocusPoint(GazeIndex.RIGHT, out focusInfo)) {} 
        // if all 3 have failed, don't update eye position
        else return;

        // use focus point to update eye position on screen
        CalculateScreenEyePosition(focusInfo.point);
    }

    private void CalculateScreenEyePosition(Vector3 P)
    {
        // projection from local space to clip space
        var lMat = sim.targetCamera.GetStereoNonJitteredProjectionMatrix(Camera.StereoscopicEye.Left);
        var rMat = sim.targetCamera.GetStereoNonJitteredProjectionMatrix(Camera.StereoscopicEye.Right);
        var cMat = sim.targetCamera.nonJitteredProjectionMatrix;
        // projection from world space into local space
        var world2cam = sim.targetCamera.worldToCameraMatrix;
        // 4th dimension necessary in graphics to get scale
        var P4d = new Vector4(P.x, P.y, P.z, 1f); 
        // point in world space * world2cam -> local space point
        // local space point * projection matrix = clip space point
        var lProjection = lMat * world2cam * P4d;
        var rProjection = rMat * world2cam * P4d;
        var cProjection = cMat * world2cam * P4d;
        // scale and shift from clip space [-1,1] into view space [0,1]
        var lViewSpace = (new Vector2(lProjection.x, lProjection.y) / lProjection.w) * .5f + .5f * Vector2.one;
        var rViewSpace = (new Vector2(rProjection.x, rProjection.y) / rProjection.w) * .5f + .5f * Vector2.one;
        var cViewSpace = (new Vector2(cProjection.x, cProjection.y) / cProjection.w) * .5f + .5f * Vector2.one;

        // ToDo: Update only when valid; Consider gaze smoothing; Play with SrAnipal GazeParameter
        sim.SetEyePosition(lViewSpace, rViewSpace, cViewSpace);
    }

    #region EyeData Callback: Registration & Clean-Up
    
    [MonoPInvokeCallback]
    private static void EyeCallback(ref EyeData_v2 eyeDataRef)
    {
        var eyeParameter = new EyeParameter();
        SRanipal_Eye_API.GetEyeParameter(ref eyeParameter);
        _eyeData = eyeDataRef;
    }

    private void SystemCheck()
    {
        if (EyeFramework.Status == EyeFramework.FrameworkStatus.NOT_SUPPORT)
        {
            eyeTrackingAvailable = false;
            Debug.LogWarning("Eye Tracking Not Supported");
            return;
        }
        

        var eyeDataResult = SRanipal_Eye_API.GetEyeData_v2(ref _eyeData);
        var eyeParamResult = SRanipal_Eye_API.GetEyeParameter(ref eyeParameter);
        var resultEyeInit = SRanipal_API.Initial(SRanipal_Eye_v2.ANIPAL_TYPE_EYE_V2, IntPtr.Zero);
        
        if (
            eyeDataResult != ViveSR.Error.WORK ||
            eyeParamResult != ViveSR.Error.WORK ||
            resultEyeInit != ViveSR.Error.WORK
        )
        {
            Debug.LogError("Inital Check failed.\n" +
                           $"[SRanipal] Eye Data Call v2 : {eyeDataResult}" +
                           $"[SRanipal] Eye Param Call v2: {eyeParamResult}" +
                           $"[SRanipal] Initial Eye v2   : {resultEyeInit}"
            );
            eyeTrackingAvailable = false;
            return;
        }

        eyeTrackingAvailable = true;
    }

    private void RegisterCallback()
    {
        var eyeParameter = new EyeParameter();
        SRanipal_Eye_API.GetEyeParameter(ref eyeParameter);

        if (SRanipal_Eye_Framework.Instance.EnableEyeDataCallback && !eyeCallbackRegistered)
        {
            SRanipal_Eye_v2.WrapperRegisterEyeDataCallback(
                Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)EyeCallback)
            );
            eyeCallbackRegistered = true;
        }

        else if (!SRanipal_Eye_Framework.Instance.EnableEyeDataCallback && eyeCallbackRegistered)
        {
            SRanipal_Eye_v2.WrapperUnRegisterEyeDataCallback(
                Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)EyeCallback)
            );
            eyeCallbackRegistered = false;
        }
    }
    
    private bool CheckFrameworkStatusErrors()
    {
        return EyeFramework.Status == EyeFramework.FrameworkStatus.WORKING;
    }
    
    void OnApplicationQuit()
    {
        Release();
    }

    private void OnDisable()
    {
        Release();
    }
    
    /// <summary>
    /// Release callback thread when disabled or quit
    /// </summary>
    private void Release()
    {
        if (eyeCallbackRegistered)
        {
            SRanipal_Eye_v2.WrapperUnRegisterEyeDataCallback(
                Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)EyeCallback)
            );
            eyeCallbackRegistered = false;
        }
    }
    
    /// <summary>
    /// Required class for IL2CPP scripting backend support
    /// </summary>
    internal class MonoPInvokeCallbackAttribute : Attribute { }
    #endregion
    
    /// <summary>
    /// Adapted from SRanipal implementation. Rewritten to be more specific and thus more efficient.
    /// Casts a ray against all colliders when enable eye callback function.
    /// </summary>
    /// <param name="index">A source of eye gaze data.</param>
    /// <param name="focusInfo">Information about where the ray focused on.</param>
    /// <returns>Indicates whether the ray hits a collider.</returns>
    private bool GetFocusPoint(GazeIndex index, out FocusInfo focusInfo)
    {
        SingleEyeData eye_data = index switch
        {
            GazeIndex.LEFT => _eyeData.verbose_data.left,
            GazeIndex.RIGHT => _eyeData.verbose_data.right,
            GazeIndex.COMBINE => _eyeData.verbose_data.combined.eye_data,
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, null)
        };
        bool valid = eye_data.GetValidity(SingleEyeDataValidity.SINGLE_EYE_DATA_GAZE_DIRECTION_VALIDITY);

        if (!valid)
        {
            focusInfo = new FocusInfo();
        }
        else
        {
            Vector3 direction = eye_data.gaze_direction_normalized;
            direction.x *= -1;

            Ray rayGlobal = new Ray(sim.targetCamera.transform.position,
                sim.targetCamera.transform.TransformDirection(direction));
            RaycastHit hit;
            if (spCastR == 0) valid = Physics.Raycast(rayGlobal, out hit, spCastDist, HallwayLayerMask);
            else valid = Physics.SphereCast(rayGlobal, spCastR, out hit, spCastDist, HallwayLayerMask);
            focusInfo = new FocusInfo
            {
                point = hit.point,
                normal = hit.normal,
                distance = hit.distance,
                collider = hit.collider,
                rigidbody = hit.rigidbody,
                transform = hit.transform
            };
        }

        return valid;
    }
}