using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DataHandling;
using Xarphos.DataCollection;
using ExperimentControl.UI;
using Xarphos;
using Xarphos.Simulation;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using ViveSR.anipal.Eye;
using Random = UnityEngine.Random;

namespace ExperimentControl
{
    public class RunExperiment : MonoBehaviour
    {
        // public string dataSaveDirectory;
        public GameObject xrOrigin;
        public GameObject xrHead;
        public GameObject handLeft;
        internal ControllerVibrator LeftController;
        public GameObject handRight;
        internal ControllerVibrator RightController;
        public GameObject waitingScreen;
        [SerializeField] private AudioSource eventClip;
        [SerializeField] private AudioSource startEndClip;

        public static RunExperiment Instance { get; private set; }
        
        internal bool recordingPaused = true;
        public UnityEvent resumedRecording;
        public UnityEvent trialInitiated;
        public UnityEvent trialCompleted;
        public UnityEvent blockCompleted;
        public UnityEvent experimentCompleted;

        private void Awake()
        {
            if (Instance != null)
                throw new InvalidOperationException("Can only have 1 'RunExperiment' class active");
            Instance = this;

            // Register the data handlers after a half second delay to ensure that the DataCollector is initialized
            Invoke(nameof(RegisterHandler), 0.5f);

            resumedRecording ??= new UnityEvent();
            trialCompleted ??= new UnityEvent();
            blockCompleted ??= new UnityEvent();
            experimentCompleted ??= new UnityEvent();
        }

        private void RegisterHandler()
        {
            DataCollector.Instance.RegisterNewHandler<TrialConfigRecord>();
        }

        private void Start()
        {
            LeftController = handLeft.GetComponentInChildren<ControllerVibrator>();
            RightController = handRight.GetComponentInChildren<ControllerVibrator>();
        }
        
        /// Data recording
        /// at each fixed update, an entry is recorded for the unity engine data
        /// the other datapoints are recorded upon the start of the trial and start of the experiment. <summary>

        private void FixedUpdate()
        {
            var trgCollider = string.Empty; 
            if (_reportingTarget)
            {
                _pointingTargetEye = GetPointingTarget(xrHead.transform, fromEyeTracker: true);
                _pointingTargetHead = GetPointingTarget(xrHead.transform, fromEyeTracker: false);
                _pointingTargetHand = GetPointingTarget(handRight.transform, fromEyeTracker: false);
                if (_pointingTargetHand.collider is not null) trgCollider = _pointingTargetHand.collider.gameObject.name;
            }

            if (recordingPaused) return;
            var record = new EngineDataRecord
            {
                TimeStamp = DateTime.Now.Ticks,
                XROriginPos = xrOrigin.transform.position,
                XROriginRot = xrOrigin.transform.rotation,
                XRHeadPos = xrHead.transform.position,
                XRHeadRot = xrHead.transform.rotation,
                HandLPos = handLeft.transform.position,
                HandLRot = handLeft.transform.rotation,
                HandRPos = handRight.transform.position,
                HandRRot = handRight.transform.rotation,
                // CollisionCount = StaticDataReport.CollisionCount,
                FrameCount = Time.frameCount,
                // ReportedEventsCount = _reportedEventsCount,
                // ActiveTarget = CurrentTrial.Environment.ActiveTargetName,
                PointLocationEye = _reportingTarget ? _pointingTargetEye.point : Vector3.zero,
                PointLocationHand = _reportingTarget ? _pointingTargetHand.point : Vector3.zero,
                PointLocationHead = _reportingTarget ? _pointingTargetHead.point : Vector3.zero,
                // TargetHit = trgCollider
            };
            DataCollector.Instance.RecordDataEntry(record);
        }
        
        /// The experiment trial configuration
        /// 
        /// Each block is measures 1 eye-tracking condition in several environments (1 environment = 1 trial)
        /// Each trial consists of a scene-recognition section and a visual search section
        /// 
        /// There are 9 blocks in total (3 practice blocks and 6 experiment blocks), grouped into 3 sessions.
        
        // A trial contains of two tasks (sections). 
        public enum Task
        {
            None,
            FreePractice,
            SceneRecognition,
            VisualSearch
        }

        public class Trial
        {
            public Trial(Task task,
                EyeTracking.EyeTrackingConditions eyeTrackingCondition,
                Environment environment,
                int maxDuration)
            {
                Task = task;
                EyeTrackingCondition = eyeTrackingCondition;
                Environment = environment;
                MaxDuration = maxDuration;
            }
            public Task Task {get; set;}
            public EyeTracking.EyeTrackingConditions EyeTrackingCondition {get; set;}
            public Environment Environment {get; set;}
            public int MaxDuration {get; set;}
        }
        
        // private Task _currTask;
        private int _reportedEventsCount;
        private Environment.RoomCategory _reportedRoomCategory = Environment.RoomCategory.None;
        private int _reportedSubjectiveRating;
        private float _trialStartTime;
        private float _trialDuration;
        
        private List<List<Trial>> _blocks;

        private List<List<Trial>> GetExperimentalBlocks()
        {
            // Shorthand for the gaze tracking conditions 
            var (cond1, cond2, cond3) =
                (EyeTracking.EyeTrackingConditions.GazeIgnored, 
                    EyeTracking.EyeTrackingConditions.GazeAssistedSampling, 
                    EyeTracking.EyeTrackingConditions.SimulationFixedToGaze);

            // Get all environments, split in practice and experiment set and randomly shuffle 
            var practiceEnvs = SingletonRegister.GetInstance<SceneHandler>().GetPracticeEnvironments().ToList();
            var experimentEnvs = SingletonRegister.GetInstance<SceneHandler>().GetExperimentEnvironments().ToList();
            Debug.Log($"Practice envs: {practiceEnvs.Count}, Exp. envs: {experimentEnvs.Count}");

            // Practice Session 
            var practiceBlock = new List<Trial>()
            {
                // Free practice (5 minutes)
                new Trial(Task.FreePractice, cond1, practiceEnvs[0], 500),
                
                // Practice the tasks with condition 1 (2.5 minutes)
                new Trial(Task.VisualSearch, cond1, practiceEnvs[0], 90), 
                new Trial(Task.SceneRecognition, cond1, practiceEnvs[1], 60), 
                
                // Practice the tasks with condition 2 (2.5 minutes)
                new Trial(Task.VisualSearch, cond2, practiceEnvs[0], 90),
                new Trial(Task.SceneRecognition, cond2, practiceEnvs[2], 60),
                
                // Practice the tasks with condition 3 (2.5 minutes)
                new Trial(Task.VisualSearch, cond3, practiceEnvs[0], 90),
                new Trial(Task.SceneRecognition, cond3, practiceEnvs[3], 60),
                
                // Final free practice round (2,5 minutes)
                new Trial(Task.FreePractice, cond1, practiceEnvs[0], 300),
            };

            // Scene recognition Sessions 
            List<List<Trial>> GetSceneRecognitionSession(int trialsPerBlock)
            {
                var shuffledEnvs = experimentEnvs.OrderBy(_ => Random.value).ToList();
                
                // Scene Recognition blocks (n scene recognition trials, preceded with 1 visual search trial)
                var srBlockCond1 = new List<Trial>() ;
                srBlockCond1.Add(new Trial(Task.VisualSearch, cond1, practiceEnvs[0], 90));
                foreach (var env in shuffledEnvs.GetRange(0, trialsPerBlock))
                    srBlockCond1.Add(new Trial(Task.SceneRecognition, cond1, env, 90));
        
                var srBlockCond2 = new List<Trial>();
                srBlockCond2.Add(new Trial(Task.VisualSearch, cond2, practiceEnvs[0], 90));
                foreach (var env in shuffledEnvs.GetRange(trialsPerBlock, trialsPerBlock))
                    srBlockCond2.Add(new Trial(Task.SceneRecognition, cond2, env, 90));
        
                var srBlockCond3 = new List<Trial>();
                srBlockCond3.Add(new Trial(Task.VisualSearch, cond3, practiceEnvs[0], 90));
                foreach (var env in shuffledEnvs.GetRange(2*trialsPerBlock, trialsPerBlock))
                    srBlockCond3.Add(new Trial(Task.SceneRecognition, cond3, env, 90));

                var session = new List<List<Trial>>() {srBlockCond1, srBlockCond2, srBlockCond3};
                return session.OrderBy(_ => Random.value).ToList();
            }
            
            
            // Visual search Sessions
            List<List<Trial>> GetVisualSearchSession(int nRepeats)
            {
                var vsBlock = new List<Trial>()
                {
                    new Trial(Task.VisualSearch, cond1, practiceEnvs[0], 120),
                    new Trial(Task.VisualSearch, cond2, practiceEnvs[0], 120),
                    new Trial(Task.VisualSearch, cond3, practiceEnvs[0], 120)
                };

                var vsSession = new List<List<Trial>>();
                for (int i = 0; i < nRepeats; i++) 
                    vsSession.Add(vsBlock.OrderBy(_ => Random.value).ToList());
                return vsSession;
            }
           

            
            // Blocks (each block is a List of trials) are grouped into Sessions (each session is a list of blocks)


            var practiceSession = new List<List<Trial>>() {practiceBlock};
            var sceneRecSession1 = GetSceneRecognitionSession(3);
            var sceneRecSession2 = GetSceneRecognitionSession(3);
            var visSearchSession = GetVisualSearchSession(3);


            return practiceSession
                .Concat(sceneRecSession1).ToList()
                .Concat(sceneRecSession2).ToList()
                .Concat(visSearchSession).ToList();
        }
        

        public void StartExperiment(string subjId)
        {
            // Load the experimental blocks
            _blocks = GetExperimentalBlocks();
            Debug.Log($"loaded {_blocks.Count} blocks:");

            // create folders and files
            var subjectDir = Path.Join(Application.persistentDataPath, subjId);

            // If already exists, don't overwrite or merge, but create new folder (trailing _ appended)
            while (Directory.Exists(subjectDir))
            {
                Debug.Log($"Already exists: {subjectDir}");
                subjectDir = $"{subjectDir}_";
                subjId = $"{subjId}_";
                SingletonRegister.GetInstance<UICallbacks>().subjID.text = subjId;
            }
            
            // if (Directory.Exists(subjectDir))
            // {
            //     var tmp = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            //     Debug.LogWarning($"Subject Directory for {subjId} exists. Replacing with {tmp}");
            //     subjId = tmp;
            //     SenorSummarySingletons.GetInstance<UICallbacks>().subjID.text = subjId;
            // }
            
            DataCollector.Instance.StartRecordingNewSubject(subjId);

            // Register events
            resumedRecording.AddListener(OnTrialStart);
            
            // Load first Trial
            _currTrialIdx = 0;
            _currBlockIdx = 0;
            CurrentTrial = _blocks[_currTrialIdx][_currBlockIdx];
            
            // Jump to the waiting screen
            SingletonRegister.GetInstance<UICallbacks>().DeactivateSimulationControlBtns();
            SingletonRegister.GetInstance<UICallbacks>().DeactivateHiddenButtons();
            SingletonRegister.GetInstance<UICallbacks>().ActivateBeginTrialButton();
            SingletonRegister.GetInstance<UICallbacks>().ActivateNavigationBtns();
            SingletonRegister.GetInstance<SceneHandler>().DeactivateAll();
            PauseRecording();
        }
        
        // Keep track of current Trial
        private int _currBlockIdx = -1;
        private int _currTrialIdx = -1;
        public UnityEvent currentTrialChanged;
        private Trial _currTrial;
        public Trial CurrentTrial
        {
            get { return _currTrial;}
            set
            {
                if (_currTrial == value)
                    return;
                
                // if CurrentTrial changed value, invoke event
                _currTrial = value;
                currentTrialChanged.Invoke();
            }
        }
        
        public Tuple<int, int, int, int> GetCurrentIndicesAndTotalTrialCount() =>
            new Tuple<int, int, int, int>(_currBlockIdx, _blocks.Count, _currTrialIdx, _blocks[_currBlockIdx].Count);
        public Tuple<int, int> GetPreviousTrialIndices()
        {
            int prevBlockIdx = _currBlockIdx;
            int prevTrialIdx = _currTrialIdx - 1;
            
            if (prevTrialIdx < 0)
            {
                if (prevBlockIdx <= 0)
                    return null;
                
                prevBlockIdx -= 1;
                prevTrialIdx = _blocks[prevBlockIdx].Count - 1;
            }
            return new Tuple<int, int>(prevBlockIdx, prevTrialIdx);
        }
        public Tuple<int, int> GetNextTrialIndices()
        {
            int nextBlockIdx = _currBlockIdx;
            int nextTrialIdx = _currTrialIdx + 1;
            
            if (nextTrialIdx > _blocks[nextBlockIdx].Count - 1)
            {
                if (nextBlockIdx >= _blocks.Count() -1 )
                    return null;
                
                nextBlockIdx += 1;
                nextTrialIdx = 0;
            }
            return new Tuple<int, int>(nextBlockIdx, nextTrialIdx);
        }

        public Trial GetNextTrial(bool setAsCurrent)
        {
            // Get trial indices 
            var indices = GetNextTrialIndices();
            if (indices == null)
                return CurrentTrial;
            
            // Retrieve the trial from the list of blocks
            var (blockIdx, trialIdx) = indices;
            var trial = _blocks[blockIdx][trialIdx];
            
            // Return the trial
            if (!setAsCurrent)
                return trial;
            
            // Or set as the current trial and return
            _currBlockIdx = blockIdx;
            _currTrialIdx = trialIdx;
            CurrentTrial = trial;
            return trial;
        }
        public Trial GetPreviousTrial(bool setAsCurrent)
        {
            // Get trial indices 
            var indices = GetPreviousTrialIndices();
            if (indices == null)
                return CurrentTrial;
            
            // Retrieve the trial from the list of blocks
            var (blockIdx, trialIdx) = indices;
            var trial = _blocks[blockIdx][trialIdx];
            
            // Return the trial
            if (!setAsCurrent)
                return trial;
            
            // Or set as the current trial and return
            _currBlockIdx = blockIdx;
            _currTrialIdx = trialIdx;
            CurrentTrial = trial;
            return trial;
        }

        public void BeginTrial(InputAction.CallbackContext ctx) => BeginTrial();
        private void BeginTrial()
        {
            Debug.Log($"Starting new Trial (condition: {CurrentTrial.EyeTrackingCondition}, environment {CurrentTrial.Environment.Name}");
            trialInitiated.Invoke();
            
            // Set responses to none
            _reportedRoomCategory = Environment.RoomCategory.None;
            _reportedSubjectiveRating = -1;
            _reportedEventsCount = 0;
            
            // Start the recording 
            DataCollector.Instance.NewTrial(_currBlockIdx, _currTrialIdx);
            StartCoroutine(ResumeRecording(5));
        }
        
        private void OnTrialStart()
        {
            startEndClip.Play();
            _endTrialAfterTimeLimit = StartCoroutine(EndTrialAfterTimeLimit());
            switch (CurrentTrial.Task)
            {
                case Task.FreePractice:
                    SingletonRegister.GetInstance<SceneHandler>().ActivateAllDefaultTargets();
                    SingletonRegister.GetInstance<UICallbacks>().ActivateSimulationControlBtns();
                    SingletonRegister.GetInstance<PhospheneSimulator>().DeactivateSimulation();
                    break;
                case Task.VisualSearch:
                    SingletonRegister.GetInstance<SceneHandler>().NextTargetObject(usePersistentIdx:true);
                    // SenorSummarySingletons.GetInstance<SceneHandler>().RandomTargetObject();
                    break;
            }
            _trialDuration = -1f;
            _trialStartTime = Time.time;
            _participantTriggerEnabled = true;
        }
        private Coroutine _endTrialAfterTimeLimit; 
        private IEnumerator EndTrialAfterTimeLimit()
        {
            var trialStarted = CurrentTrial;
            yield return  new WaitForSeconds(CurrentTrial.MaxDuration);
            if ((trialStarted == CurrentTrial) & (!lastSecondRecording) & (!recordingPaused))
                GetUserResponseAndEndTrial();
        }
        
        /// Pausing & Unpausing the experiment: 
        /// The script will continue running, but variable recordingPaused is temporarily set to true, which pauses
        /// the data recording and disables some input-actions.
        /// 
        /// Upon pausing the experiment (e.g. between different trials), display a waiting screen.
        /// Upon resuming the experiment, the participant gets a "Get Ready message" for a specified countdown time.
        private void PauseRecording()
        {
            recordingPaused = true;
            SingletonRegister.GetInstance<SceneHandler>().JumpToWaitingScreen();
            SingletonRegister.GetInstance<PhospheneSimulator>().DeactivateSimulation();
        }
        
        
        private IEnumerator ResumeRecording(int waitSeconds)
        {
            // Get the icon that indicates the eye tracking condition (or all three if free practice)
            var conditionIcon = (CurrentTrial.Task == Task.FreePractice)?  
                "❌ 🔒 👁"  : EyeTracking.ConditionSymbol(CurrentTrial.EyeTrackingCondition);   
            
            // Display the task and condition in the waiting screen
            SingletonRegister.GetInstance<SceneHandler>()
                .SetWaitScreenMessage($"{CurrentTrial.Task.HumanName()} \n{conditionIcon}");
            
            // Wait for some seconds before starting the trial
            yield return new WaitForSeconds(waitSeconds);
            
            // And go.. 
            SingletonRegister.GetInstance<SceneHandler>().JumpToEnvironment(CurrentTrial.Environment);
            SingletonRegister.GetInstance<PhospheneSimulator>().ActivateSimulation(CurrentTrial.EyeTrackingCondition);
            resumedRecording?.Invoke();
            recordingPaused = false;
        }
        
        /// Participant response 
        /// 
        /// In the scene recognition task, the participant hits a trigger to indicate that they recognize the scene 
        /// In the visual search task, the participant points and triggers whenever they found a target
        private bool _participantTriggerEnabled;
        public void OnParticipantTrigger1(InputAction.CallbackContext ctx) => OnParticipantTrigger1();
        private void OnParticipantTrigger1()
        {
            // Ignore if not currently recording
            Debug.Log($"Participant pressed trigger button! (Experiment is paused: {recordingPaused})");
            if (recordingPaused || !_participantTriggerEnabled) return;
            
            _reportedEventsCount += 1;
            switch (CurrentTrial.Task)
            {
                case Task.FreePractice:
                    eventClip.Play();
                    SingletonRegister.GetInstance<PhospheneSimulator>()
                        .ToggleSimulationActive();
                    break;
                case Task.SceneRecognition:
                    GetUserResponseAndEndTrial();
                    break;
                case Task.VisualSearch:
                    StartCoroutine(ReportTarget());
                    eventClip.Play();
                    break;
            }
        }
        
        
        private void GetUserResponseAndEndTrial()
        { 
            // End the experimental task and waits for the user response (which will conclude the trial)
            _trialDuration = Time.time - _trialStartTime;
            _participantTriggerEnabled = false;
            Debug.Log("COROUTINE:"); //TODO remove
            Debug.Log(_endTrialAfterTimeLimit);
            StopCoroutine(_endTrialAfterTimeLimit);
            Debug.Log(_endTrialAfterTimeLimit);
            startEndClip.Play();
            SingletonRegister.GetInstance<UICallbacks>().DeactivateEndTrialButton();

            // Jump to the pause screen
            SingletonRegister.GetInstance<SceneHandler>().DeactivateAll();
            SingletonRegister.GetInstance<SceneHandler>().JumpToWaitingScreen();
            SingletonRegister.GetInstance<PhospheneSimulator>().DeactivateSimulation();
            
            // Get user response
            StartCoroutine(WaitForUserResponse());
        }
        
        private IEnumerator WaitForUserResponse()
        {
            // Wait for response...
            var UI = SingletonRegister.GetInstance<UICallbacks>();
            switch (CurrentTrial.Task)
            {
                case Task.SceneRecognition:
                    UI.PromptSceneRecognitionResponse("What environment did you see?");
                    yield return new WaitUntil(() => UI.SceneRecognitionResponse != Environment.RoomCategory.None);
                    UI.PromptSubjectiveRating("How confident are you?");
                    yield return new WaitUntil(() => UI.SubjectiveRatingResponse != -1);
                    break;
                
                case Task.VisualSearch:
                    UI.PromptSubjectiveRating("How easy was the task?");
                    yield return new WaitUntil(() => UI.SubjectiveRatingResponse != -1);
                    break;
            }
            
            // ... and end the trial.
            _reportedRoomCategory = UI.SceneRecognitionResponse;
            _reportedSubjectiveRating = UI.SubjectiveRatingResponse;
            EndTrial();
        }
        
        
        
        // Whenever a target is reported, proceed to the next target object (or if last target end the trial)
        private bool _reportingTarget;
        private FocusInfo _pointingTargetHand;
        private FocusInfo _pointingTargetEye;
        private FocusInfo _pointingTargetHead;
        private int _targetIdx = 0;
        
        private IEnumerator ReportTarget()
        {
            Debug.Log($"trg reported ({_reportedEventsCount})");
            
            // Display fixation dot for 1 second
            _reportingTarget = true;
            _participantTriggerEnabled = false;
            SingletonRegister.GetInstance<PhospheneSimulator>().SetFocusDot(1);
            yield return new WaitForSeconds(1);
            SingletonRegister.GetInstance<PhospheneSimulator>().SetFocusDot(0);
            _reportingTarget = false;
            _participantTriggerEnabled = true;
            
            // Next target
            if (!recordingPaused && !lastSecondRecording)
                SingletonRegister.GetInstance<SceneHandler>().NextTargetObject(usePersistentIdx:true);
            // SenorSummarySingletons.GetInstance<SceneHandler>().RandomTargetObject();

        }
        
        
        private FocusInfo GetPointingTarget(Transform parentTransform, bool fromEyeTracker)
        {
            var eyeTracker = SingletonRegister.GetInstance<EyeTracking>();
            var focusInfo = new FocusInfo();
            
            var valid = fromEyeTracker?
                eyeTracker.GetFocusPoint(GazeIndex.COMBINE, out focusInfo):
                eyeTracker.GetFocusInfoFromRayCast(Vector3.forward, out focusInfo, parentTransform);
            return focusInfo; 
        }
        
        
        /// Ending the experiment 
        /// EndTrial() is invoked when the max number of targets is reached, or when manually ended the trial.
        /// The script will continue recording the last second of the trial.

        private bool lastSecondRecording;
        public void ManuallyEndTrial()
        {
            // manuallyEndedTrial = true;
            if (!lastSecondRecording)
            {
                lastSecondRecording = true;
                Invoke(nameof(GetUserResponseAndEndTrial), 1f);
            }
        }

        public void EndTrial()
        {
            StopCoroutine(_endTrialAfterTimeLimit);
            
            DataCollector.Instance.RecordDataEntry(new TrialConfigRecord
            {
                ExperimentalTask = CurrentTrial.Task,
                GazeCondition = CurrentTrial.EyeTrackingCondition,
                EnvironmentName = CurrentTrial.Environment.Name,
                EnvironmentClass = CurrentTrial.Environment.roomCategory,
                Glasses = (Glasses)SingletonRegister.GetInstance<UICallbacks>().glassesDropdown.value,
                GazeRaySensitivity = SingletonRegister.GetInstance<EyeTracking>().GazeRaySensitivity,
                DataDelimiter = Data2File.Delimiter,
                ReportedRoomCategory = _reportedRoomCategory,
                ReportedSubjectiveRating = _reportedSubjectiveRating,
                ReportedEventsCount = _reportedEventsCount,
                TrialDuration = _trialDuration
            });
            PauseRecording();
            lastSecondRecording = false;
            DataCollector.Instance.StopTrial();
            trialCompleted?.Invoke();

            // If this was the last trial, the experiment is now finished
            var nextIdx = GetNextTrialIndices();
            if (nextIdx == null)
            {
                SingletonRegister.GetInstance<SceneHandler>().SetWaitScreenMessage("Finished Experiment");
                experimentCompleted.Invoke();
                return;
            }

            // Check if this was the last trial of this block
            var (blockIdx, trialIdx) = nextIdx;
            if (blockIdx > _currBlockIdx)
                ConcludeBlock();
            
            // Load next trial
            GetNextTrial(true);
        }

        private void ConcludeBlock()
        {
            blockCompleted.Invoke();
            SingletonRegister.GetInstance<UICallbacks>().PostBlock(DataCollector.Instance.GetHandlerRefs());
            SingletonRegister.GetInstance<SceneHandler>().SetWaitScreenMessage("Block completed!");

            //TODO:
            // SenorSummarySingletons.GetInstance<UICallbacks>().BtnPlayground();
            // SenorSummarySingletons.GetInstance<PhospheneSimulator>().StopTrial();
            // SenorSummarySingletons.GetInstance<UICallbacks>().PostBlock(allHandlers);
            // StartNewBlock();
        }


        public void RunCalibrationTest()
        {
            StartCoroutine(CalibrationPerformanceTrial());
        }

        private IEnumerator CalibrationPerformanceTrial()
        {
            
            // Initiate trial
            trialInitiated.Invoke(); // Deactivates the UI buttons
            
            // Create filestream for the calibrationTest results
            DataCollector.Instance.NewTrial(_currBlockIdx, _currTrialIdx, calibrationTest: true);

            // Jump to the calibration screen
            var sceneHandler = SingletonRegister.GetInstance<SceneHandler>();
            sceneHandler.JumpToCalibrationTestScreen();
            var nDots = sceneHandler.CurrentEnvironment.targetObjects.Length;
            Debug.Log($"nDots {nDots}");
            
            // And Go
            recordingPaused = false;
            for (var i = 0; i < nDots; i++)
            {
                sceneHandler.NextTargetObject();
                yield return new WaitForSeconds(1.5f);
            }
            
            // Conclude the trial
            recordingPaused = true;
            trialCompleted.Invoke(); //Activates the GUI buttons
            DataCollector.Instance.StopTrial();
            sceneHandler.JumpToWaitingScreen();
        }
        

    }
}
