﻿using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xarphos.DataCollection;
using Xarphos.Simulation.Utility;
using Unity.VisualScripting;
using Unity.XR.CoreUtils;
using UnityEngine;
using ViveSR;
using ViveSR.anipal;
using ViveSR.anipal.Eye;
using EyeFramework = ViveSR.anipal.Eye.SRanipal_Eye_Framework;


namespace Xarphos.Simulation
{
    /// <summary>
    /// This class is responsible for getting the eye tracking data and smoothing it, etc.
    /// It uses the SRanipal Framework to get the eye tracking data.
    /// </summary>
    [RequireComponent(typeof(PhospheneSimulator))]
    public class EyeTracking : MonoBehaviour
    {
        [SerializeField] private LayerMask hallwayLayerMask;
        
        [Header("Eye Tracking Parameter")]
        public float sphereCastRadius = 0.01f;
        public float sphereCastDistance = 20f;
        
        private static EyeData_v2 _eyeData;
        private static EyeData_v2 _eyeDataFreeze;
        private static EyeParameter _eyeParameter;
        private static bool _eyeCallbackRegistered;
        private static bool _updatingEye2ScreenPosition;
        
        private PhospheneSimulator sim;

        public double GazeRaySensitivity => _eyeParameter.gaze_ray_parameter.sensitive_factor;
        internal bool EyeTrackingAvailable { get; private set; }
        private bool EyeTrackingErrored = false;

        // For custom adjustment of the fixation point simulation (e.g. during replay from collected data)
        private Vector3 _customFixationPoint;
        private int _customTimestamp;
        private bool _useCustomFixationPoint;

        
        public void SetCustomFixationPoint(Vector3 fixationPoint, int timestamp)
        {
            _customTimestamp = timestamp;
            _customFixationPoint = fixationPoint;
            _useCustomFixationPoint = true;
        }

        public enum EyeTrackingConditions
        {
            GazeAssistedSampling = 0, SimulationFixedToGaze = 1, GazeIgnored = 2
        }
        
        public static string ConditionSymbol(EyeTrackingConditions condition)
        {
            return condition switch
            {
                EyeTracking.EyeTrackingConditions.GazeIgnored => "❌",
                EyeTracking.EyeTrackingConditions.SimulationFixedToGaze => "🔒",
                EyeTracking.EyeTrackingConditions.GazeAssistedSampling => "👁",
                _ => "",
            };
        }
        
#region Unity Event Functions

        private void Awake()
        {
            SingletonRegister.RegisterType(this);
        }

        private void Start()
        {
            // Headset needs a few frames to register and initialise
            // start register and check functions with a delay
            //? Is there a "VR-Ready" Event to subscribe to?
            Invoke(nameof(SystemCheck), .5f);

            // find reference to simulator
            sim = GetComponent<PhospheneSimulator>();

            // setup smoothing variables
            smoothingThresholdSq = smoothingThreshold * smoothingThreshold;
            
            lastMeasuredPositionsL = new FixedSizeList<SmoothData>(smoothingBuffer);
            lastSmoothedPositionsL = new FixedSizeList<SmoothData>(smoothingBuffer);
            lastMeasuredPositionsR = new FixedSizeList<SmoothData>(smoothingBuffer);
            lastSmoothedPositionsR = new FixedSizeList<SmoothData>(smoothingBuffer);
            lastMeasuredPositionsC = new FixedSizeList<SmoothData>(smoothingBuffer);
            lastSmoothedPositionsC = new FixedSizeList<SmoothData>(smoothingBuffer);

        }
        
        private void FixedUpdate()
        {
            if (!EyeTrackingErrored && !CheckFrameworkStatusErrors())
            {
                EyeTrackingErrored = true;
                EyeTrackingAvailable = false;
                Debug.LogWarning("Framework Responded failure to work.");
                // enabled = false; // ToDo: Should we disable the script?
            }
        }

        private void Update()
        {
            Vector2 lViewSpace, rViewSpace, cViewSpace;
            int timestamp;

            if (_useCustomFixationPoint)
            {
                (lViewSpace, rViewSpace, cViewSpace) = sim.EyePosFromFocusPoint(_customFixationPoint);
                timestamp = _customTimestamp;
            }
            
            else if (!EyeTrackingAvailable)
            {
                (lViewSpace, rViewSpace, cViewSpace) = sim.EyePosFromScreenPoint(.5f, .5f);
                timestamp = 0;
            }
            else
            {
                _eyeDataFreeze = _eyeData.Clone(new FieldsCloner(), false);
                
                FocusInfo focusInfo;
                // try to get focus point from combined gaze origin
                if (GetFocusPoint(GazeIndex.COMBINE, out focusInfo)) {}
                // if that fails, try to get focus point using left eye
                else if (GetFocusPoint(GazeIndex.LEFT, out focusInfo)) {}
                // if left also fails try right eye
                else if (GetFocusPoint(GazeIndex.RIGHT, out focusInfo)) {} 
                // if all 3 have failed, don't update eye position
                else return;

                timestamp = _eyeDataFreeze.timestamp;

                // use focus point to update eye position on screen
                (lViewSpace, rViewSpace, cViewSpace) = sim.EyePosFromFocusPoint(focusInfo.point);
            }
            
            UpdateEyePosition(lViewSpace, rViewSpace, cViewSpace, timestamp);
        }

        /// <summary>
        /// Updates the eye position for the simulation parameters and fixation dot
        /// </summary>
        /// <param name="lViewSpace">Fixation point for left eye</param>
        /// <param name="rViewSpace">Fixation point for right eye</param>
        /// <param name="cViewSpace">Combined fixation point</param>
        /// <param name="timestamp">timestamp of recording for smoothing</param>
        private void UpdateEyePosition(Vector2 lViewSpace, Vector2 rViewSpace, Vector2 cViewSpace, int timestamp)
        {
            if (timestamp != 0)
            {
                sim.SetEyePosition( SmoothedPosition(GazeIndex.LEFT, lViewSpace, timestamp), 
                                    SmoothedPosition(GazeIndex.RIGHT, rViewSpace, timestamp),
                                    SmoothedPosition(GazeIndex.COMBINE, cViewSpace, timestamp));
            } else
            {
                sim.SetEyePosition(lViewSpace, rViewSpace, cViewSpace);
            }
        }
        
        /// <summary>
        /// Adapted from SRanipal implementation. Rewritten to be more specific and thus more efficient.
        /// Casts a ray against all colliders when enable eye callback function.
        /// </summary>
        /// <param name="index">A source of eye gaze data.</param>
        /// <param name="focusInfo">Information about where the ray focused on.</param>
        /// <returns>Indicates whether the ray hits a collider.</returns>
        public bool GetFocusPoint(GazeIndex index, out FocusInfo focusInfo)
        {
            SingleEyeData eyeData = index switch
            {
                GazeIndex.LEFT => _eyeDataFreeze.verbose_data.left,
                GazeIndex.RIGHT => _eyeDataFreeze.verbose_data.right,
                GazeIndex.COMBINE => _eyeDataFreeze.verbose_data.combined.eye_data,
                _ => throw new ArgumentOutOfRangeException(nameof(index), index, null)
            };
            bool valid = eyeData.GetValidity(SingleEyeDataValidity.SINGLE_EYE_DATA_GAZE_DIRECTION_VALIDITY);

            if (!valid)
            {
                focusInfo = new FocusInfo();
            }
            else
            {
                Vector3 direction = eyeData.gaze_direction_normalized;
                direction.x *= -1;
                valid = GetFocusInfoFromRayCast(direction, out focusInfo);
            }

            return valid;
        }

        public bool GetFocusInfoFromRayCast(Vector3 direction, out FocusInfo focusInfo) =>
            GetFocusInfoFromRayCast(direction, out focusInfo, parentTransform: sim.targetCamera.transform);
        public bool GetFocusInfoFromRayCast(Vector3 direction,  out FocusInfo focusInfo, Transform parentTransform)
        {
            Ray rayGlobal = new Ray(parentTransform.position, parentTransform.TransformDirection(direction));
            RaycastHit hit;
            var valid = sphereCastRadius == 0 ? 
                Physics.Raycast(rayGlobal, out hit, sphereCastDistance, hallwayLayerMask) :
                Physics.SphereCast(rayGlobal, sphereCastRadius, out hit, sphereCastDistance, hallwayLayerMask);
            focusInfo = new FocusInfo
            {
                point = hit.point,
                normal = hit.normal,
                distance = hit.distance,
                collider = hit.collider,
                rigidbody = hit.rigidbody,
                transform = hit.transform
            };
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
                EyeTrackingAvailable = false;
                Debug.LogWarning("Eye Tracking Not Supported");
                return;
            }
            if (EyeFramework.Status == EyeFramework.FrameworkStatus.STOP || EyeFramework.Status == EyeFramework.FrameworkStatus.ERROR)
            {
                EyeTrackingAvailable = false;
                Debug.LogWarning("Eye Tracking Stopped or Errored");
                return;
            }

            // unfiltered data please
            var param = new EyeParameter();
            param.gaze_ray_parameter.sensitive_factor = 1.0;
            var setEyeParamResult = SRanipal_Eye_API.SetEyeParameter(param);

            var eyeDataResult = SRanipal_Eye_API.GetEyeData_v2(ref _eyeData);
            var eyeParamResult = SRanipal_Eye_API.GetEyeParameter(ref _eyeParameter);
            var resultEyeInit = SRanipal_API.Initial(SRanipal_Eye_v2.ANIPAL_TYPE_EYE_V2, IntPtr.Zero);

            if (eyeDataResult != Error.WORK ||
                eyeParamResult != Error.WORK ||
                resultEyeInit != Error.WORK ||
                setEyeParamResult != Error.WORK)
            {
                Debug.LogError("Initial Check failed.\n" +
                               $"[SRanipal] Eye Data Call v2 : {eyeDataResult}" +
                               $"[SRanipal] Eye Param Call v2: {eyeParamResult}" +
                               $"[SRanipal] Initial Eye v2   : {resultEyeInit}" +
                               $"[SRanipal] Set Eye v2   : {resultEyeInit}"
                );
                EyeTrackingAvailable = false;
                return;
            }

            if (Math.Abs(_eyeParameter.gaze_ray_parameter.sensitive_factor -
                         param.gaze_ray_parameter.sensitive_factor) > 1e-5)
            {
                Debug.LogError($"Retrieved Parameter ({_eyeParameter.gaze_ray_parameter.sensitive_factor}) different to set parameter for gaze sensitivity ({param.gaze_ray_parameter.sensitive_factor}).");
            }

            EyeTrackingAvailable = true;
            RegisterCallback();
        }

        private void RegisterCallback()
        {
            if (SRanipal_Eye_Framework.Instance.EnableEyeDataCallback && !_eyeCallbackRegistered)
            {
                SRanipal_Eye_v2.WrapperRegisterEyeDataCallback(
                    Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)EyeCallback)
                );
                _eyeCallbackRegistered = true;
            }

            else if (!SRanipal_Eye_Framework.Instance.EnableEyeDataCallback && _eyeCallbackRegistered)
            {
                SRanipal_Eye_v2.WrapperUnRegisterEyeDataCallback(
                    Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)EyeCallback)
                );
                _eyeCallbackRegistered = false;
            }
        }
#endregion

#region EyeTracking Data Collection

        internal static readonly float[] Timings = Enumerable.Repeat(float.MinValue, 500).ToArray();
        internal static int TimingIdx;
        private static int errorInUpdate = 0;

        [MonoPInvokeCallback]
        private static void EyeCallback(ref EyeData_v2 eyeDataRef)
        {
            //! needs to be implemented if wanted:
            // if (recordingPaused) return;

            TimingIdx = (TimingIdx + 1) % Timings.Length;
            var now = DateTime.Now;
            var tick = now.Hour * 3600f + now.Minute * 60f + now.Second + now.Millisecond / 1000f;
            Timings[TimingIdx] = tick;
            
            var fetchResult = SRanipal_Eye_API.GetEyeData_v2(ref eyeDataRef);
            if (fetchResult != Error.WORK)
            {
                errorInUpdate += 1;
                return;
            }
            
            // update class variable
            _eyeData = eyeDataRef;
            /************************************
             * Record Data
             ************************************/
            var timestamp = DateTime.Now.Ticks;
            var dataClone = eyeDataRef.CloneViaSerialization();
            new Task(() =>
                {
                    RecordTrackerData(dataClone, errorInUpdate, timestamp);
                }).Start();
            
            errorInUpdate = 0;
        }
        
        private static void RecordTrackerData(EyeData_v2 eyeData, int errorsSince, long timestamp)
        {
            var collector = DataCollector.Instance;
            // record tracker data
            collector.RecordDataEntry(new EyeTrackerDataRecord
            {
                TimeStamp = timestamp,
                TrackerTimeStamp = eyeData.timestamp,
                TrackerTrackingImprovementCount = eyeData.verbose_data.tracking_improvements,
                TrackerFrameCount = eyeData.frame_sequence,
                ConvergenceDistance = eyeData.verbose_data.combined.convergence_distance_mm,
                ConvergenceDistanceValidity = eyeData.verbose_data.combined.convergence_distance_validity,
                ErrorsSinceLastUpdate = errorsSince
            });
            // record left eye
            collector.RecordDataEntry(new SingleEyeDataRecord
            {
                TimeStamp = timestamp,
                TrackerTimeStamp = eyeData.timestamp,
                EyeIndex = GazeIndex.LEFT,
                Validity = eyeData.verbose_data.left.eye_data_validata_bit_mask,
                Openness = eyeData.verbose_data.left.eye_openness,
                PupilDiameter = eyeData.verbose_data.left.pupil_diameter_mm,
                PosInSensor = eyeData.verbose_data.left.pupil_position_in_sensor_area,
                GazeOriginInEye = eyeData.verbose_data.left.gaze_origin_mm,
                GazeDirectionNormInEye = eyeData.verbose_data.left.gaze_direction_normalized
            });
            // record right eye
            collector.RecordDataEntry(new SingleEyeDataRecord
            {
                TimeStamp = timestamp,
                TrackerTimeStamp = eyeData.timestamp,
                EyeIndex = GazeIndex.RIGHT,
                Validity = eyeData.verbose_data.right.eye_data_validata_bit_mask,
                Openness = eyeData.verbose_data.right.eye_openness,
                PupilDiameter = eyeData.verbose_data.right.pupil_diameter_mm,
                PosInSensor = eyeData.verbose_data.right.pupil_position_in_sensor_area,
                GazeOriginInEye = eyeData.verbose_data.right.gaze_origin_mm,
                GazeDirectionNormInEye = eyeData.verbose_data.right.gaze_direction_normalized
            });
            // record combined eye
            collector.RecordDataEntry(new SingleEyeDataRecord
            {
                TimeStamp = timestamp,
                TrackerTimeStamp = eyeData.timestamp,
                EyeIndex = GazeIndex.COMBINE,
                Validity = eyeData.verbose_data.combined.eye_data.eye_data_validata_bit_mask,
                Openness = eyeData.verbose_data.combined.eye_data.eye_openness,
                PupilDiameter = eyeData.verbose_data.combined.eye_data.pupil_diameter_mm,
                PosInSensor = eyeData.verbose_data.combined.eye_data.pupil_position_in_sensor_area,
                GazeOriginInEye = eyeData.verbose_data.combined.eye_data.gaze_origin_mm,
                GazeDirectionNormInEye = eyeData.verbose_data.combined.eye_data.gaze_direction_normalized
            });
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
            if (_eyeCallbackRegistered)
            {
                SRanipal_Eye_v2.WrapperUnRegisterEyeDataCallback(
                    Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)EyeCallback)
                );
                _eyeCallbackRegistered = false;
            }
        }
        
        /// <summary>
        /// Required class for IL2CPP scripting backend support
        /// </summary>
        private class MonoPInvokeCallbackAttribute : Attribute { }
#endregion

#region Smoothing
        // Following Olsson, Pontus. "Real-time and offline filters for eye tracking." (2007).
        // on mouse pointer filtered position with fast attenuation on saccades

        // ToDo: Smooth eyePos in sensor or smooth screen pos from worldspace raycast
        [Header("Smoothing Parameters")] 
        [SerializeField] private float smoothingT = 1.5f;
        [SerializeField] private float smoothingTFast = 0.05f;
        [SerializeField] private float smoothingThreshold = 0.05f; // default value ~5 degree of visual field
        private readonly int smoothingBuffer = 8;
        private float smoothingThresholdSq;
        private float smoothingTReturn;
        private float smoothingTReturnSpeed = float.MinValue;
        private FixedSizeList<SmoothData> lastMeasuredPositionsL;
        private FixedSizeList<SmoothData> lastSmoothedPositionsL;
        private FixedSizeList<SmoothData> lastMeasuredPositionsR;
        private FixedSizeList<SmoothData> lastSmoothedPositionsR;
        private FixedSizeList<SmoothData> lastMeasuredPositionsC;
        private FixedSizeList<SmoothData> lastSmoothedPositionsC;

        private readonly struct SmoothData
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

        private Vector2 SmoothedPosition(GazeIndex which, Vector2 measuredPos, int timestamp)
        {
            var lastMeasuredPositions = which switch
            {
                GazeIndex.LEFT => lastMeasuredPositionsL,
                GazeIndex.RIGHT => lastMeasuredPositionsR,
                GazeIndex.COMBINE => lastMeasuredPositionsC,
                _ => throw new ArgumentOutOfRangeException(nameof(which), which, null)
            };

            var lastSmoothedPositions = which switch
            {
                GazeIndex.LEFT => lastSmoothedPositionsL,
                GazeIndex.RIGHT => lastSmoothedPositionsR,
                GazeIndex.COMBINE => lastSmoothedPositionsC,
                _ => throw new ArgumentOutOfRangeException(nameof(which), which, null)
            };

            // retrieve last measurement
            if (!lastMeasuredPositions.GetLast(out var prevMeasure))
            {
                prevMeasure = new SmoothData(measuredPos, timestamp);
            }
            // add new measure to buffer
            var newMeasure = new SmoothData(measuredPos, timestamp);
            lastMeasuredPositions.Add(newMeasure);
            
            // calculate sampling interval
            var samplingTime = Mathf.Max(newMeasure.Timestamp - prevMeasure.Timestamp, .02f); // .02 is 50Hz, fallback if timestamp is 0
            
            // if we are still attenuating after saccade calculate acceleration and update T
            if (smoothingTReturnSpeed > 0)
            {
                smoothingTReturn += smoothingTReturnSpeed;
                smoothingTReturnSpeed += 1f / (10f * samplingTime); // accelerate T with (1/10th of sampling time) over (sampling time squared)
                if (smoothingTReturn >= smoothingT)
                {
                    smoothingTReturn = float.MinValue;
                    smoothingTReturnSpeed = float.MinValue;
                }
            }
            else if(lastMeasuredPositions.Count > 3)// check for new jump
            {
                var recentHalf = lastMeasuredPositions.GetRecentHalf();
                var recentMean = recentHalf.Aggregate(Vector2.zero, (acc, d) => acc + d.Position) / recentHalf.Count;
                var olderHalf = lastMeasuredPositions.GetOlderHalf();
                var olderMean = olderHalf.Aggregate(Vector2.zero, (acc, d) => acc + d.Position) / olderHalf.Count;
                var diff = Vector2Extensions.Abs((recentMean - olderMean));
                // check if we moved outside of threshold circle
                if (diff.sqrMagnitude >= smoothingThresholdSq)
                {
                    // reset T and start acceleration
                    smoothingTReturn = smoothingTFast;
                    smoothingTReturnSpeed = 1f / (10f * samplingTime);
                }
            }
            
            // calculate filter coefficient 
            var t = smoothingTReturn > 0 ? smoothingTReturn : smoothingT;
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
#endregion
    }
}
