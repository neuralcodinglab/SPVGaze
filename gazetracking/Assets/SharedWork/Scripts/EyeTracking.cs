using System;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Assertions;
using ViveSR.anipal;
using ViveSR.anipal.Eye;
using EyeFramework = ViveSR.anipal.Eye.SRanipal_Eye_Framework;

public class EyeTracking : MonoBehaviour
{
    [SerializeField] private LayerMask HallwayLayerMask;

    [Header("Eye Tracking Parameter")]
    [SerializeAs("Sphere Cast Radius")]public float spCastR = 0.01f;
    [SerializeAs("Sphere Cast Max Distance")]public float spCastDist = 20f;
    
    private static EyeData_v2 eyeData;
    private EyeParameter eye_parameter;
    private bool eye_callback_registered;
    private PhospheneSimulator sim;
    internal bool EyeTrackingAvailable { get; private set; }
    
    private void Start()
    {
        Invoke(nameof(SystemCheck), .5f);
        Invoke(nameof(RegisterCallback), .5f);

        sim = GetComponent<PhospheneSimulator>();
    }
    
    private void FixedUpdate()
    {
        if (!CheckFrameworkStatusErrors())
        {
            EyeTrackingAvailable = false;
            // Debug.LogWarning("Framework Responded failure to work.");
        }
    }
    
    private void Update()
    {
        FocusInfo focusInfo;
        if (GetFocusPoint(GazeIndex.COMBINE, out focusInfo)) { }
        else if (GetFocusPoint(GazeIndex.LEFT, out focusInfo)) { }
        else if (GetFocusPoint(GazeIndex.RIGHT, out focusInfo)) { }
        else return;

        CalculateScreenEyePosition(focusInfo.point);
    }

    private void CalculateScreenEyePosition(Vector3 P)
    {
        // projection from local space to clip space
        var lMat = sim.targetCamera.GetStereoNonJitteredProjectionMatrix(Camera.StereoscopicEye.Left);
        var rMat = sim.targetCamera.GetStereoNonJitteredProjectionMatrix(Camera.StereoscopicEye.Right);
        var cMat = sim.targetCamera.nonJitteredProjectionMatrix;
        // projection from world space into local space
        var w2c = sim.targetCamera.worldToCameraMatrix;
        // world space * w2c -> local space; local space * projection = clip space
        var P4d = new Vector4(P.x, P.y, P.z, 1f); // 4th dimension necessary in graphics to get scale
        var lProjection = lMat * w2c * P4d;
        var rProjection = rMat * w2c * P4d;
        var cProjection = rMat * w2c * P4d;
        // scale and shift into view space
        var lViewSpace = (new Vector2(lProjection.x, lProjection.y) / lProjection.w) * .5f + .5f * Vector2.one;
        var rViewSpace = (new Vector2(rProjection.x, rProjection.y) / rProjection.w) * .5f + .5f * Vector2.one;
        var cViewSpace = (new Vector2(cProjection.x, cProjection.y) / cProjection.w) * .5f + .5f * Vector2.one;

        // ToDo: Update only when valid; Consider gaze smoothing; Play with SrAnipal GazeParameter
        sim.SetEyePosition(lViewSpace, rViewSpace, cViewSpace);
    }

    #region EyeData Callback & Registration
    
    [MonoPInvokeCallback]
    private static void EyeCallback(ref EyeData_v2 eyeDataRef)
    {
        var eyeParameter = new EyeParameter();
        SRanipal_Eye_API.GetEyeParameter(ref eyeParameter);
        eyeData = eyeDataRef;
    }

    private void SystemCheck()
    {
        if (EyeFramework.Status == EyeFramework.FrameworkStatus.NOT_SUPPORT)
        {
            EyeTrackingAvailable = false;
            Debug.LogWarning("Eye Tracking Not Supported");
            return;
        }
        

        var eyeDataResult = SRanipal_Eye_API.GetEyeData_v2(ref eyeData);
        var eyeParamResult = SRanipal_Eye_API.GetEyeParameter(ref eye_parameter);
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
            EyeTrackingAvailable = false;
            return;
        }

        EyeTrackingAvailable = true;
    }

    private void RegisterCallback()
    {
        var eyeParameter = new EyeParameter();
        SRanipal_Eye_API.GetEyeParameter(ref eyeParameter);

        if (SRanipal_Eye_Framework.Instance.EnableEyeDataCallback && !eye_callback_registered)
        {
            SRanipal_Eye_v2.WrapperRegisterEyeDataCallback(
                Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)EyeCallback)
            );
            eye_callback_registered = true;
        }

        else if (!SRanipal_Eye_Framework.Instance.EnableEyeDataCallback && eye_callback_registered)
        {
            SRanipal_Eye_v2.WrapperUnRegisterEyeDataCallback(
                Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)EyeCallback)
            );
            eye_callback_registered = false;
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
        if (eye_callback_registered)
        {
            SRanipal_Eye_v2.WrapperUnRegisterEyeDataCallback(
                Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)EyeCallback)
            );
            eye_callback_registered = false;
        }
    }
    
    /// <summary>
    /// Required class for IL2CPP scripting backend support
    /// </summary>
    internal class MonoPInvokeCallbackAttribute : Attribute { }
    #endregion
    
    /// <summary>
    /// Casts a ray against all colliders when enable eye callback function.
    /// </summary>
    /// <param name="index">A source of eye gaze data.</param>
    /// <param name="ray">The starting point and direction of the ray.</param>
    /// <param name="focusInfo">Information about where the ray focused on.</param>
    /// <param name="radius">The radius of the gaze ray</param>
    /// <param name="maxDistance">The max length of the ray.</param>
    /// <param name="focusableLayer">A layer id that is used to selectively ignore object.</param>
    /// <param name="eye_data">ViveSR.anipal.Eye.EyeData_v2. </param>
    /// <returns>Indicates whether the ray hits a collider.</returns>
    private bool GetFocusPoint(GazeIndex index, out FocusInfo focusInfo)
    {
        SingleEyeData eye_data = index switch
        {
            GazeIndex.LEFT => eyeData.verbose_data.left,
            GazeIndex.RIGHT => eyeData.verbose_data.right,
            GazeIndex.COMBINE => eyeData.verbose_data.combined.eye_data,
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
    
    /// <summary>
    /// Gets the gaze ray of a source of eye gaze data when enable eye callback function.
    /// </summary>
    /// <param name="gazeIndex">The index of a source of eye gaze data.</param>
    /// <param name="origin">The starting point of the ray in local coordinates.</param>
    /// <param name="direction">Tthe direction of the ray.</param>
    /// <param name="eye_data">ViveSR.anipal.Eye.EyeData_v2. </param>
    /// <returns>Indicates whether the eye gaze data received is valid.</returns>
    public static bool GetGazeRay(GazeIndex gazeIndex, out Ray ray, EyeData_v2 eye_data)
    {
        bool valid = false;
        Vector3 origin= Vector3.zero, direction = Vector3.zero;
        {
            SingleEyeData[] eyesData = new SingleEyeData[(int)GazeIndex.COMBINE + 1];
            eyesData[(int)GazeIndex.LEFT] = eye_data.verbose_data.left;
            eyesData[(int)GazeIndex.RIGHT] = eye_data.verbose_data.right;
            eyesData[(int)GazeIndex.COMBINE] = eye_data.verbose_data.combined.eye_data;

            if (gazeIndex == GazeIndex.COMBINE)
            {
                valid = eyesData[(int)GazeIndex.COMBINE].GetValidity(SingleEyeDataValidity.SINGLE_EYE_DATA_GAZE_DIRECTION_VALIDITY);
                if (valid)
                {
                    origin = eyesData[(int)GazeIndex.COMBINE].gaze_origin_mm * 0.001f;
                    direction = eyesData[(int)GazeIndex.COMBINE].gaze_direction_normalized;
                    direction.x *= -1;
                }
            }
            else if (gazeIndex == GazeIndex.LEFT || gazeIndex == GazeIndex.RIGHT)
            {
                valid = eyesData[(int)gazeIndex].GetValidity(SingleEyeDataValidity.SINGLE_EYE_DATA_GAZE_DIRECTION_VALIDITY);
                if (valid)
                {
                    origin = eyesData[(int)gazeIndex].gaze_origin_mm * 0.001f;
                    direction = eyesData[(int)gazeIndex].gaze_direction_normalized;
                    origin.x *= -1;
                    direction.x *= -1;
                }
            }
        }
        ray = new Ray(origin, direction);
        return valid;
    }
}