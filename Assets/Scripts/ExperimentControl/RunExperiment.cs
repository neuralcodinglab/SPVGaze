using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DataHandling;
using DataHandling.Separated;
using ExperimentControl.UI;
using mattmc3.dotmore.Extensions;
using Simulation;
using Unity.VisualScripting;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using ViveSR.anipal;
using ViveSR.anipal.Eye;
using Random = UnityEngine.Random;

namespace ExperimentControl
{
    public class RunExperiment : MonoBehaviour
    {
        public GameObject xrOrigin;
        // public CollisionHandler boxCheck;
        // public CheckpointHandler zoneCounter;
        public GameObject xrHead;
        public GameObject handLeft;
        internal ControllerVibrator LeftController;
        public GameObject handRight;
        internal ControllerVibrator RightController;
        public GameObject waitingScreen;
        [SerializeField] private AudioSource eventClip;
        [SerializeField] private AudioSource startEndClip;

        public static RunExperiment Instance { get; private set; }
        private Data2File TrialConfigHandler { get; set; }
        private Data2File EngineDataHandler { get; set; }
        private Data2File EyeTrackerDataHandler { get; set; }
        private Data2File SingleEyeDataHandlerL { get; set; }
        private Data2File SingleEyeDataHandlerR { get; set; }
        private Data2File SingleEyeDataHandlerC { get; set; }
        private IEnumerable<Data2File> allHandlers; 
        
        internal bool recordingPaused = true;

        public UnityEvent resumedRecording;
        public UnityEvent trialCompleted;
        public UnityEvent blockCompleted;

        private void Awake()
        {
            if (Instance != null)
                throw new InvalidOperationException("Can only have 1 'RunExperiment' class active");
            Instance = this;

            TrialConfigHandler = gameObject.AddComponent<Data2File>();
            TrialConfigHandler.DataStructure = typeof(TrialConfigRecord);
            EngineDataHandler = gameObject.AddComponent<Data2File>();
            EngineDataHandler.DataStructure = typeof(EngineDataRecord);
            EyeTrackerDataHandler = gameObject.AddComponent<Data2File>();
            EyeTrackerDataHandler.DataStructure = typeof(EyeTrackerDataRecord);
            SingleEyeDataHandlerL = gameObject.AddComponent<Data2File>();
            SingleEyeDataHandlerL.DataStructure = typeof(SingleEyeDataRecord);
            SingleEyeDataHandlerL.FileName += "L";
            SingleEyeDataHandlerR = gameObject.AddComponent<Data2File>();
            SingleEyeDataHandlerR.DataStructure = typeof(SingleEyeDataRecord);
            SingleEyeDataHandlerR.FileName += "R";
            SingleEyeDataHandlerC = gameObject.AddComponent<Data2File>();
            SingleEyeDataHandlerC.DataStructure = typeof(SingleEyeDataRecord);
            SingleEyeDataHandlerC.FileName += "C";
            allHandlers = new List<Data2File>
            {
                TrialConfigHandler,
                EngineDataHandler,
                EyeTrackerDataHandler,
                SingleEyeDataHandlerL,
                SingleEyeDataHandlerR,
                SingleEyeDataHandlerC
            };

            resumedRecording ??= new UnityEvent();
            trialCompleted ??= new UnityEvent();
            blockCompleted ??= new UnityEvent();
        }

        private void Start()
        {
            LeftController = handLeft.GetComponentInChildren<ControllerVibrator>();
            RightController = handRight.GetComponentInChildren<ControllerVibrator>();
        }
        
        /// Data recording
        /// at each fixed update, an entry is recorded for the unity engine data
        /// the other datapoints are recorded upon the start of the trial and start of the experiment.
        
        
        private void RecordDataEntry(IDataStructure entry, Data2File handler)
        {
            if(recordingPaused) return;
            
            handler.AddRecord(entry);
        }

        public void RecordDataEntry(TrialConfigRecord entry) => RecordDataEntry(entry, TrialConfigHandler);
        public void RecordDataEntry(EngineDataRecord entry) => RecordDataEntry(entry, EngineDataHandler);
        public void RecordDataEntry(EyeTrackerDataRecord entry) => RecordDataEntry(entry, EyeTrackerDataHandler);

        public void RecordDataEntry(SingleEyeDataRecord entry)
        {
            switch (entry.EyeIndex)
            {
                case GazeIndex.LEFT:
                    RecordDataEntry(entry, SingleEyeDataHandlerL);
                    break;
                case GazeIndex.RIGHT:
                    RecordDataEntry(entry, SingleEyeDataHandlerR);
                    break;
                case GazeIndex.COMBINE:
                    RecordDataEntry(entry, SingleEyeDataHandlerC);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        private void FixedUpdate()
        {
            if (recordingPaused) return;

            var record = new EngineDataRecord(); 
            record.TimeStamp = DateTime.Now.Ticks;
            record.XROriginPos = xrOrigin.transform.position;
            record.XROriginRot = xrOrigin.transform.rotation;
            record.XRHeadPos = xrHead.transform.position;
            record.XRHeadRot = xrHead.transform.rotation;
            record.HandLPos = handLeft.transform.position;
            record.HandLRot = handLeft.transform.rotation;
            record.HandRPos = handRight.transform.position;
            record.HandRRot = handRight.transform.rotation;
            record.CollisionCount = StaticDataReport.CollisionCount;
            record.FrameCount = Time.frameCount;
            record.ReportedEventsCount = _reportedEventsCount;
            record.ReportedRoomCategory = _reportedRoomCategory;
            RecordDataEntry(record);
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


        private List<List<Trial>> _blocks;

        
        
        
        private List<List<Trial>> GetExperimentalBlocks()
        {
            // Shorthand for the gaze tracking conditions 
            var (cond1, cond2, cond3) =
                (EyeTracking.EyeTrackingConditions.GazeIgnored, 
                    EyeTracking.EyeTrackingConditions.GazeAssistedSampling, 
                    EyeTracking.EyeTrackingConditions.SimulationFixedToGaze);
            
            // Get all environments, split in practice and experiment set and randomly shuffle 
            var allEnvs = SenorSummarySingletons.GetInstance<SceneHandler>().GetEnvironments().ToList();
            var practiceEnvs = allEnvs.Where(value => value.practiceEnv).ToList();
            var experimentEnvs = allEnvs.Where(value => value.practiceEnv == false).ToList();
            experimentEnvs = experimentEnvs.OrderBy(_ => Random.value).ToList();
            
            Debug.Log($"Practice envs: {practiceEnvs.Count}, Exp. envs: {experimentEnvs.Count}, total: {allEnvs.Count}");

            // Practice blocks
            var freePractBlock = new List<Trial>() {new Trial(Task.FreePractice, cond1, practiceEnvs[0], 180)};
            
            var prBlockCond1 = new List<Trial>()
            {
                new Trial(Task.VisualSearch, cond1, practiceEnvs[0], 90), 
                new Trial(Task.SceneRecognition, cond1, practiceEnvs[1], 60), 
            };
            var prBlockCond2 = new List<Trial>()
            {
                new Trial(Task.VisualSearch, cond2, practiceEnvs[0], 90),
                new Trial(Task.SceneRecognition, cond2, practiceEnvs[2], 60),
            };
            
            var prBlockCond3 = new List<Trial>()
            {
                new Trial(Task.VisualSearch, cond3, practiceEnvs[0], 90),
                new Trial(Task.SceneRecognition, cond2, practiceEnvs[3], 60),
            };

            // Scene Recognition blocks (4 scene recognition trials, preceded with 1 visual search trial)
            var srBlockCond1 = new List<Trial>() ;
            srBlockCond1.Add(new Trial(Task.VisualSearch, cond1, practiceEnvs[0], 90));
            foreach (var env in experimentEnvs.GetRange(0, 3))
                srBlockCond1.Add(new Trial(Task.SceneRecognition, cond1, env, 90));
        
            var srBlockCond2 = new List<Trial>();
            srBlockCond2.Add(new Trial(Task.VisualSearch, cond2, practiceEnvs[0], 90));
            foreach (var env in experimentEnvs.GetRange(3, 3))
                srBlockCond2.Add(new Trial(Task.SceneRecognition, cond2, env, 90));
        
            var srBlockCond3 = new List<Trial>();
            srBlockCond3.Add(new Trial(Task.VisualSearch, cond3, practiceEnvs[0], 90));
            foreach (var env in experimentEnvs.GetRange(6, 3))
                srBlockCond3.Add(new Trial(Task.SceneRecognition, cond3, env, 90));

            // Visual search blocks 
            Trial VSTrial(EyeTracking.EyeTrackingConditions condition) => 
                new Trial(Task.VisualSearch, condition, practiceEnvs[0], 120);
            var vsBlockCond1 = new List<Trial>() {VSTrial(cond1)};
            var vsBlockCond2 = new List<Trial>() {VSTrial(cond2)};
            var vsBlockCond3 = new List<Trial>() {VSTrial(cond3)};
            
            // Blocks (each block is a List of trials) are grouped into Sessions (each session is a list of blocks)
            List<List<Trial>> RandomizeOrder(List<Trial> block1, List<Trial> block2, List<Trial> block3)
            {
                var session = new List<List<Trial>>() {block1, block2, block3};
                return session.OrderBy(_ => Random.value).ToList();
            }

            var freePracticeSession = new List<List<Trial>>() { freePractBlock };
            var taskPracticeSession = RandomizeOrder(prBlockCond1, prBlockCond2, prBlockCond3);
            var sceneRecSession1 = RandomizeOrder(srBlockCond1, srBlockCond2, srBlockCond3);
            // var sceneRecSession2 = RandomizeOrder(srBlockCond1, srBlockCond2, srBlockCond3);
            var visSearchSession1 = RandomizeOrder(vsBlockCond1, vsBlockCond2, vsBlockCond3);
            var visSearchSession2 = RandomizeOrder(vsBlockCond1, vsBlockCond2, vsBlockCond3);

            return freePracticeSession
                .Concat(taskPracticeSession).ToList()
                .Concat(sceneRecSession1).ToList()
                 // .Concat(sceneRecSession2).ToList()
                 .Concat(visSearchSession1).ToList()
                 .Concat(visSearchSession2).ToList(); 
        }
        

        public void StartExperiment(string subjId)
        {
            _blocks = GetExperimentalBlocks();
            Debug.Log($"loaded {_blocks.Count} blocks:");
           
            //TODO: Remove
            var i = 0;
            foreach (var block in _blocks)
            {
                i += 1;
                var lastTrial = block[^1];
                Debug.Log($"Block {i}, {block.Count} trials ({lastTrial.Task}, {lastTrial.EyeTrackingCondition})");
            }
            

            // create folders and files
            var subjectDir = Path.Join(Application.persistentDataPath, subjId);
            if (Directory.Exists(subjectDir))
            {
                var tmp = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
                Debug.LogWarning($"Subject Directory for {subjId} exists. Replacing with {tmp}");
                subjId = tmp;
                SenorSummarySingletons.GetInstance<UICallbacks>().subjID.text = subjId;
            }
            foreach(var h in allHandlers) h.NewSubject(subjId);

            // Register events
            
            
            // Jump to the waiting screen
            resumedRecording.AddListener(OnStartTrial);
            SenorSummarySingletons.GetInstance<SceneHandler>().DeactivateAll();
            PauseRecording();
            
            
            // And start the first block
            _currBlockIdx = -1;
            StartNewBlock();
        }
        
        // 
        private void OnStartTrial()
        {
            startEndClip.Play();
            StartCoroutine(EndTrialAfterTimeLimit());
            switch (_currTrial.Task)
            {
                case Task.FreePractice:
                    SenorSummarySingletons.GetInstance<SceneHandler>().ActivateAllDefaultTargets();
                    break;
                case Task.VisualSearch:
                    SenorSummarySingletons.GetInstance<SceneHandler>().RandomTargetObject();
                    break;
            }
        }

        private IEnumerator EndTrialAfterTimeLimit()
        {
            var trialStarted = _currTrial;
            yield return  new WaitForSeconds(_currTrial.MaxDuration);
            if ((trialStarted == _currTrial) & (!lastSecondRecording) & (!recordingPaused))
            {
                startEndClip.Play();
                if (_currTrial.Task == Task.SceneRecognition)
                {
                    _currTrial.Task = Task.None;
                    ReportSceneRecognitionResponse();
                }
                else
                {
                    EndTrial();
                }
            }

        }
        
        private int _nTargets = int.MaxValue;
        private int _currBlockIdx = -1;
        private int _currTrialIdx = -1;
        private Trial _currTrial;
        
        public void StartNewBlock()
        {
            _currBlockIdx += 1;
            if (_currBlockIdx > _blocks.Count - 1)
            {
                Debug.Log("Finished the final block");
                // TODO:
                // ConcludeBlock();
                return;
            }
            Debug.Log($"Starting new block ({_blocks[_currBlockIdx].Count} Trials. Type: {_blocks[_currBlockIdx][^1].Task})");
            _currTrialIdx = -1;
            
            StartNewTrial();
        }

        public void NextTrial(InputAction.CallbackContext ctx) => StartNewTrial();
        
        private void StartNewTrial()
        {
            SenorSummarySingletons.GetInstance<UICallbacks>().DeactivateNextTrialButton();
            SenorSummarySingletons.GetInstance<UICallbacks>().DeactivateNavigationBtns();
            _currTrialIdx += 1;
            
            // If the last trial of the block -> conclude this block.
            if (_currTrialIdx > _blocks[_currBlockIdx].Count - 1)
            {
                Debug.Log("Finished last trial of the block");
                ConcludeBlock();
                if (_currBlockIdx < _blocks.Count)
                    blockCompleted?.Invoke();
                return;
            }
            
            // Otherwise, update the current trial variables, and update the UI
            _currTrial = _blocks[_currBlockIdx][_currTrialIdx];
            SenorSummarySingletons.GetInstance<UICallbacks>().UpdateBlockAndTrial(_currBlockIdx + 1, _currTrialIdx + 1);
            _reportedRoomCategory = Environment.RoomCategory.None;
            _reportedEventsCount = 0;
            _nTargets = _currTrial.Environment.targetObjects.Length;
            Debug.Log($"Starting new Trial (condition: {_currTrial.EyeTrackingCondition}, environment {_currTrial.Environment.Name}");

            // Start the recording 
            foreach(var h in allHandlers) h.NewTrial(_currBlockIdx, _currTrialIdx);
            StartCoroutine(ResumeRecording(5, "Get Ready (Scene recognition)"));
            RecordDataEntry(new TrialConfigRecord
            {
                GazeCondition = _currTrial.EyeTrackingCondition,
                EnvironmentName = _currTrial.Environment.Name,
                EnvironmentClass = _currTrial.Environment.roomCategory,
                Glasses = (Glasses)SenorSummarySingletons.GetInstance<UICallbacks>().glassesDropdown.value,
                GazeRaySensitivity = SenorSummarySingletons.GetInstance<EyeTracking>().GazeRaySensitivity,
                DataDelimiter = Data2File.Delimiter,
            });
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
            SenorSummarySingletons.GetInstance<SceneHandler>().JumpToWaitingScreen();
            SenorSummarySingletons.GetInstance<PhospheneSimulator>().DeactivateSimulation();
        }
        
        
        private IEnumerator ResumeRecording(int waitseconds, string message)
        {
            SenorSummarySingletons.GetInstance<SceneHandler>().SetWaitScreenMessage(_currTrial.Task.HumanName());
            yield return  new WaitForSeconds(waitseconds);
            SenorSummarySingletons.GetInstance<SceneHandler>().JumpToEnvironment(_currTrial.Environment);
            SenorSummarySingletons.GetInstance<PhospheneSimulator>().ActivateSimulation(_currTrial.EyeTrackingCondition);
            resumedRecording?.Invoke();
            recordingPaused = false;
        }
        
        public void TogglePauseExperiment()
        {
            if (recordingPaused)
            {
                Debug.Log("Manually resumed experiment");
                StartCoroutine(ResumeRecording(3, "Get Ready!"));
            }
            else 
                PauseRecording();
        }
        
        
        /// Participant response 
        /// 
        /// In the scene recognition task, the participant hits a trigger to indicate that they recognize the scene 
        /// In the visual search task, the participant points and triggers whenever they found a target

        public void OnParticipantTrigger1(InputAction.CallbackContext ctx) => OnParticipantTrigger1();
        private void OnParticipantTrigger1()
        {
            // Ignore if not currently recording
            Debug.Log($"Participant pressed trigger button! (Experiment is paused: {recordingPaused})");
            if (recordingPaused) return;
            
            _reportedEventsCount += 1;
            switch (_currTrial.Task)
            {
                case Task.None:
                    Debug.Log("Ignoring repeated trigger...");
                    break;
                case Task.FreePractice:
                    eventClip.Play();
                    SenorSummarySingletons.GetInstance<PhospheneSimulator>()
                        .ToggleSimulationActive();
                    break;
                case Task.SceneRecognition:
                    _currTrial.Task = Task.None;
                    ReportSceneRecognitionResponse(); 
                    startEndClip.Play();
                    break;
                case Task.VisualSearch:
                    StartCoroutine(ReportTarget());
                    ReportTarget();
                    eventClip.Play();
                    break;
            }
        }
        
        // After the scene is recognized the script waits for user input (reported answer is registered by the experimenter) 
        private void ReportSceneRecognitionResponse()
        {
            Debug.Log($"Scene is recognized by subject ({_reportedEventsCount} out of {_nTargets})");
            StopCoroutine(EndTrialAfterTimeLimit());
            StartCoroutine(WaitForSceneRecognitionResponse());
        }
        
        private IEnumerator WaitForSceneRecognitionResponse()
        {
            // Jump to the pause screen
            SenorSummarySingletons.GetInstance<SceneHandler>().JumpToWaitingScreen();
            SenorSummarySingletons.GetInstance<PhospheneSimulator>().DeactivateSimulation();
            SenorSummarySingletons.GetInstance<SceneHandler>().SetWaitScreenMessage("What environment did you see?");
            
            // Wait for response...
            var UI = SenorSummarySingletons.GetInstance<UICallbacks>();
            UI.SceneRecognitionResponse = Environment.RoomCategory.None;
            UI.DeactivateEndTrialButton();
            UI.ActivateSceneResponseBtns();
            yield return new WaitUntil(() => UI.SceneRecognitionResponse != Environment.RoomCategory.None);
            
            // ... and end the trial.
            _reportedRoomCategory = UI.SceneRecognitionResponse;
            UI.DeactivateSceneResponseBtns();
            EndTrial();
            
        }
        
        
        
        // Whenever a target is reported, proceed to the next target object (or if last target end the trial)
        private IEnumerator ReportTarget()
        {
            Debug.Log($"trg reported ({_reportedEventsCount} out of {_nTargets})");
            
            // Display fixation dot for 1 second
            SenorSummarySingletons.GetInstance<PhospheneSimulator>().ToggleFocusDot();
            yield return new WaitForSeconds(1);
            SenorSummarySingletons.GetInstance<PhospheneSimulator>().ToggleFocusDot();
            
            // Next target
            if (!recordingPaused && !lastSecondRecording)
                SenorSummarySingletons.GetInstance<SceneHandler>().RandomTargetObject();
        }
        
        /// Ending the experiment 
        /// EndTrial() is invoked when the max number of targets is reached, or when manually ended the trial.
        /// The script will continue recording the last second of the trial.

        private bool lastSecondRecording;
        public void ManuallyEndTrial()
        {
            // manuallyEndedTrial = true;
            if (!lastSecondRecording) Invoke(nameof(EndTrial), 1f);
        }

        public void EndTrial()
        {
            StopCoroutine(EndTrialAfterTimeLimit());
            SenorSummarySingletons.GetInstance<SceneHandler>().DeactivateAll();
            PauseRecording();
            _nTargets = int.MaxValue;
            lastSecondRecording = false;
            foreach(var h in allHandlers) h.StopTrial();
            trialCompleted?.Invoke();
            UpdateNextTrialDescription();
        }


        private void UpdateNextTrialDescription()
        {
            // Update the next trial description in the UI monitor 
            
            
            if (_currTrialIdx < _blocks[_currBlockIdx].Count - 1)
                // If this was not the last trial of block, display the current idx and the num. of total trials 
                SenorSummarySingletons.GetInstance<UICallbacks>()
                    .SetNextTrialValue($" {_blocks[_currBlockIdx][_currTrialIdx+1].Task}" +
                                       $"({_blocks[_currBlockIdx].Count - _currTrialIdx - 1} left)");
            else
            {
                // If this was the last trial of the block, display the task and condition in the next block
                SenorSummarySingletons.GetInstance<SceneHandler>().SetWaitScreenMessage("Block Completed");
                if (_currBlockIdx < _blocks.Count - 1)
                {
                    var nextTrial = _blocks[_currBlockIdx + 1][0];
                    SenorSummarySingletons.GetInstance<UICallbacks>()
                        .SetNextTrialValue($"{nextTrial.Task} ({nextTrial.EyeTrackingCondition})");
                }
                else
                    // If this was the very last trial, display 'finished' message
                    SenorSummarySingletons.GetInstance<UICallbacks>().SetNextTrialValue("Finished everything!");
            }
        }
        
        private void ConcludeBlock()
        {
            //TODO:
            // SenorSummarySingletons.GetInstance<UICallbacks>().BtnPlayground();
            // SenorSummarySingletons.GetInstance<PhospheneSimulator>().StopTrial();
            // SenorSummarySingletons.GetInstance<UICallbacks>().PostBlock(allHandlers);
            StartNewBlock();
        }

        public void NavigatePreviousTrial()
        {
            _currTrialIdx -= 1;
            
            // If the last trial of this block, navigate to next block
            if (_currTrialIdx < 0)
            {
                _currBlockIdx -= 1;
                if (_currBlockIdx < 0)
                {
                    // If out of bounds, don't decrease the value further.
                    _currBlockIdx = 0; 
                    _currTrialIdx = -1; 
                }
                else
                    _currTrialIdx = _blocks[_currBlockIdx].Count;
            }
            SenorSummarySingletons.GetInstance<UICallbacks>().UpdateBlockAndTrial(_currBlockIdx + 1, _currTrialIdx + 1);
        }

        public void NavigateNextTrial()
        {
            _currTrialIdx += 1;
            
            // If the last trial of this block, navigate to next block
            if (_currTrialIdx > _blocks[_currBlockIdx].Count - 1)
            {
                _currBlockIdx += 1;
                if (_currBlockIdx > _blocks.Count - 1)
                {
                    // If out of bounds, don't increase the value further. 
                    _currBlockIdx -= 1;
                    _currTrialIdx -= 1;
                }
                else
                    // First trial of next blok
                    _currTrialIdx = 0;
                
            }
            SenorSummarySingletons.GetInstance<UICallbacks>().UpdateBlockAndTrial(_currBlockIdx + 1, _currTrialIdx + 1);
        }


    }
}
