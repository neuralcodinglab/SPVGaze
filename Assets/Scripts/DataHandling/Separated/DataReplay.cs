using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExperimentControl;
using Simulation;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using ViveSR.anipal.Eye;
using Environment = ExperimentControl.Environment;
using XRController = UnityEngine.InputSystem.XR.XRController;

namespace DataHandling.Separated
{ 
    public class DataReplay : MonoBehaviour
    {
        [SerializeField] private GameObject XR_Origin;
        [SerializeField] private GameObject playerCamera;
        [SerializeField] private GameObject leftHandController;
        [SerializeField] private GameObject rightHandController;
        [SerializeField] private LineRenderer lineRenderer;

        private double _frameMismatchTolerance = 1E7; //The tolerance value for mismatching timestamps between eye and engine data

        private bool _replayActive;

        private void DeactivateInputControl()
        {
            playerCamera.GetComponent<TrackedPoseDriver>().enabled=false;
            leftHandController.GetComponent<ActionBasedController>().enabled=false;
            rightHandController.GetComponent<ActionBasedController>().enabled = false;
        }

        public void ReplayTrial(string subjId, string trialId)
        {
            var pathBase = Path.Join(Application.persistentDataPath, subjId, trialId);
            var trialConfig = ReadTrialConfigRecordFromCsv($"{pathBase}TrialConfigRecord.tsv");
            var engineDataSequence = ReadEngineRecordsFromCsv($"{pathBase}EngineDataRecord.tsv");
            var eyeDataSequence = ReadEyeDataRecordsFromCsv($"{pathBase}SingleEyeDataRecordC.tsv");
            
            
            StartCoroutine(ReplayData(trialConfig, engineDataSequence, eyeDataSequence));

        }

        private IEnumerator ReplayData(TrialConfigRecord trialConfig ,List<EngineDataRecord> engineDataSequence, List<SingleEyeDataRecord> eyeDataSequence)
        {
            yield return new WaitForSeconds(5f);
            
            // Deactivate Normal input from the VR system
            DeactivateInputControl();
            lineRenderer.enabled = true;
            
            // Jump to the appropriate environment
            SenorSummarySingletons.GetInstance<SceneHandler>().JumpToEnvironment(trialConfig.EnvironmentName);
            var currentTarget = "None";
            
            foreach (var engineRecord in engineDataSequence)
            {
                // Activate target object
                if (engineRecord.ActiveTarget != currentTarget)
                {
                    currentTarget = engineRecord.ActiveTarget;
                    Debug.Log($"Current target switched to {currentTarget}");
                    if (currentTarget != "None")
                        SenorSummarySingletons.GetInstance<SceneHandler>().ActivateTargetObject(currentTarget);
                }
                    
                    
                // Find eyeRecord with the closest timestamp
                var eyeRecord = eyeDataSequence.OrderBy(x => Math.Abs(x.TimeStamp - engineRecord.TimeStamp)).First();
                if (Math.Abs(eyeRecord.TimeStamp - engineRecord.TimeStamp) > _frameMismatchTolerance)
                    throw new InvalidDataException($"No matching eye data for engine data with TimeStamp" +
                        $" {engineRecord.TimeStamp}. Closest eye data TimeStamp was {eyeRecord.TimeStamp}" +
                        $"(mismatch: {Math.Abs(eyeRecord.TimeStamp-engineRecord.TimeStamp)}");
                Step(engineRecord, eyeRecord); 
                yield return new WaitForSeconds(0.00002f);  //TODO: incorporate actual framerate
            }
        }

        private void Step(EngineDataRecord engineRecord, SingleEyeDataRecord eyeDataRecord)
        {
            XR_Origin.transform.position = engineRecord.XROriginPos;
            XR_Origin.transform.rotation = engineRecord.XROriginRot;
            
            playerCamera.transform.position = engineRecord.XRHeadPos;
            playerCamera.transform.rotation = engineRecord.XRHeadRot;
            
            leftHandController.transform.position = engineRecord.HandLPos;
            leftHandController.transform.rotation = engineRecord.HandLRot;
            
            rightHandController.transform.position = engineRecord.HandRPos;
            rightHandController.transform.rotation = engineRecord.HandRRot;

            var focusInfo = new FocusInfo();
            var eyeTracking = SenorSummarySingletons.GetInstance<EyeTracking>();
            eyeTracking.GetFocusInfoFromRayCast(-eyeDataRecord.GazeDirectionNormInEye, out focusInfo);
            eyeTracking.SetCustomFixationPoint(focusInfo.point, (int)eyeDataRecord.TimeStamp);
            lineRenderer.SetPosition(1,100*eyeDataRecord.GazeDirectionNormInEye);
        }

        public static TrialConfigRecord ReadTrialConfigRecordFromCsv(string path)
        {
            var record = new TrialConfigRecord();
            
            string content = File.ReadAllText(path);
            string[] rows = content.Split('\n');
            string[] header = rows[0].Split('\t');
            string[] values = rows[1].Split("\t");

            // Read string values
            var task = values[Array.IndexOf(header, "ExperimentalTask")];
            var gazeCondition= values[Array.IndexOf(header, "GazeCondition")];
            var envName = values[Array.IndexOf(header, "EnvironmentName")];
            var envClass = values[Array.IndexOf(header, "EnvironmentClass")];
            var glasses = values[Array.IndexOf(header, "Glasses")];
            var gazeRaySensitivity = values[Array.IndexOf(header, "GazeRaySensitivity")];
            var delimiter = values[Array.IndexOf(header, "DataDelimiter")];
            var reportedRoomCat = values[Array.IndexOf(header, "ReportedRoomCategory")];
            var subjRating = values[Array.IndexOf(header, "ReportedSubjectiveRating")];
            var nEvents = values[Array.IndexOf(header, "ReportedEventsCount")];
            var trialDuration = values[Array.IndexOf(header, "TrialDuration")];
            
            // Cast string values to TrialConfigRecord
            record.ExperimentalTask = Enum.Parse<RunExperiment.Task>(task);
            record.GazeCondition = Enum.Parse<EyeTracking.EyeTrackingConditions>(gazeCondition);
            record.EnvironmentName = envName;
            record.EnvironmentClass = Enum.Parse<ExperimentControl.Environment.RoomCategory>(envClass);
            record.Glasses = Enum.Parse<Glasses>(glasses);
            record.GazeRaySensitivity = double.Parse(gazeRaySensitivity);
            record.DataDelimiter = delimiter;
            record.ReportedRoomCategory = Enum.Parse<Environment.RoomCategory>(reportedRoomCat);
            record.ReportedSubjectiveRating = int.Parse(subjRating);
            record.ReportedEventsCount = int.Parse(nEvents);
            record.TrialDuration = float.Parse(trialDuration);
            return record;
        }
        public static List<EngineDataRecord> ReadEngineRecordsFromCsv(string path)
        {
            // Output sequence
            var outputSequence = new List<EngineDataRecord>();
            
            // Read the CSV
            string content = File.ReadAllText(path);
            string[] rows = content.Split('\n');
            string[] header = rows[0].Split('\t');
          
            // Iterate over the rows 
            for (int rowIdx = 1; rowIdx < rows.Length -1; rowIdx++)
            {
                // Search in the header file where to find the value, and store the elements (x,y,z,w) as a string-array
                string[] values = rows[rowIdx].Split("\t");
                var orgPos = values[Array.IndexOf(header, "XROriginPos")].Replace("(","").Replace(")", "").Split(",");
                var orgRot = values[Array.IndexOf(header, "XROriginRot")].Replace("(","").Replace(")", "").Split(",");
                var headPos = values[Array.IndexOf(header, "XRHeadPos")].Replace("(","").Replace(")", "").Split(",");
                var headRot = values[Array.IndexOf(header, "XRHeadRot")].Replace("(","").Replace(")", "").Split(",");
                var lHandPos = values[Array.IndexOf(header, "HandLPos")].Replace("(","").Replace(")", "").Split(",");
                var lHandRot = values[Array.IndexOf(header, "HandLRot")].Replace("(","").Replace(")", "").Split(",");
                var rHandPos = values[Array.IndexOf(header, "HandRPos")].Replace("(","").Replace(")", "").Split(",");
                var rHandRot = values[Array.IndexOf(header, "HandRRot")].Replace("(","").Replace(")", "").Split(",");

                
   
                
                // Add the record to the sequence
                outputSequence.Add(
                    new EngineDataRecord()
                    {
                        TimeStamp = long.Parse(values[Array.IndexOf(header, "TimeStamp")]),
                        ActiveTarget = values[Array.IndexOf(header,"ActiveTarget")],
                        XROriginPos = new Vector3(float.Parse(orgPos[0]), float.Parse(orgPos[1]), float.Parse(orgPos[2])),
                        XROriginRot =  new Quaternion(float.Parse(orgRot[0]), float.Parse(orgRot[1]), float.Parse(orgRot[2]), float.Parse(orgRot[3])),
                        XRHeadPos = new Vector3(float.Parse(headPos[0]), float.Parse(headPos[1]), float.Parse(headPos[2])),
                        XRHeadRot = new Quaternion(float.Parse(headRot[0]), float.Parse(headRot[1]), float.Parse(headRot[2]), float.Parse(headRot[3])),
                        HandLPos = new Vector3(float.Parse(lHandPos[0]), float.Parse(lHandPos[1]), float.Parse(lHandPos[2])),
                        HandLRot = new Quaternion(float.Parse(lHandRot[0]), float.Parse(lHandRot[1]), float.Parse(lHandRot[2]), float.Parse(lHandRot[3])),
                        HandRPos = new Vector3(float.Parse(rHandPos[0]), float.Parse(rHandPos[1]), float.Parse(rHandPos[2])),
                        HandRRot = new Quaternion(float.Parse(rHandRot[0]), float.Parse(rHandRot[1]), float.Parse(rHandRot[2]), float.Parse(rHandRot[3])),
                    }
                );
                
            }
            return outputSequence;
        }
        
        public static List<SingleEyeDataRecord> ReadEyeDataRecordsFromCsv(string path)
        {
            // Output sequence
            var outputSequence = new List<SingleEyeDataRecord>();
            
            // Read the CSV
            string content = File.ReadAllText(path);
            string[] rows = content.Split('\n');
            string[] header = rows[0].Split('\t');
          
            // Iterate over the rows 
            for (int rowIdx = 1; rowIdx < rows.Length -1; rowIdx++)
            {
                // Search in the header file where to find the value, and store the elements (x,y,z,w) as a string-array
                string[] values = rows[rowIdx].Split("\t");
                var eyeIndex = values[Array.IndexOf(header, "EyeIndex")];
                var gazeOrg = values[Array.IndexOf(header, "GazeOriginInEye")].Replace("(","").Replace(")", "").Split(",");
                var gazeDir = values[Array.IndexOf(header, "GazeDirectionNormInEye")].Replace("(","").Replace(")", "").Split(",");


                // Add the record to the sequence
                outputSequence.Add(
                    new SingleEyeDataRecord()
                    {
                        TimeStamp = long.Parse(values[Array.IndexOf(header, "TimeStamp")]),
                        EyeIndex =  eyeIndex switch {"LEFT" => GazeIndex.LEFT, "RIGHT" => GazeIndex.RIGHT, "COMBINE" => GazeIndex.COMBINE,
                            _ => throw new ArgumentOutOfRangeException(eyeIndex)}, 
                        GazeOriginInEye = new Vector3(float.Parse(gazeOrg[0]), float.Parse(gazeOrg[1]), float.Parse(gazeOrg[2])),
                        GazeDirectionNormInEye =  new Vector3(float.Parse(gazeDir[0]), float.Parse(gazeDir[1]), float.Parse(gazeDir[2])),
                    }
                );
                
            }
            return outputSequence;
        }
        
        

        private void Awake()
        {
           // ReplayTrial("jt", "00_01");
           // ReplayEngineDataRecord("C:\\Users\\Jaap de Ruyter\\AppData\\LocalLow\\DefaultCompany\\ExperimentPuzzle\\Jaap_\\07_00EngineDataRecord.tsv", 
           //     "C:\\Users\\Jaap de Ruyter\\AppData\\LocalLow\\DefaultCompany\\ExperimentPuzzle\\Jaap_\\07_00SingleEyeDataRecordC.tsv");
           //  s
        }
    }
}
