using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.XR.CoreUtils;
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

        #region Smoothing
        // Following Olsson, Pontus. "Real-time and offline filters for eye tracking." (2007).
        // on mouse pointer filtered position with fast attenuation on saccades

        // ToDo: Smooth eyePos in sensor or smooth screen pos from worldspace raycast
        [Header("Smooting Parameters")]
        public const float SmoothingT = 1.5f;
        public const float SmoothingThreshold = 0.05f; // default value ~5 degree of visual field
        public const float SmoothingTFast = 0.05f;
        public const float SmoothingThresholdSq = SmoothingThreshold * SmoothingThreshold;
        private float smoothingTReturn;
        private float smoothingTReturnSpeed = float.MinValue;
        private FixedSizeList<SmoothData> lastMeasuredPositions;
        private FixedSizeList<SmoothData> lastSmoothedPositions;
        
        private struct SmoothData
        {
            internal Vector2 Position { get; }
            private readonly int time; // diff of timestamp = h in ms
            public float Timestamp => time / 1000f; // return timestamp in seconds

            public SmoothData(Vector2 measuredPos, int timestamp)
            {
                Position = measuredPos;
                time = timestamp;
            }
        }

        private Vector2 SmoothedPosition(Vector2 measuredPos, int timestamp)
        {
            lastMeasuredPositions ??= new FixedSizeList<SmoothData>(12);
            lastSmoothedPositions ??= new FixedSizeList<SmoothData>(12);

            // retrieve last measurement
            if (!lastMeasuredPositions.GetLast(out var prevMeasure))
            {
                prevMeasure = new SmoothData(measuredPos, timestamp);
            }
            // add new measure to buffer
            var newMeasure = new SmoothData(measuredPos, timestamp);
            lastMeasuredPositions.Add(newMeasure);
            
            // calculate sampling interval
            var samplingTime = newMeasure.Timestamp - prevMeasure.Timestamp;
            
            // if we are still attenuating after saccade calculate acceleration and update T
            if (smoothingTReturnSpeed >= 0)
            {
                smoothingTReturn += smoothingTReturnSpeed;
                smoothingTReturnSpeed += 1f / (10f * samplingTime); // accelerate T with (1/10th of sampling time) over (sampling time squared)
                if (smoothingTReturn >= SmoothingT)
                {
                    smoothingTReturn = float.MinValue;
                    smoothingTReturnSpeed = float.MinValue;
                }
            }
            else // check for new jump
            {
                var recentHalf = lastMeasuredPositions.GetRecentHalf();
                var recentMean = recentHalf.Aggregate(Vector2.zero, (acc, d) => acc + d.Position) / recentHalf.Count;
                var olderHalf = lastMeasuredPositions.GetOlderHalf();
                var olderMean = olderHalf.Aggregate(Vector2.zero, (acc, d) => acc + d.Position) / olderHalf.Count;
                var diff = (recentMean - olderMean).Abs();
                // check if we moved outside of threshold circle
                if (diff.sqrMagnitude >= SmoothingThresholdSq)
                {
                    // reset T and start acceleration
                    smoothingTReturn = SmoothingTFast;
                    smoothingTReturnSpeed = 1f / (10f * samplingTime);
                }
            }
            
            // calculate filter coefficient 
            var t = smoothingTReturn >= 0 ? smoothingTReturn : SmoothingT;
            var alpha = t / samplingTime;
            
            // get last smoothed position for calculation
            if (!lastSmoothedPositions.GetLast(out var prevSmoothed))
            {
                prevSmoothed = new SmoothData(measuredPos, timestamp);
            }
            
            // calculate smoothed position
            var smoothedPos = (measuredPos + alpha * prevSmoothed.Position) / (1 + alpha);
            lastSmoothedPositions.Add(new SmoothData(smoothedPos, timestamp));

            return smoothedPos;
        }

        private class FixedSizeList<T> : List<T>
        {
            public int Size { get; }
            protected new readonly int Capacity;

            public FixedSizeList(int size) : base(size)
            {
                Size = size;
            }

            public bool GetLast(out T obj)
            {
                obj = default;
                if (Count == 0) return false;
                obj = this.ElementAt(Count - 1);
                return true;
            }

            public List<T> GetRecentHalf()
            {
                if (Count <= 1) return null;
                var idx = Mathf.CeilToInt(Count / 2f);
                return GetRange(idx, Count - idx);
            }
            
            public IList<T> GetOlderHalf()
            {
                if (Count == 0) return null;
                var idx = Mathf.CeilToInt(Count / 2f);
                return GetRange(0, idx);
            }

            public bool IsFull()
            {
                return Count == Size;
            }

            public new void Add(T obj)
            {
                if (Count == Capacity)
                {
                    RemoveAt(0);
                    base.Add(obj);
                    if (Count != Capacity || Count != Size)
                    {
                        Debug.LogError("FixedSizeList not so fixed.");
                    }
                }
                else
                    base.Add(obj);
            }
        }


        #endregion
    }
}
