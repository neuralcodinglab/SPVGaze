using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExperimentControl;
using Xarphos;
using Xarphos.Simulation;
using Xarphos.DataCollection;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using ViveSR.anipal.Eye;
using Environment = ExperimentControl.Environment;
using TrialConfigRecord = ExperimentControl.TrialConfigRecord;


namespace DataHandling.Separated
{ 
    // ToDo: Fix to match with disentangled data collection
    public class DataReplay : MonoBehaviour
    {
        // [SerializeField] private GameObject XR_Origin;
        // [SerializeField] private GameObject playerCamera;
        // [SerializeField] private GameObject leftHandController;
        // [SerializeField] private GameObject rightHandController;
        // [SerializeField] private LineRenderer GazeLineRenderer;
        // [SerializeField] private LineRenderer ControllerLineRenderer;

        // private double _frameMismatchTolerance = 1E7; //The tolerance value for mismatching timestamps between eye and engine data

        // private bool _replayFinished = true;
        // private PhospheneSimulator simulator;
        // private EngineDataRecord engineRecord;
        // private string currSubject;
        // private string currTrial;

        // private void DeactivateInputControl()
        // {
        //     playerCamera.GetComponent<TrackedPoseDriver>().enabled=false;
        //     leftHandController.GetComponent<ActionBasedController>().enabled=false;
        //     rightHandController.GetComponent<ActionBasedController>().enabled = false;
        // }

        // public void ReplayTrial(string subjId, string trialId)
        // {
        //     var pathBase = Path.Join(Application.persistentDataPath, subjId, trialId);
        //     var trialConfig = ReadTrialConfigRecordFromCsv($"{pathBase}TrialConfigRecord.tsv");
        //     var engineDataSequence = ReadEngineRecordsFromCsv($"{pathBase}EngineDataRecord.tsv");
        //     var eyeDataSequence = ReadEyeDataRecordsFromCsv($"{pathBase}SingleEyeDataRecordC.tsv");
            
            
        //     StartCoroutine(ReplayData(trialConfig, engineDataSequence, eyeDataSequence));
        // }

        // private IEnumerator ReplayData(TrialConfigRecord trialConfig ,List<EngineDataRecord> engineDataSequence, List<SingleEyeDataRecord> eyeDataSequence)
        // {
        //     yield return new WaitForSeconds(5f);
            
        //     // Deactivate Normal input from the VR system
        //     DeactivateInputControl();
        //     GazeLineRenderer.enabled = true;
        //     ControllerLineRenderer.enabled = true;
            
        //     // Environment and phosphene simulator
        //     SingletonRegister.GetInstance<SceneHandler>().JumpToEnvironment(trialConfig.EnvironmentName);
        //     simulator = SingletonRegister.GetInstance<PhospheneSimulator>();
            
        //     // Keep track of the reported events
        //     var reportedTrgIdx = 0;
        //     var currentTarget = "None";
        //     var reportingTarget = false;

        //     // Loop over frames
        //     var frame = 0;
        //     foreach (var engRecord in engineDataSequence)
        //     {
        //         engineRecord = engRecord;
        //         // Activate target object
        //         if (engineRecord.ActiveTarget != currentTarget) 
        //         {
        //             currentTarget = engineRecord.ActiveTarget;
        //                 reportingTarget = false;
        //                 Debug.Log($"Current target switched to {currentTarget}");
        //                 if (currentTarget != "None")
        //                     SingletonRegister.GetInstance<SceneHandler>().ActivateTargetObject(currentTarget);
        //         }
                
        //         // Step to next frame
        //         frame += 1;
        //         if ((frame % 5) != 0)
        //             continue;
                
        //         // Target report starts when event trigger was pressed
        //         if (engineRecord.ReportedEventsCount != reportedTrgIdx)
        //             reportingTarget = true;
                
        //         // if (!reportingTarget)
        //         //         continue;

        //         // Find eyeRecord with the closest timestamp, and Step to next frame
        //         var eyeRecord = eyeDataSequence.OrderBy(x => Math.Abs(x.TimeStamp - engineRecord.TimeStamp)).First();
        //         if (Math.Abs(eyeRecord.TimeStamp - engineRecord.TimeStamp) > _frameMismatchTolerance)
        //             throw new InvalidDataException($"No matching eye data for engine data with TimeStamp" +
        //                                            $" {engineRecord.TimeStamp}. Closest eye data TimeStamp was {eyeRecord.TimeStamp}" +
        //                                            $"(mismatch: {Math.Abs(eyeRecord.TimeStamp-engineRecord.TimeStamp)}");
        //         Step(engineRecord, eyeRecord); 
        //         yield return new WaitForSeconds(0.00000002f);  //TODO: incorporate actual framerate

        //         // // Save frames to disk
        //         // if (reportingTarget)
        //         // {
        //         //                         
        //         //     // Set line renderers and eye position
        //         //     var eyeTracking = SenorSummarySingletons.GetInstance<EyeTracking>();
        //         //     eyeTracking.SetCustomFixationPoint(engineRecord.PointLocationEye, (int)eyeRecord.TimeStamp);
        //         //     
        //         //     // Save simulation frames
        //         //     reportedTrgIdx = engineRecord.ReportedEventsCount;
        //         //     var directory = $"E:/SPVGazeData/Screenshots/{subjId}_{trialId}_target{reportedTrgIdx-1:D2}_{currentTarget}";
        //         //     var filename = $"{frame:D5}.png";
        //         //     
        //         //     // Save Plain frames
        //         //     SwitchSimMode(SimulationMode.Plain, trialConfig.GazeCondition);
        //         //     SaveSimulationFrames(directory, filename, SimulationMode.Plain);
        //         //
        //         //     SwitchSimMode(SimulationMode.Edges, trialConfig.GazeCondition);
        //         //     SaveSimulationFrames(directory, filename, SimulationMode.Edges);
        //         //
        //         //     SwitchSimMode(SimulationMode.Phosphenes, trialConfig.GazeCondition);
        //         //     SaveSimulationFrames(directory, filename, SimulationMode.Phosphenes);
        //         // }
                
        //         // Save simulation frames
        //         reportedTrgIdx = engineRecord.ReportedEventsCount;
        //         // var directory = $"C:\\Users\\Jaap de Ruyter\\Pictures\\GazeVideos\\{currSubject}\\{currTrial}";

        //         var directory = $"E:\\SPVGazeData\\NewVideos\\{currSubject}\\{currTrial}";
        //         var filename = $"{frame:D5}.png";
        //         SaveSimulationFrames(directory, filename, SimulationMode.Phosphenes);
        //         yield return new WaitForSeconds(0.2f);
        //     }
        //     _replayFinished = true;
        // }

        // enum SimulationMode
        // {
        //     Plain = 0,
        //     Edges = 1,
        //     Phosphenes = 3,
        // }

        // private void SaveSimulationFrames(string directory, string filename, SimulationMode simMode)
        // {
        //     if (!Directory.Exists(directory))
        //     {
        //         Directory.CreateDirectory(directory);
        //         Directory.CreateDirectory($"{directory}/{simMode.ToString()}");
        //     }
        //     ScreenCapture.CaptureScreenshot($"{directory}/plain/{filename}", 
        //         ScreenCapture.StereoScreenCaptureMode.LeftEye);
        // }
        
        // private IEnumerator SwitchSimMode(SimulationMode simMode, EyeTracking.EyeTrackingConditions gazeCondition)
        // {
            
        //     switch (simMode)
        //     {
        //         case SimulationMode.Plain:
        //             // Screenshot natural sight
        //             simulator.SetFocusDot(1);
        //             SetLineRender(GazeLineRenderer, engineRecord.XRHeadPos, engineRecord.PointLocationEye, true);
        //             SetLineRender(ControllerLineRenderer, engineRecord.HandRPos, engineRecord.PointLocationHand, true);
        //             yield return new WaitForSeconds(.2f);
        //             break;
        //         case SimulationMode.Edges:
        //             // Deactivate line renderers
        //             GazeLineRenderer.enabled = false;
        //             ControllerLineRenderer.enabled = false;
        //             simulator.ActivateImageProcessing();
        //             simulator.SetFocusDot(1);
        //             simulator.DeactivateSimulation();
        //             yield return new WaitForSeconds(.2f);
        //             break;
        //         case SimulationMode.Phosphenes:
        //             simulator.ActivateSimulation(gazeCondition);
        //             simulator.SetFocusDot(1);
        //             yield return new WaitForSeconds(.2f);
        //             break;
        //     }
        // }

        // private void SetLineRender(LineRenderer lineRenderer, Vector3 origin, Vector3 tip, bool useWorldSpace)
        // {
        //     // Configure line renderer positions (world space or local space coordinates ) 
        //     lineRenderer.enabled = true;
        //     lineRenderer.SetPosition(0,origin); 
        //     lineRenderer.SetPosition(1,tip);
        //     lineRenderer.useWorldSpace = useWorldSpace;
        // }
        // private void ResetLineRenderer(LineRenderer lineRenderer) // Reset to local coordinates
        //     => SetLineRender(lineRenderer, Vector3.zero, Vector3.forward * 100, false);

        // private void Step(EngineDataRecord engineRecord, SingleEyeDataRecord eyeDataRecord)
        // {
        //     XR_Origin.transform.position = engineRecord.XROriginPos;
        //     XR_Origin.transform.rotation = engineRecord.XROriginRot;
            
        //     playerCamera.transform.position = engineRecord.XRHeadPos;
        //     playerCamera.transform.rotation = engineRecord.XRHeadRot;
            
        //     leftHandController.transform.position = engineRecord.HandLPos;
        //     leftHandController.transform.rotation = engineRecord.HandLRot;
            
        //     rightHandController.transform.position = engineRecord.HandRPos;
        //     rightHandController.transform.rotation = engineRecord.HandRRot;

        //     // var focusInfo = new FocusInfo();
        //     // var eyeTracking = SenorSummarySingletons.GetInstance<EyeTracking>();
        //     // eyeTracking.GetFocusInfoFromRayCast(-eyeDataRecord.GazeDirectionNormInEye, out focusInfo);
        //     // eyeTracking.SetCustomFixationPoint(focusInfo.point, (int)eyeDataRecord.TimeStamp);
        //     // GazeLineRenderer.SetPosition(1,100*eyeDataRecord.GazeDirectionNormInEye);
        // }

        // public static TrialConfigRecord ReadTrialConfigRecordFromCsv(string path)
        // {
        //     var record = new TrialConfigRecord();
            
        //     string content = File.ReadAllText(path);
        //     string[] rows = content.Split('\n');
        //     string[] header = rows[0].Split('\t');
        //     string[] values = rows[1].Split("\t");

        //     // Read string values
        //     var task = values[Array.IndexOf(header, "ExperimentalTask")];
        //     var gazeCondition= values[Array.IndexOf(header, "GazeCondition")];
        //     var envName = values[Array.IndexOf(header, "EnvironmentName")];
        //     var envClass = values[Array.IndexOf(header, "EnvironmentClass")];
        //     var glasses = values[Array.IndexOf(header, "Glasses")];
        //     var gazeRaySensitivity = values[Array.IndexOf(header, "GazeRaySensitivity")];
        //     var delimiter = values[Array.IndexOf(header, "DataDelimiter")];
        //     var reportedRoomCat = values[Array.IndexOf(header, "ReportedRoomCategory")];
        //     var subjRating = values[Array.IndexOf(header, "ReportedSubjectiveRating")];
        //     var nEvents = values[Array.IndexOf(header, "ReportedEventsCount")];
        //     var trialDuration = values[Array.IndexOf(header, "TrialDuration")];
            
        //     // Cast string values to TrialConfigRecord
        //     record.ExperimentalTask = Enum.Parse<RunExperiment.Task>(task);
        //     record.GazeCondition = Enum.Parse<EyeTracking.EyeTrackingConditions>(gazeCondition);
        //     record.EnvironmentName = envName;
        //     record.EnvironmentClass = Enum.Parse<ExperimentControl.Environment.RoomCategory>(envClass);
        //     record.Glasses = Enum.Parse<Glasses>(glasses);
        //     record.GazeRaySensitivity = double.Parse(gazeRaySensitivity);
        //     record.DataDelimiter = delimiter;
        //     record.ReportedRoomCategory = Enum.Parse<Environment.RoomCategory>(reportedRoomCat);
        //     record.ReportedSubjectiveRating = int.Parse(subjRating);
        //     record.ReportedEventsCount = int.Parse(nEvents);
        //     record.TrialDuration = float.Parse(trialDuration);
        //     return record;
        // }
        // public static List<EngineDataRecord> ReadEngineRecordsFromCsv(string path)
        // {
        //     // Output sequence
        //     var outputSequence = new List<EngineDataRecord>();
            
        //     // Read the CSV
        //     string content = File.ReadAllText(path);
        //     string[] rows = content.Split('\n');
        //     string[] header = rows[0].Split('\t');
          
        //     // Iterate over the rows 
        //     for (int rowIdx = 1; rowIdx < rows.Length -1; rowIdx++)
        //     {
        //         // Search in the header file where to find the value, and store the elements (x,y,z,w) as a string-array
        //         string[] values = rows[rowIdx].Split("\t");
        //         var orgPos = values[Array.IndexOf(header, "XROriginPos")].Replace("(","").Replace(")", "").Split(",");
        //         var orgRot = values[Array.IndexOf(header, "XROriginRot")].Replace("(","").Replace(")", "").Split(",");
        //         var headPos = values[Array.IndexOf(header, "XRHeadPos")].Replace("(","").Replace(")", "").Split(",");
        //         var headRot = values[Array.IndexOf(header, "XRHeadRot")].Replace("(","").Replace(")", "").Split(",");
        //         var lHandPos = values[Array.IndexOf(header, "HandLPos")].Replace("(","").Replace(")", "").Split(",");
        //         var lHandRot = values[Array.IndexOf(header, "HandLRot")].Replace("(","").Replace(")", "").Split(",");
        //         var rHandPos = values[Array.IndexOf(header, "HandRPos")].Replace("(","").Replace(")", "").Split(",");
        //         var rHandRot = values[Array.IndexOf(header, "HandRRot")].Replace("(","").Replace(")", "").Split(",");
        //         var eyePoint = values[Array.IndexOf(header, "PointLocationEye")].Replace("(","").Replace(")", "").Split(",");
        //         var handPoint = values[Array.IndexOf(header, "PointLocationHand")].Replace("(","").Replace(")", "").Split(",");
        //         var headPoint = values[Array.IndexOf(header, "PointLocationHead")].Replace("(","").Replace(")", "").Split(",");

                
   
                
        //         // Add the record to the sequence
        //         outputSequence.Add(
        //             new EngineDataRecord()
        //             {
        //                 TimeStamp = long.Parse(values[Array.IndexOf(header, "TimeStamp")]),
        //                 ActiveTarget = values[Array.IndexOf(header,"ActiveTarget")],
        //                 XROriginPos = new Vector3(float.Parse(orgPos[0]), float.Parse(orgPos[1]), float.Parse(orgPos[2])),
        //                 XROriginRot =  new Quaternion(float.Parse(orgRot[0]), float.Parse(orgRot[1]), float.Parse(orgRot[2]), float.Parse(orgRot[3])),
        //                 XRHeadPos = new Vector3(float.Parse(headPos[0]), float.Parse(headPos[1]), float.Parse(headPos[2])),
        //                 XRHeadRot = new Quaternion(float.Parse(headRot[0]), float.Parse(headRot[1]), float.Parse(headRot[2]), float.Parse(headRot[3])),
        //                 HandLPos = new Vector3(float.Parse(lHandPos[0]), float.Parse(lHandPos[1]), float.Parse(lHandPos[2])),
        //                 HandLRot = new Quaternion(float.Parse(lHandRot[0]), float.Parse(lHandRot[1]), float.Parse(lHandRot[2]), float.Parse(lHandRot[3])),
        //                 HandRPos = new Vector3(float.Parse(rHandPos[0]), float.Parse(rHandPos[1]), float.Parse(rHandPos[2])),
        //                 HandRRot = new Quaternion(float.Parse(rHandRot[0]), float.Parse(rHandRot[1]), float.Parse(rHandRot[2]), float.Parse(rHandRot[3])),
        //                 PointLocationEye = new Vector3(float.Parse(eyePoint[0]), float.Parse(eyePoint[1]), float.Parse(eyePoint[2])),
        //                 PointLocationHand = new Vector3(float.Parse(handPoint[0]), float.Parse(handPoint[1]), float.Parse(handPoint[2])),
        //                 PointLocationHead = new Vector3(float.Parse(headPoint[0]), float.Parse(headPoint[1]), float.Parse(headPoint[2])),
        //                 ReportedEventsCount = int.Parse(values[Array.IndexOf(header, "ReportedEventsCount")])
        //             }
        //         );
                
        //     }
        //     return outputSequence;
        // }
        
        // public static List<SingleEyeDataRecord> ReadEyeDataRecordsFromCsv(string path)
        // {
        //     // Output sequence
        //     var outputSequence = new List<SingleEyeDataRecord>();
            
        //     // Read the CSV
        //     string content = File.ReadAllText(path);
        //     string[] rows = content.Split('\n');
        //     string[] header = rows[0].Split('\t');
          
        //     // Iterate over the rows 
        //     for (int rowIdx = 1; rowIdx < rows.Length -1; rowIdx++)
        //     {
        //         // Search in the header file where to find the value, and store the elements (x,y,z,w) as a string-array
        //         string[] values = rows[rowIdx].Split("\t");
        //         var eyeIndex = values[Array.IndexOf(header, "EyeIndex")];
        //         var gazeOrg = values[Array.IndexOf(header, "GazeOriginInEye")].Replace("(","").Replace(")", "").Split(",");
        //         var gazeDir = values[Array.IndexOf(header, "GazeDirectionNormInEye")].Replace("(","").Replace(")", "").Split(",");


        //         // Add the record to the sequence
        //         outputSequence.Add(
        //             new SingleEyeDataRecord()
        //             {
        //                 TimeStamp = long.Parse(values[Array.IndexOf(header, "TimeStamp")]),
        //                 EyeIndex =  eyeIndex switch {"LEFT" => GazeIndex.LEFT, "RIGHT" => GazeIndex.RIGHT, "COMBINE" => GazeIndex.COMBINE,
        //                     _ => throw new ArgumentOutOfRangeException(eyeIndex)}, 
        //                 GazeOriginInEye = new Vector3(float.Parse(gazeOrg[0]), float.Parse(gazeOrg[1]), float.Parse(gazeOrg[2])),
        //                 GazeDirectionNormInEye =  new Vector3(float.Parse(gazeDir[0]), float.Parse(gazeDir[1]), float.Parse(gazeDir[2])),
        //             }
        //         );
                
        //     }
        //     return outputSequence;
        // }

        // private IEnumerator ReplayMultiple(List<string> subjects, List<string>  trials)
        // {
        //     foreach (var subj in subjects)
        //     {
        //         currSubject = subj;
        //         foreach (var trial in trials)
        //         {
        //             currTrial = trial;
        //             _replayFinished = false;
        //             ReplayTrial(subj, trial);
        //             yield return new WaitUntil(() => _replayFinished);
        //         }
        //     }
        // }


        // private void Awake()
        // {
        //     Debug.Log("RUNNING DATA REPLAY");

        //     var subjects = new List<string>() { "S45", };// "S41", "S42",  "S40" , "S39", "S38" }; //{"S56", "S35", "S37", "S55","S54","S53","S52", "S51", "S50"}}; // done: "S57"
        //     var trials = new List<string>() {"07_00", "07_01", "07_02", "08_00", "08_01", "08_02", "09_00", "09_01", "09_02"};
            
        //     SwitchSimMode(SimulationMode.Edges, EyeTracking.EyeTrackingConditions.GazeIgnored);
        //     StartCoroutine(ReplayMultiple(subjects, trials));

        //     // ReplayEngineDataRecord("C:\\Users\\Jaap de Ruyter\\AppData\\LocalLow\\DefaultCompany\\ExperimentPuzzle\\Jaap_\\07_00EngineDataRecord.tsv", 
        //     //     "C:\\Users\\Jaap de Ruyter\\AppData\\LocalLow\\DefaultCompany\\ExperimentPuzzle\\Jaap_\\07_00SingleEyeDataRecordC.tsv");
        // }
    }
}
