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
using Unity.VisualScripting;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using ViveSR.anipal.Eye;

namespace ExperimentControl.UI
{
    public class UICallbacks : MonoBehaviour
    {
        [Header("Setup")]
        public TMP_InputField subjID;
        public TMP_Dropdown glassesDropdown;
        public Button btnCalibrate;
        public Button btnStartExperiment;

        [Header("Simulation Management")]
        public Button btnDeactivateSimulation;
        public Button btnActivateImageProcessing;
        public Button btnActivateFullSimulation;
        public Button btnNextPracticeEnvironment;
        public Button btnToggleFixationDot;
        public Button btnCycleGaze;

        [Header("Trial Navigation")]
        public Button btnEndCurrentTrial;
        public Button btnBeginCurrentTrial;
        public Button forwardNavBtn;
        public Button backwardNavBtn;

        [Header("User response")] 
        public Button btnReportLiving;
        public Button btnReportBedroom;
        public Button btnReportKitchen;
        public Button btnReportBathroom;
        public Slider sliderSubjRating;
        public TMP_Text sliderValue;
        public Button btnSubmitSubjRating;

        [Header("Hidden buttons")] 
        public Button btnShowHiddenButtons;
        public Button btnCycleAllEnvs;
        public Button btnCycleSurfaceReplacement;
        public Button btnToggleEdgeDetection;
        public Button btnResetAlignment;
        public Button btnCycleTargets;

        [Header("Monitoring")] 
        public TMP_Text eyeTrackingFreqTxt;
        public TMP_Text blockVal;
        public TMP_Text trialVal;
        public TMP_Text conditionVal;
        public TMP_Text taskVal;
        public TMP_Text environmentVal;
        public TMP_Text nextConditionVal;
        public TMP_Text nextTaskVal;
        public TMP_Text nextEnvironmentVal;
        
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
            DeactivateSceneResponseBtns();
            DeactivateSubjectiveRatingSlider();
            DeactivateBeginTrialButton();
            DeactivateHiddenButtons();
            DeactivateSimulationControlBtns();
            var options = Enum.GetNames(typeof(Glasses)).Select(x => new TMP_Dropdown.OptionData(x));
            glassesDropdown.options = new List<TMP_Dropdown.OptionData>(options);
            glassesDropdown.value = 0;
            
            // End trial / Next Trial / navigation buttons
            DeactivateEndTrialButton();
            DeactivateNavigationBtns();
            RunExperiment.Instance.trialInitiated.AddListener(DeactivateBeginTrialButton); 
            RunExperiment.Instance.trialInitiated.AddListener(DeactivateNavigationBtns);
            RunExperiment.Instance.trialInitiated.AddListener(DeactivateHiddenButtons);
            RunExperiment.Instance.resumedRecording.AddListener(ActivateEndTrialButton);
            RunExperiment.Instance.trialCompleted.AddListener(ActivateNavigationBtns);
            RunExperiment.Instance.trialCompleted.AddListener(ActivateBeginTrialButton);
            RunExperiment.Instance.trialCompleted.AddListener(DeactivateEndTrialButton);
            RunExperiment.Instance.trialCompleted.AddListener(DeactivateSimulationControlBtns);
            SenorSummarySingletons.GetInstance<SceneHandler>().environmentChanged.AddListener( environment =>
                environmentVal.text = environment.Name);

            
            
            SenorSummarySingletons.GetInstance<PhospheneSimulator>()
                .onChangeGazeCondition.AddListener(condition => conditionVal.text = condition.ToString());
            RunExperiment.Instance.currentTrialChanged.AddListener(UpdateTrialInfo);

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

        public void UpdateTrialInfo()
        {

            // Current Trial
            var exp = RunExperiment.Instance;
            var (blockIdx, totalBlockCount, trialIdx, totalTrialCount) = exp.GetCurrentIndicesAndTotalTrialCount();
            var currentTrial = exp.CurrentTrial;
            blockVal.text = $"{blockIdx+1:D2} / {totalBlockCount:D2}"; //blockIdx.ToString("D2");
            trialVal.text = $"{trialIdx+1:D2} / {totalTrialCount:D2}"; // trialIdx.ToString("D2");
            conditionVal.text = currentTrial.EyeTrackingCondition.ToString();
            taskVal.text = currentTrial.Task.ToString();
            environmentVal.text = currentTrial.Environment.Name;

            // Next Trial
            var nextIdx = exp.GetNextTrialIndices();
            if (nextIdx == null)
            {
                nextConditionVal.text = "--";
                nextTaskVal.text = "--";
                nextEnvironmentVal.text = "--";
                return;
            }
            var nextTrial = exp.GetNextTrial(false);
            nextConditionVal.text = nextTrial.EyeTrackingCondition.ToString();
            nextTaskVal.text = nextTrial.Task.ToString();
            nextEnvironmentVal.text = nextTrial.Environment.Name.ToString();
        }

        private Coroutine taskChecking;
        public void PostBlock(IEnumerable<Data2File> allHandlers)
        {
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

        public void BtnToggleSimulator()
        {
            // GetNullSafe<PhospheneSimulator>().TogglePhospheneSim();
        }

        public void BtnActivateImageProcessing() =>
            GetNullSafe<PhospheneSimulator>().ActivateImageProcessing();
        
        public void BtnActivateFullSimulation() =>
            GetNullSafe<PhospheneSimulator>().ActivateSimulation();
        
        public void BtnDeactivateSimulation() =>
            GetNullSafe<PhospheneSimulator>().DeactivateSimulation();


        public void BtnToggleEdgeDetection()
        {
            GetNullSafe<PhospheneSimulator>().ToggleEdgeDetection(new InputAction.CallbackContext());
        }
        
        public void BtnNextLocation()
        {
            GetNullSafe<SceneHandler>().NextEnvironment(new InputAction.CallbackContext());
        }
        
        public void BtnNextPracticeLocation()
        {
            GetNullSafe<SceneHandler>().NextPracticeEnvironment(new InputAction.CallbackContext());
        }
        
        public void DeactivateBeginTrialButton() => btnBeginCurrentTrial.interactable = false;
        public void ActivateBeginTrialButton() => btnBeginCurrentTrial.interactable = true;
        public void BtnBeginCurrentTrial()
        {
            RunExperiment.Instance.BeginTrial(new InputAction.CallbackContext());
            DeactivateBeginTrialButton();
        }
        
        // public void BtnNextVSTarget()
        // {
        //     GetNullSafe<SceneHandler>().NextTargetObject(new InputAction.CallbackContext());
        // }
        
        public void BtnCycleGaze()
        {
            GetNullSafe<PhospheneSimulator>().NextEyeTrackingCondition(new InputAction.CallbackContext());
        }

        public void BtnStartExperiment()
        {
            btnStartExperiment.interactable = false;
            RunExperiment.Instance.StartExperiment(subjID.text);
        }

        // public void BtnRunNextBlock()
        // {
        //     SRanipal_Eye_API.LaunchEyeCalibration(Marshal.GetFunctionPointerForDelegate(new Action(ReturnFromCalibration)));
        //     
        //     RunExperiment.Instance.StartNewBlock();
        //     btnNextBlockObj.interactable = false;
        //     
        //     if (taskChecking != null) StopCoroutine(taskChecking);
        //     postExperimentMonitoring.SetActive(false);
        // }

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

        public int SubjectiveRatingResponse {get; set;}
        public void PromptSubjectiveRating(string message)
        {
            SubjectiveRatingResponse = -1;
            SenorSummarySingletons.GetInstance<SceneHandler>().SetWaitScreenMessage(message);
            ActivateSubjectiveRatingSlider();
        }

        public void ActivateSubjectiveRatingSlider() => sliderSubjRating.interactable = true;
        public void DeactivateSubjectiveRatingSlider()
        {
            sliderSubjRating.interactable = false;
            btnSubmitSubjRating.interactable = false;
            sliderValue.text = "-";
        }

        public void OnSliderRatingChanged()
        {
            sliderValue.text = sliderSubjRating.value.ToString();
            btnSubmitSubjRating.interactable = true;
        }
        public void OnSubjectiveRatingSubmission()
        {
            if (sliderValue.text == "-") return;
            SubjectiveRatingResponse = int.Parse(sliderValue.text);
            DeactivateSubjectiveRatingSlider();
        }
        
        public Environment.RoomCategory SceneRecognitionResponse {get; set;}
        public void PromptSceneRecognitionResponse(string message)
        {
            SceneRecognitionResponse = Environment.RoomCategory.None;
            ActivateSceneResponseBtns();
            SenorSummarySingletons.GetInstance<SceneHandler>().SetWaitScreenMessage(message);
        }
        public void ActivateSceneResponseBtns()
        {
            btnReportLiving.interactable = true;
            btnReportBedroom.interactable = true;
            btnReportKitchen.interactable = true;
            btnReportBathroom.interactable = true;
        }

        public void DeactivateSceneResponseBtns()
        {
            btnReportLiving.interactable = false;
            btnReportBedroom.interactable = false;
            btnReportKitchen.interactable = false;
            btnReportBathroom.interactable = false;
        }
        public void BtnReportKitchen()
        {
            SceneRecognitionResponse = Environment.RoomCategory.Kitchen;
            DeactivateSceneResponseBtns();
        }
        
        public void BtnReportLiving()
        {
            SceneRecognitionResponse = Environment.RoomCategory.Living;
            DeactivateSceneResponseBtns();
        }
        public void BtnReportBathroom()
        {
            SceneRecognitionResponse = Environment.RoomCategory.Bathroom;
            DeactivateSceneResponseBtns();
        }
        
        public void BtnReportBedroom()
        {
            SceneRecognitionResponse = Environment.RoomCategory.Bedroom;
            DeactivateSceneResponseBtns();
        }

        public void BtnEndCurrentTrial()
        {
            RunExperiment.Instance.ManuallyEndTrial();
        }

        public void DeactivateEndTrialButton() => btnEndCurrentTrial.interactable = false;
        public void ActivateEndTrialButton() => btnEndCurrentTrial.interactable = true;

        public void ReturnFromCalibration()
        {
            Debug.Log("The Calibration returned!");
            RunExperiment.Instance.RunCalibrationTest();
        }
        
        public void ActivateNavigationBtns()
        {
            forwardNavBtn.interactable = true;
            backwardNavBtn.interactable = true;
        }

        public void DeactivateNavigationBtns()
        {
            forwardNavBtn.interactable = false;
            backwardNavBtn.interactable = false;
        }

        public void DeactivateSimulationControlBtns()
        {
            btnDeactivateSimulation.interactable = false;
            btnActivateImageProcessing.interactable = false;
            btnActivateFullSimulation.interactable = false;
            btnNextPracticeEnvironment.interactable = false;
            btnToggleFixationDot.interactable = false;
            btnCycleGaze.interactable = false;
        }

        public void ActivateSimulationControlBtns()
        {
            btnDeactivateSimulation.interactable = true;
            btnActivateImageProcessing.interactable = true;
            btnActivateFullSimulation.interactable = true;
            btnNextPracticeEnvironment.interactable = true;
            btnToggleFixationDot.interactable = true;
            btnCycleGaze.interactable = true;
        }

        public void NavigateNextTrial() => RunExperiment.Instance.GetNextTrial(true);
        public void NavigatePreviousTrial() => RunExperiment.Instance.GetPreviousTrial(true);

        public void ActivateHiddenButtons()
        {
            btnCycleAllEnvs.interactable = true;
            btnCycleSurfaceReplacement.interactable = true;
            btnToggleEdgeDetection.interactable = true;
            btnResetAlignment.interactable = true;
            btnCycleTargets.interactable = true;
            ActivateSimulationControlBtns();
        }
        
        public void DeactivateHiddenButtons()
        {
            btnCycleAllEnvs.interactable = false;
            btnCycleSurfaceReplacement.interactable = false;
            btnToggleEdgeDetection.interactable = false;
            btnResetAlignment.interactable = false;
            btnCycleTargets.interactable = false;
        }

    }
}
