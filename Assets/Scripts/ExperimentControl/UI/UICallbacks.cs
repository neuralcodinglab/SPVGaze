using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using DataHandling;
using DataHandling.Separated;
using MathNet.Numerics.Statistics;
using Simulation;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ViveSR.anipal.Eye;

namespace ExperimentControl.UI
{
    public class UICallbacks : MonoBehaviour
    {
        [Header("Management")]
        public TMP_InputField subjID;
        public TMP_Dropdown glassesDropdown;

        public Button btnResetState;
        public Button btnStartExperiment;
        public Button btnNextBlockObj;

        [Header("Monitoring")] 
        public TMP_Text eyeTrackingFreqTxt;
        public TMP_Text blockVal;
        public TMP_Text trialVal;
        public TMP_Text conditionVal;
        public TMP_Text hallwayVal;
        public TMP_Text inZoneTxt;
        public TMP_Text collisionCountTxt;
        
        [Header("Post Experiment")]
        public GameObject postExperimentMonitoring;
        public TMP_Text tasksRemaining;
        public TMP_Text maxRecordsRemaining;
        public TMP_Text openStreamsRemaining;

        private static T GetNullSafe<T>()
        {
            var o = SenorSummarySingletons.GetInstance<T>();
            if (o == null) throw new NullReferenceException("Failed to retrieve instance from Singleton collection");
            return o;
        }

        private void Awake()
        {
            SenorSummarySingletons.RegisterType(this);
        }

        private void Start()
        {
            var options = Enum.GetNames(typeof(Glasses)).Select(x => new TMP_Dropdown.OptionData(x));
            glassesDropdown.options = new List<TMP_Dropdown.OptionData>(options);
            glassesDropdown.value = 0;
            
            RunExperiment.Instance.blockCompleted.AddListener(() =>
            {
                btnNextBlockObj.interactable = true;
            });
            StaticDataReport.OnChangeInZone.AddListener(val => inZoneTxt.text = val.ToString("D3"));
            StaticDataReport.OnChangeCollisionCount.AddListener(val => collisionCountTxt.text = val.ToString("D3"));
            SenorSummarySingletons.GetInstance<PhospheneSimulator>()
                .onChangeGazeCondition.AddListener(condition => conditionVal.text = condition.ToString());
            SenorSummarySingletons.GetInstance<InputHandler>().onChangeHallway
                .AddListener(hallway => hallwayVal.text = hallway.Name);
        }

        private void FixedUpdate()
        {
            UpdateTrackerFrequency();
        }

        private void UpdateTrackerFrequency()
        {
            // 0 is the last field written to,
            // so once that is not the default value anymore we start calculating running average
            if (!(Math.Abs(EyeTracking.Timings[0] - float.MinValue) > float.Epsilon * 3)) return;

            Vector2[] tmp;
            
            float[] copy = new float[EyeTracking.Timings.Length];
            EyeTracking.Timings.CopyTo(copy, 0);
            int idx = EyeTracking.TimingIdx;
            for (int i = 0; i < copy.Length; i += 1)
            {
                var prev = (idx - 1 + copy.Length) % copy.Length;
                if (copy[idx] - copy[prev] < 0)
                    break;
                idx = (idx - 1 + copy.Length) % copy.Length;
            }
            idx = (idx + 1) % copy.Length;
            tmp = new Vector2[EyeTracking.Timings.Length - 1];

            for (int i = 0; i < tmp.Length; i += 1)
            {
                var next = (idx + i) % copy.Length;
                var prev = (next - 1 + copy.Length) % copy.Length;

                var x = copy[next];
                var y = 1f / (x - copy[prev]);
                
                tmp[i] = new Vector2(x, y);
            }

            var meanFreq = tmp.Select(v => v.y).Mean();
            eyeTrackingFreqTxt.text = meanFreq.ToString("F3") + " Hz";
        }

        public void UpdateBlockAndTrial(int block, int trial)
        {
            blockVal.text = block.ToString("D2");
            trialVal.text = trial.ToString("D2");
        }

        private Coroutine taskChecking;
        public void PostBlock(IEnumerable<Data2File> allHandlers)
        {
            btnNextBlockObj.interactable = false;
            postExperimentMonitoring.SetActive(true);
            
            var handlerArr = allHandlers.ToArray();
            taskChecking = StartCoroutine(CheckCompletion(handlerArr));
        }

        private IEnumerator CheckCompletion(Data2File[] allHandlers)
        {
            while (true)
            {
                var totalTasks = 0;
                var upperBoundRecords = 0;
                var openStreams = 0;
                foreach (var h in allHandlers)
                {
                    totalTasks += h.TaskList.Count(t => !t.IsCompleted);
                    upperBoundRecords += h.RemainingItems.Aggregate(0, (agg, q) => agg + q.Count);
                    openStreams += h.OldStreams.Count;
                }

                tasksRemaining.text = totalTasks.ToString("D3");
                maxRecordsRemaining.text = upperBoundRecords.ToString("D5");
                openStreamsRemaining.text = openStreams.ToString("D3");

                if (totalTasks <= 0)
                {
                    break;
                }

                yield return new WaitForSeconds(0.5f);
            }
            yield return new WaitForSeconds(5f);
            postExperimentMonitoring.SetActive(false);
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

        
        public void BtnToggleEdgeDetection()
        {
            GetNullSafe<PhospheneSimulator>().ToggleEdgeDetection(new InputAction.CallbackContext());
        }
        
        public void BtnNextLocation()
        {
            GetNullSafe<SceneHandler>().NextHouseLocation(new InputAction.CallbackContext());
        }
        public void BtnNextRoom()
        {
            GetNullSafe<SceneHandler>().NextMobilityCourse(new InputAction.CallbackContext());
        }
        
        public void BtnNextVSTarget()
        {
            GetNullSafe<SceneHandler>().NextVisualSearchTarget(new InputAction.CallbackContext());
        }
        
        public void BtnCycleGaze()
        {
            GetNullSafe<PhospheneSimulator>().NextEyeTrackingCondition(new InputAction.CallbackContext());
        }

        public void BtnSetUpExperimentBlocks()
        {
            btnStartExperiment.interactable = true;
            btnResetState.interactable = false;
            RunExperiment.Instance.EndTrial();
        }

        public void BtnStartExperiment()
        {
            btnStartExperiment.interactable = false;
            btnResetState.interactable = true;
            RunExperiment.Instance.StartExperiment(subjID.text);
        }

        public void BtnRunNextBlock()
        {
            SRanipal_Eye_API.LaunchEyeCalibration(Marshal.GetFunctionPointerForDelegate(new Action(ReturnFromCalibration)));
            
            RunExperiment.Instance.StartNewBlock();
            btnNextBlockObj.interactable = false;
            
            if (taskChecking != null) StopCoroutine(taskChecking);
            postExperimentMonitoring.SetActive(false);
        }

        public void BtnCycleSurface()
        {
            GetNullSafe<PhospheneSimulator>().NextSurfaceReplacementMode(new InputAction.CallbackContext());
        }

        public void BtnToggleFocusDot()
        {
            GetNullSafe<PhospheneSimulator>().ToggleFocusDot();
        }

        public void BtnResetAlignment()
        {
            SenorSummarySingletons.GetInstance<InputHandler>().ResetCamera2OriginAlignment();
        }
        

        private void ReturnFromCalibration()
        {
            Debug.Log("The Calibration returned!");
        }
    }
}
