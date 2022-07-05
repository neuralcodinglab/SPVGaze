using System;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using ViveSR;
using ViveSR.anipal;
using ViveSR.anipal.Eye;
using EyeFramework = ViveSR.anipal.Eye.SRanipal_Eye_Framework;


namespace Simulation
{
    public class EyeTracking : MonoBehaviour
    {
        [SerializeField] private LayerMask hallwayLayerMask;

        [Header("Eye Tracking Parameter")]
        public float sphereCastRadius = 0.01f;
        public float sphereCastDistance = 20f;
        
        private static EyeData_v2 _eyeData;
        private EyeParameter eyeParameter;
        private bool eyeCallbackRegistered;
    #region all eye data attributes
        public GazeRayParameter gaze;
        private static UInt64 eye_valid_L, eye_valid_R;                 // The bits explaining the validity of eye data.
        private static float openness_L, openness_R;                    // The level of eye openness.
        private static float pupil_diameter_L, pupil_diameter_R;        // Diameter of pupil dilation.
        private static Vector2 pos_sensor_L, pos_sensor_R;              // Positions of pupils.
        private static Vector3 gaze_origin_L, gaze_origin_R;            // Position of gaze origin.
        private static Vector3 gaze_direct_L, gaze_direct_R;            // Direction of gaze ray.
        private static float frown_L, frown_R;                          // The level of user's frown.
        private static float squeeze_L, squeeze_R;                      // The level to show how the eye is closed tightly.
        private static float wide_L, wide_R;                            // The level to show how the eye is open widely.
        private static double gaze_sensitive;                           // The sensitive factor of gaze ray.
        private static float distance_C;                                // Distance from the central point of right and left eyes.
        private static bool distance_valid_C;                           // Validity of combined data of right and left eyes.
        
        private static int track_imp_cnt = 0;
        private static TrackingImprovement[] track_imp_item;
        
        private static long MeasureTime, CurrentTime, MeasureEndTime;
        private static float time_stamp;
        private static int frame;
    #endregion

        private PhospheneSimulator sim;
        internal bool eyeTrackingAvailable { get; private set; }

        public enum EyeTrackingConditions { GazeIgnored = 0, SimulationFixedToGaze = 1, GazeAssistedSampling = 2 }
        
#region Unity Event Functions
        private void Start()
        {
            // Headset needs a few frames to register and initialise
            // start register and check functions with a delay
            // ToDo: Is there a "VR-Ready" Event to subscribe to?
            Invoke(nameof(SystemCheck), .5f);

            // find reference to simulator
            sim = GetComponent<PhospheneSimulator>();

            SenorSummarySingletons.RegisterType(this);
        }
        
        private void FixedUpdate()
        {
            if (!CheckFrameworkStatusErrors())
            {
                eyeTrackingAvailable = false;
                Debug.LogWarning("Framework Responded failure to work.");
            }
        }

        private void Update()
        {
            if (!eyeTrackingAvailable)
            {
                SetEyePositionToCenter();
                return;
            }

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

        private void SetEyePositionToCenter()
        {
            // calculate a point in the world backwards from a point centred in the screen
            var P = sim.targetCamera.ViewportToWorldPoint(
                new Vector3(.5f, .5f, 10f), Camera.MonoOrStereoscopicEye.Mono); 
            CalculateScreenEyePosition(P);
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
            var lViewSpace = (new Vector2(lProjection.x, -lProjection.y) / lProjection.w) * .5f + .5f * Vector2.one;
            var rViewSpace = (new Vector2(rProjection.x, -rProjection.y) / rProjection.w) * .5f + .5f * Vector2.one;
            var cViewSpace = (new Vector2(cProjection.x, -cProjection.y) / cProjection.w) * .5f + .5f * Vector2.one;

            // ToDo: Update only when valid; Consider gaze smoothing; Play with SrAnipal GazeParameter
            sim.SetEyePosition(lViewSpace, rViewSpace, cViewSpace);
        }
        
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
                if (sphereCastRadius == 0) valid = Physics.Raycast(rayGlobal, out hit, sphereCastDistance, hallwayLayerMask);
                else valid = Physics.SphereCast(rayGlobal, sphereCastRadius, out hit, sphereCastDistance, hallwayLayerMask);
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
        
        private bool CheckFrameworkStatusErrors()
        {
            return EyeFramework.Status == EyeFramework.FrameworkStatus.WORKING;
        }    
    #endregion

#region EyeData Setup
        private void SystemCheck()
        {
            if (EyeFramework.Status == EyeFramework.FrameworkStatus.NOT_SUPPORT)
            {
                eyeTrackingAvailable = false;
                Debug.LogWarning("Eye Tracking Not Supported");
                return;
            }
            // unfiltered data please
            var param = new EyeParameter();
            param.gaze_ray_parameter.sensitive_factor = 1f;
            SRanipal_Eye_API.SetEyeParameter(param);

            var eyeDataResult = SRanipal_Eye_API.GetEyeData_v2(ref _eyeData);
            var eyeParamResult = SRanipal_Eye_API.GetEyeParameter(ref eyeParameter);
            var resultEyeInit = SRanipal_API.Initial(SRanipal_Eye_v2.ANIPAL_TYPE_EYE_V2, IntPtr.Zero);
            
            if (
                eyeDataResult != Error.WORK ||
                eyeParamResult != Error.WORK ||
                resultEyeInit != Error.WORK
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
            RegisterCallback();
        }

        private void RegisterCallback()
        {
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
#endregion

#region EyeTracking Data Collection

internal static readonly float[] Timings = Enumerable.Repeat(float.MinValue, 500).ToArray();
internal static int TimingIdx = 0;

        [MonoPInvokeCallback]
        private static void EyeCallback(ref EyeData_v2 eyeDataRef)
        {
            TimingIdx = (TimingIdx + 1) % Timings.Length;
            var now = DateTime.Now;
            var tick = now.Hour * 3600f + now.Minute * 60f + now.Second + now.Millisecond / 1000f;
            Timings[TimingIdx] = tick;
            
            var eyeParameter = new EyeParameter();
            var retrievalOutcome = SRanipal_Eye_API.GetEyeParameter(ref eyeParameter);
            var outcome = Enum.GetName(typeof(Error), retrievalOutcome);
            _eyeData = eyeDataRef;
            
            var fetchResult = SRanipal_Eye_API.GetEyeData_v2(ref _eyeData);
            if (fetchResult != ViveSR.Error.WORK) return;
            
            MeasureTime = now.Ticks;
            time_stamp = _eyeData.timestamp;
            frame = _eyeData.frame_sequence;
            eye_valid_L = _eyeData.verbose_data.left.eye_data_validata_bit_mask;
            eye_valid_R = _eyeData.verbose_data.right.eye_data_validata_bit_mask;
            openness_L = _eyeData.verbose_data.left.eye_openness;
            openness_R = _eyeData.verbose_data.right.eye_openness;
            pupil_diameter_L = _eyeData.verbose_data.left.pupil_diameter_mm;
            pupil_diameter_R = _eyeData.verbose_data.right.pupil_diameter_mm;
            pos_sensor_L = _eyeData.verbose_data.left.pupil_position_in_sensor_area;
            pos_sensor_R = _eyeData.verbose_data.right.pupil_position_in_sensor_area;
            gaze_origin_L = _eyeData.verbose_data.left.gaze_origin_mm;
            gaze_origin_R = _eyeData.verbose_data.right.gaze_origin_mm;
            gaze_direct_L = _eyeData.verbose_data.left.gaze_direction_normalized;
            gaze_direct_R = _eyeData.verbose_data.right.gaze_direction_normalized;
            gaze_sensitive = eyeParameter.gaze_ray_parameter.sensitive_factor;
            frown_L = _eyeData.expression_data.left.eye_frown;
            frown_R = _eyeData.expression_data.right.eye_frown;
            squeeze_L = _eyeData.expression_data.left.eye_squeeze;
            squeeze_R = _eyeData.expression_data.right.eye_squeeze;
            wide_L = _eyeData.expression_data.left.eye_wide;
            wide_R = _eyeData.expression_data.right.eye_wide;
            distance_valid_C = _eyeData.verbose_data.combined.convergence_distance_validity;
            distance_C = _eyeData.verbose_data.combined.convergence_distance_mm;
            track_imp_cnt = _eyeData.verbose_data.tracking_improvements.count;
        }
#endregion

#region EyeData Clean Up & Necessities
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
            Debug.Log("Releasing...");
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
    }
}
