using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DataHandling;
using DataHandling.Separated;
using ExperimentControl.UI;
using Simulation;
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
        public CollisionHandler boxCheck;
        public CheckpointHandler zoneCounter;
        public GameObject xrHead;
        public GameObject handLeft;
        internal ControllerVibrator LeftController;
        public GameObject handRight;
        internal ControllerVibrator RightController;
        
        public static RunExperiment Instance { get; private set; }

        private List<List<Tuple<EyeTracking.EyeTrackingConditions, HallwayCreator.Hallways>>> _blocks = new ()
        {
            new()
            {
                new(EyeTracking.EyeTrackingConditions.GazeIgnored, HallwayCreator.Hallways.Hallway1),
                new(EyeTracking.EyeTrackingConditions.SimulationFixedToGaze, HallwayCreator.Hallways.Hallway2),
                new(EyeTracking.EyeTrackingConditions.GazeAssistedSampling, HallwayCreator.Hallways.Hallway3)
            },
            new()
            {
                new(EyeTracking.EyeTrackingConditions.GazeIgnored, HallwayCreator.Hallways.Hallway2),
                new(EyeTracking.EyeTrackingConditions.SimulationFixedToGaze, HallwayCreator.Hallways.Hallway3),
                new(EyeTracking.EyeTrackingConditions.GazeAssistedSampling, HallwayCreator.Hallways.Hallway1)
            },
            new()
            {
                new(EyeTracking.EyeTrackingConditions.GazeIgnored, HallwayCreator.Hallways.Hallway3),
                new(EyeTracking.EyeTrackingConditions.SimulationFixedToGaze, HallwayCreator.Hallways.Hallway1),
                new(EyeTracking.EyeTrackingConditions.GazeAssistedSampling, HallwayCreator.Hallways.Hallway2)
            }
        };
        
        private Data2File TrialConfigHandler { get; set; }
        private Data2File EngineDataHandler { get; set; }
        private Data2File EyeTrackerDataHandler { get; set; }
        private Data2File SingleEyeDataHandler { get; set; }
        private IEnumerable<Data2File> allHandlers; 
        
        internal bool betweenTrials = true;

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
            SingleEyeDataHandler = gameObject.AddComponent<Data2File>();
            SingleEyeDataHandler.DataStructure = typeof(SingleEyeDataRecord);
            allHandlers = new List<Data2File>
            {
                TrialConfigHandler,
                EngineDataHandler,
                EyeTrackerDataHandler,
                SingleEyeDataHandler
            };

            trialCompleted ??= new UnityEvent();
            blockCompleted ??= new UnityEvent();
        }

        private void Start()
        {
            LeftController = handLeft.GetComponentInChildren<ControllerVibrator>();
            RightController = handRight.GetComponentInChildren<ControllerVibrator>();
        }

        private bool lastSecondRecording;
        private bool manuallyEndedTrial;

        private void Update()
        {
            if (!lastSecondRecording && StaticDataReport.InZone >= lastZone && !manuallyEndedTrial)
            {
                lastSecondRecording = true;
                Invoke(nameof(EndTrial), 1f);
            }
        }

        public void ManuallyEndTrial()
        {
            manuallyEndedTrial = true;
            Invoke(nameof(EndTrial), 1f);
        }

        public void EndTrial()
        {
            StaticDataReport.InZone = 0;
            lastZone = int.MaxValue;
            lastSecondRecording = false;
            betweenTrials = true;
            
            manuallyEndedTrial = false;
            
            foreach(var h in allHandlers) h.StopTrial();
            
            trialCompleted?.Invoke();
            StartNewTrial();
        }

        private void FixedUpdate()
        {
            if (betweenTrials) return;

            var record = new EngineDataRecord(); 
            record.TimeStamp = DateTime.Now.Ticks;
            record.XROriginPos = xrOrigin.transform.position;
            record.XROriginRot = xrOrigin.transform.rotation;
            record.XROriginInBox = boxCheck.InBox;
            record.XROriginInCheckpoint = zoneCounter.InCheckpoint;
            record.XRHeadPos = xrHead.transform.position;
            record.XRHeadRot = xrHead.transform.rotation;
            record.HandLPos = handLeft.transform.position;
            record.HandLRot = handLeft.transform.rotation;
            record.HandLInBox = LeftController.inBox;
            record.HandLInWall = LeftController.inWall;
            record.HandRPos = handRight.transform.position;
            record.HandRRot = handRight.transform.rotation;
            record.HandRInBox = RightController.inBox;
            record.HandRInWall = RightController.inWall;
            record.CollisionCount = StaticDataReport.CollisionCount;
            record.CheckpointCount = StaticDataReport.InZone;
            record.FrameCount = Time.frameCount;

            RecordDataEntry(record);
        }

        public void StartExperiment(string subjId)
        {           
            // Shuffle hallway-condition matrix
            _blocks = _blocks.OrderBy(_ => Random.value).ToList();
            _blocks[0] = _blocks[0].OrderBy(_ => Random.value).ToList();
            _blocks[1] = _blocks[1].OrderBy(_ => Random.value).ToList();
            _blocks[2] = _blocks[2].OrderBy(_ => Random.value).ToList();
            
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
            
            // and go
            currBlock = -1;
            StartNewBlock();
        }

        private int currBlock = -1;
        private int currTrial = -1;
        private int lastZone = int.MaxValue;

        public void StartNewBlock()
        {
            currBlock += 1;
            if (currBlock > _blocks.Count - 1)
            {
                ConcludeBlock();
                return;
            }
            
            currTrial = -1;
            
            StartNewTrial();
        }

        private void StartNewTrial()
        {
            currTrial += 1;
            StaticDataReport.InZone = 0;
            lastZone = int.MaxValue;
            StaticDataReport.CollisionCount = 0;

            if (currTrial > _blocks[currBlock].Count - 1)
            {
                ConcludeBlock();
                if (currBlock < _blocks.Count)
                    blockCompleted?.Invoke();
                return;
            }
            
            // update UI
            SenorSummarySingletons.GetInstance<UICallbacks>().UpdateBlockAndTrial(currBlock + 1, currTrial + 1);

            var (condition, hallway) = _blocks[currBlock][currTrial];
            lastZone = HallwayCreator.HallwayObjects[hallway].LastZoneId;

            foreach(var h in allHandlers) h.NewTrial(currBlock, currTrial);
            
            SenorSummarySingletons.GetInstance<InputHandler>().MoveToNewHallway(HallwayCreator.HallwayObjects[hallway]);
            SenorSummarySingletons.GetInstance<PhospheneSimulator>().StartTrial(condition);
            
            betweenTrials = false;
            RecordDataEntry(new TrialConfigRecord
            {
                GazeCondition = condition,
                Hallway = hallway,
                Glasses = (Glasses)SenorSummarySingletons.GetInstance<UICallbacks>().glassesDropdown.value,
                GazeRaySensitivity = SenorSummarySingletons.GetInstance<EyeTracking>().GazeRaySensitivity,
                DataDelimiter = Data2File.Delimiter,
            });
        }

        private void ConcludeBlock()
        {
            SenorSummarySingletons.GetInstance<UICallbacks>().BtnPlayground();
            SenorSummarySingletons.GetInstance<PhospheneSimulator>().StopTrial();
            SenorSummarySingletons.GetInstance<UICallbacks>().PostBlock(allHandlers);
        }

        private void RecordDataEntry(IDataStructure entry, Data2File handler)
        {
            if(betweenTrials) return;
            
            handler.AddRecord(entry);
        }

        public void RecordDataEntry(TrialConfigRecord entry) => RecordDataEntry(entry, TrialConfigHandler);
        public void RecordDataEntry(EngineDataRecord entry) => RecordDataEntry(entry, EngineDataHandler);
        public void RecordDataEntry(EyeTrackerDataRecord entry) => RecordDataEntry(entry, EyeTrackerDataHandler);
        public void RecordDataEntry(SingleEyeDataRecord entry) => RecordDataEntry(entry, SingleEyeDataHandler);

    }
}
