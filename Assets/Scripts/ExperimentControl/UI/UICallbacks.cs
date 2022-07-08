using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DataHandling;
using Simulation;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using ViveSR.anipal.Eye;

namespace ExperimentControl.UI
{
    public class UICallbacks : MonoBehaviour
    {
        public TMP_Text subjID;
        public TMP_Dropdown GlassesDropdown;

        private static T GetNullSafe<T>()
        {
            var o = SenorSummarySingletons.GetInstance<T>();
            if (o == null) throw new NullReferenceException("Failed to retrieve instance from Singleton collection");
            return o;
        }

        private void Start()
        {
            SenorSummarySingletons.RegisterType(this);
        }

        public void BtnCalibrate()
        {
            SRanipal_Eye_API.LaunchEyeCalibration(Marshal.GetFunctionPointerForDelegate(new Action(ReturnFromCalibration)));
        }

        public void BtnPlayground()
        {
            GetNullSafe<InputHandler>().MoveToNewHallway(HallwayCreator.HallwayObjects[HallwayCreator.Hallways.Playground]);
        }

        public void BtnToggleSimulator()
        {
            GetNullSafe<PhospheneSimulator>().TogglePhospheneSim();
        }

        public void BtnCycleGaze()
        {
            GetNullSafe<PhospheneSimulator>().NextEyeTrackingCondition(new InputAction.CallbackContext());
        }

        public  void BtnSetUpExperimentBlocks()
        {
            var data = SQLiteHandler.Instance;
            data.AllowNewRecords();

            EyeParameter param = default;
            SRanipal_Eye_API.GetEyeParameter(ref param);
            data.AddTrialConfig(
                StaticDataReport.subjID, StaticDataReport.blockId, StaticDataReport.trialId,
                EyeTracking.EyeTrackingConditions.GazeAssistedSampling, HallwayCreator.Hallways.Hallway2, Glasses.Glasses,
                param.gaze_ray_parameter.sensitive_factor
            );
            var xrOrigin = GameObject.FindObjectOfType<XROrigin>().transform;
            var xrH = FindObjectOfType<PhospheneSimulator>().transform;
            var lHand = GameObject.Find("LeftHand Controller").transform;
            var rHand = GameObject.Find("RightHand Controller").transform;
            data.AddEngineRecord(
                StaticDataReport.subjID, StaticDataReport.blockId, StaticDataReport.trialId, DateTime.Now.Ticks,
                xrOrigin, false, false, xrH,
                lHand, false, false, 
                rHand, false, false,
                StaticDataReport.CollisionCount, StaticDataReport.InZone, Time.frameCount
            );
        }

        private void TestData()
        {
            var c = SenorSummarySingletons.GetInstance<DataCollector>();
            var now = DateTime.Now.Ticks;
            var xrO = FindObjectOfType<XROrigin>();
            var xrH = FindObjectOfType<PhospheneSimulator>();
            var lHand = GameObject.Find("LeftHand Controller");
            var rHand = GameObject.Find("RightHand Controller");
            IList<DataCollector.DataRecord> records = new List<DataCollector.DataRecord>
            {
                DataCollector.DataRecord.CreateTrialConfigRecord(
                    EyeTracking.EyeTrackingConditions.GazeAssistedSampling,
                    HallwayCreator.Hallways.Hallway2,
                    Glasses.Glasses,
                    0f,
                    "\t"
                ),
                DataCollector.DataRecord.CreateEngineDataRecord(
                    now, xrO.transform.position, xrO.transform.rotation.eulerAngles, false, true,
                    xrH.transform.position, xrH.transform.rotation.eulerAngles,
                    lHand.transform.position, lHand.transform.rotation.eulerAngles, true, false,
                    rHand.transform.position, rHand.transform.rotation.eulerAngles, false, true,
                    StaticDataReport.CollisionCount, StaticDataReport.InZone, Time.frameCount
                ),
                DataCollector.DataRecord.CreateEyeTrackerRecord(
                    now, 23, 0, 12,
                    float.NaN, false
                ),
                DataCollector.DataRecord.CreateSingleEyeRecord(
                    now, 23, GazeIndex.LEFT, 0,
                    .6f, .12f, .5f*Vector2.one,
                    Vector3.back + Vector3.left, Vector3.forward, 
                    .5f, .3f, .7f
                ),
                DataCollector.DataRecord.CreateSingleEyeRecord(
                    now, 23, GazeIndex.RIGHT, 0,
                    .6f, .12f, .5f*Vector2.one,
                    Vector3.back + Vector3.right, Vector3.forward, 
                    .5f, .3f, .7f
                ),
                DataCollector.DataRecord.CreateSingleEyeRecord(
                    now, 23, GazeIndex.COMBINE, 0,
                    .6f, float.NaN, .5f*Vector2.one,
                    Vector3.back, Vector3.forward, 
                    float.NaN, float.NaN, float.NaN
                )
            };
            c.AddRecords(records);
        }

        public void BtnStartExperiment()
        {
            
        }

        public void BtnRunNextBlock()
        {
            
        }

        private void ReturnFromCalibration()
        {
            Debug.Log("The Calibration returned!");
        }
    }
}
