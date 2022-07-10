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

        public void BtnSetUpExperimentBlocks()
        {
            RunExperiment.Instance.StartExperiment(subjID.text);
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
