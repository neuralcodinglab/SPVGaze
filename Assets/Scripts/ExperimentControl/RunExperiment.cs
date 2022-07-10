using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DataHandling;
using DataHandling.Separated;
using Simulation;
using UnityEngine;
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
        private ControllerVibrator leftController;
        public GameObject handRight;
        private ControllerVibrator rightController;
        
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
        private IEnumerable<Data2File> _allHandlers; 
        
        private bool trialCompleted = true;

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
            _allHandlers = new List<Data2File>
            {
                TrialConfigHandler,
                EngineDataHandler,
                EyeTrackerDataHandler,
                SingleEyeDataHandler
            };
        }

        private void Start()
        {
            leftController = handLeft.GetComponentInChildren<ControllerVibrator>();
            rightController = handRight.GetComponentInChildren<ControllerVibrator>();
        }

        private void FixedUpdate()
        {
            if (trialCompleted) return;
            
            RecordDataEntry(new EngineDataRecord
            {
                TimeStamp = DateTime.Now.Ticks,
                XROriginPos = xrOrigin.transform.position,
                XROriginRot = xrOrigin.transform.rotation,
                XROriginInBox = boxCheck.InBox,
                XROriginInCheckpoint = zoneCounter.InCheckpoint,
                XRHeadPos = xrHead.transform.position,
                XRHeadRot = xrHead.transform.rotation,
                HandLPos = handLeft.transform.position,
                HandLRot = handLeft.transform.rotation,
                HandLInBox = leftController.inBox,
                HandLInWall = leftController.inWall,
                HandRPos = handRight.transform.position,
                HandRRot = handRight.transform.rotation,
                HandRInBox = rightController.inBox,
                HandRInWall = rightController.inWall,
                CollisionCount = StaticDataReport.CollisionCount,
                CheckpointCount = StaticDataReport.InZone,
                FrameCount = Time.frameCount,
            });
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
            }
            foreach(var h in _allHandlers) h.NewSubject(subjId);
            
            // and go
            StartCoroutine(StartNewBlock(0));
        }

        private IEnumerator StartNewBlock(int blockIdx)
        {
            var trials = _blocks[blockIdx];
            for (var i=0; i < trials.Count; i+=1)
            {
                var (condition, hallway) = trials[i];
                
                EyeParameter eyeparams = new EyeParameter();
                SRanipal_Eye_API.GetEyeParameter(ref eyeparams);
                RecordDataEntry(new TrialConfigRecord()
                {
                    GazeCondition = condition,
                    Hallway = hallway,
                    Glasses = Glasses.Contacts,
                    GazeRaySensitivity = eyeparams.gaze_ray_parameter.sensitive_factor,
                    DataDelimiter = Data2File.Delimiter,
                });
                
                trialCompleted = false;
                StartCoroutine(StartNewTrial(condition, hallway));
                foreach (var h in _allHandlers) h.NewTrial(blockIdx, i);
                yield return new WaitUntil(() => trialCompleted);
            }
        }

        private IEnumerator StartNewTrial(EyeTracking.EyeTrackingConditions condition, HallwayCreator.Hallways hallway)
        {
            SenorSummarySingletons.GetInstance<InputHandler>().MoveToNewHallway(HallwayCreator.HallwayObjects[hallway]);
            SenorSummarySingletons.GetInstance<PhospheneSimulator>().SetGazeTrackingCondition(condition);
            
            yield return new WaitUntil(CheckTrialCompleted);
            
            foreach(var h in _allHandlers) h.StopTrial();
        }

        private bool CheckTrialCompleted()
        {
            trialCompleted = StaticDataReport.InZone > 5;
            return trialCompleted;
        }

        private void RecordDataEntry(IDataStructure entry, Data2File handler)
        {
            if(trialCompleted) return;
            
            handler.AddRecord(entry);
        }

        public void RecordDataEntry(TrialConfigRecord entry) => RecordDataEntry(entry, TrialConfigHandler);
        public void RecordDataEntry(EngineDataRecord entry) => RecordDataEntry(entry, EngineDataHandler);
        public void RecordDataEntry(EyeTrackerDataRecord entry) => RecordDataEntry(entry, EyeTrackerDataHandler);
        public void RecordDataEntry(SingleEyeDataRecord entry) => RecordDataEntry(entry, SingleEyeDataHandler);

    }
}
