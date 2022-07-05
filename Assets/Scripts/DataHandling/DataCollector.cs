using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExperimentControl;
using mattmc3.dotmore.Collections.Generic;
using Simulation;
using Unity.XR.CoreUtils;
using UnityEngine;
using ViveSR.anipal.Eye;

// using Sirenix.Utilities;

namespace DataHandling
{
    public class DataCollector
    {
        public readonly string SubjID;
        public readonly string TrialID;

        public string Delimiter { get; }
        private string subjDir;
        private string trialDir;

        private int currBlock, currTrial;

        private BlockingCollection<DataRecord> writeQueue;
        private CancellationTokenSource asyncCancellationTokenSource;
        private ParallelLoopResult parallelLoopResult;
        private Dictionary<Schema, FileStream> schema2Stream;
        private Task currentWritingOp;

        private string fileHeader;
        private Schema schema;

        private static Schema trialConfigSchema = new()
        {
           Mapping = 
           {
            { "SubjectID", new DataDescription { DType = typeof(string) } },
            { "TrialID", new DataDescription { DType = typeof(string) } },
            { "GazeCondition", new DataDescription { DType = typeof(string) } }, // enum2string
            { "Hallway", new DataDescription { DType = typeof(string) } }, // enum2string
            { "Glasses", new DataDescription { DType = typeof(string) } }, // enum2string
            { "GazeRaySensitivity", new DataDescription { DType = typeof(double), Min = 0, Max = 1 } },
            { "DataDelimiter", new DataDescription { DType = typeof(string) } } 
           }
        };

        private static Schema engineDataSchema = new()
        {
            Mapping =
            {
                { "SubjectID", new DataDescription{DType = typeof(string)}},
                { "TrialID", new DataDescription{DType = typeof(string)}},
                { "TimeStamp", new DataDescription { DType = typeof(long) } }, // DateTime.Now.Ticks
                { "XROrigin_Pos", new DataDescription { DType = typeof(Vector3) } },
                { "XROrigin_Rot", new DataDescription { DType = typeof(Vector3) } },
                { "XROrigin_InBox", new DataDescription { DType = typeof(bool) } },
                { "XROrigin_InCheckpoint", new DataDescription { DType = typeof(bool) } },
                { "XRHead_Pos", new DataDescription { DType = typeof(Vector3) } },
                { "XRHead_Rot", new DataDescription { DType = typeof(Vector3) } },
                { "Hand_L_Pos", new DataDescription { DType = typeof(Vector3) } },
                { "Hand_L_Rot", new DataDescription { DType = typeof(Vector3) } },
                { "Hand_L_InBox", new DataDescription { DType = typeof(bool) } },
                { "Hand_L_InWall", new DataDescription { DType = typeof(bool) } },
                { "Hand_R_Pos", new DataDescription { DType = typeof(Vector3) } },
                { "Hand_R_Rot", new DataDescription { DType = typeof(Vector3) } },
                { "Hand_R_InBox", new DataDescription { DType = typeof(bool) } },
                { "Hand_R_InWall", new DataDescription { DType = typeof(bool) } },
                { "CollisionCount", new DataDescription { DType = typeof(int), Min = 0 } },
                { "CheckpointCount", new DataDescription { DType = typeof(int), Min = 0 } },
                { "FrameCount", new DataDescription { DType = typeof(int), Min = 0 } }
            }
        };

        private static Schema eyeTrackerDataSchema = new()
        {
            Mapping = 
            {
                { "SubjectID", new DataDescription{DType = typeof(string)}},
                { "TrialID", new DataDescription{DType = typeof(string)}},
                { "TimeStamp", new DataDescription { DType = typeof(long) } }, // DateTime.Now.Ticks
                { "Tracker_TimeStamp", new DataDescription { DType = typeof(int) } },
                { "Tracker_TrackingImprovementCount", new DataDescription { DType = typeof(int) } },
                { "Tracker_FrameCount", new DataDescription { DType = typeof(int), Min = 0 } },
                { "ConvergenceDistance", new DataDescription { DType = typeof(float) } },
                { "ConvergenceDistanceValidity", new DataDescription { DType = typeof(bool) } }
            }
        };

        private static Schema singleEyeDataSchema = new()
        {
            Mapping = {
                { "SubjectID", new DataDescription{DType = typeof(string)}},
                { "TrialID", new DataDescription{DType = typeof(string)}},
                { "TimeStamp", new DataDescription { DType = typeof(long) } }, // DateTime.Now.Ticks
                { "Tracker_TimeStamp", new DataDescription { DType = typeof(int) } },
                { "EyeIndex", new DataDescription{DType = typeof(string)} }, // enum to string
                { "Validity", new DataDescription { DType = typeof(ulong) } },
                { "Openness", new DataDescription { DType = typeof(float) } },
                { "PupilDiameter", new DataDescription { DType = typeof(float) } },
                { "PosInSensor", new DataDescription { DType = typeof(Vector2), Min = 0, Max = 1 } },
                { "GazeOriginInEye", new DataDescription { DType = typeof(Vector3) } },
                { "GazeDirectionNormInEye", new DataDescription { DType = typeof(Vector3), Min = 0, Max = 1 } },
                { "Frown", new DataDescription { DType = typeof(float) } }, // will be null(?) for combined
                { "Squeeze", new DataDescription { DType = typeof(float) } }, // will be null(?) for combined
                { "Wide", new DataDescription { DType = typeof(float) } } // will be null(?) for combined
            }
        };

        private static Schema singleEyeDataSchemaC = singleEyeDataSchema;
        private static Schema singleEyeDataSchemaL = singleEyeDataSchema;
        private static Schema singleEyeDataSchemaR = singleEyeDataSchema;

        private static Schema defaultSchema = new()
        {
            Mapping = 
            {
                { "TimeStamp", new DataDescription{ DType=typeof(long) } }, // DateTime.Now.Ticks 
                { "XROrigin_Pos", new DataDescription{ DType=typeof(Vector3)} },
                { "XROrigin_Rot", new DataDescription{ DType=typeof(Vector3)} },
                { "XRHead_Pos", new DataDescription{ DType=typeof(Vector3)} },
                { "XRHead_Rot", new DataDescription{ DType=typeof(Vector3)} },
                { "HandL_Pos", new DataDescription{ DType=typeof(Vector3)} },
                { "HandL_Rot", new DataDescription{ DType=typeof(Vector3)} },
                { "HandR_Pos", new DataDescription{ DType=typeof(Vector3)} },
                { "HandR_Rot", new DataDescription{ DType=typeof(Vector3)} },
                { "CollisionCount", new DataDescription{ DType=typeof(int), Min=0} },
                // Eye Data
                { "Eye_TimeStamp", new DataDescription{DType=typeof(int)} },
                { "Eye_TrackingImprovementCount", new DataDescription{DType=typeof(int)} },
                { "Eye_GazeRaySensitivity", new DataDescription{DType=typeof(double), Min=0, Max=1} },
                // Combined Data
                { "Eye_ConvergenceDistance", new DataDescription{DType=typeof(float)} },
                { "Eye_ConvergenceDistanceValidity", new DataDescription{DType = typeof(bool)} },
                { "Eye_FrameCount", new DataDescription{DType=typeof(int), Min=0} },
                { "Eye_C_Validity", new DataDescription{DType=typeof(ulong)} },
                { "Eye_C_Openness", new DataDescription{DType=typeof(float)} },
                { "Eye_C_PupilDiameter", new DataDescription{DType=typeof(float)} },
                { "Eye_C_PosInSensor", new DataDescription{DType=typeof(Vector2), Min=0, Max=1} },
                { "Eye_C_GazeOriginInEye", new DataDescription{DType=typeof(Vector3)} },
                { "Eye_C_GazeDirectionNormInEye", new DataDescription{DType=typeof(Vector3), Min=0, Max=1} },
                // Left Eye
                { "Eye_L_Validity", new DataDescription{DType=typeof(ulong)} },
                { "Eye_L_Openness", new DataDescription{DType=typeof(float)} },
                { "Eye_L_PupilDiameter", new DataDescription{DType=typeof(float)} },
                { "Eye_L_PosInSensor", new DataDescription{DType=typeof(Vector2), Min=0, Max=1} },
                { "Eye_L_GazeOriginInEye", new DataDescription{DType=typeof(Vector3)} },
                { "Eye_L_GazeDirectionNormInEye", new DataDescription{DType=typeof(Vector3), Min=0, Max=1} },
                { "Eye_L_Frown", new DataDescription{DType=typeof(float)} },
                { "Eye_L_Squeeze", new DataDescription{DType=typeof(float)} },
                { "Eye_L_Wide", new DataDescription{DType=typeof(float)} },
                // Right Eye
                { "Eye_R_Validity", new DataDescription{DType=typeof(ulong)} },
                { "Eye_R_Openness", new DataDescription{DType=typeof(float)} },
                { "Eye_R_PupilDiameter", new DataDescription{DType=typeof(float)} },
                { "Eye_R_PosInSensor", new DataDescription{DType=typeof(Vector2)} },
                { "Eye_R_GazeOriginInEye", new DataDescription{DType=typeof(Vector2)} },
                { "Eye_R_GazeDirectionNormInEye", new DataDescription{DType=typeof(Vector3)} },
                { "Eye_R_Frown", new DataDescription{DType=typeof(float)} },
                { "Eye_R_Squeeze", new DataDescription{DType=typeof(float)} },
                { "Eye_R_Wide", new DataDescription{DType=typeof(float)} },
            }
    };

        private Dictionary<Schema, string> schema2filename = new()
        {
            { trialConfigSchema, "trial_config.csv" },
            { engineDataSchema, "engine_data.csv" },
            { eyeTrackerDataSchema, "eye_tracker_data.csv" },
            { singleEyeDataSchemaC, "eye_data_left.csv" },
            { singleEyeDataSchemaL, "eye_data_right.csv" },
            { singleEyeDataSchemaR, "eye_data_combined.csv" },
        };

        public DataCollector(string subjectID = "_TEST", string delim = "\t")
        {
            SubjID = subjectID;
            Delimiter = delim;

            currBlock = 0;

            subjDir = Path.Join(StaticDataReport.DataDir, SubjID);
            if (Directory.Exists(subjDir))
            {
                Debug.LogWarning($"Subject DIrectory {subjDir} exists. Appending ticks.");
                subjDir = Path.Join(subjDir, DateTime.Now.Ticks.ToString());
            }
            Directory.CreateDirectory(subjDir);

            SenorSummarySingletons.RegisterType(this);
        }

        public async void StartNewTrial(bool isNewBlock=false)
        {
            // clean up last block if this is not the first
            if (currBlock != 0)
            {
                await CleanUp();
            }

            if (isNewBlock)
            {
                currBlock += 1;
                currTrial = 0;
            }
            currTrial += 1;

            trialDir = Path.Join(subjDir, $"{currBlock:D2}_{currTrial:D2}");
            // ToDo: Create files for data description
            
            // Create FileStreams
            schema2Stream = new Dictionary<Schema, FileStream>();
            foreach (var mapping in schema2filename)
            {
                var schema = mapping.Key;
                var filename = mapping.Value;
                var stream = 
                    new FileStream(Path.Join(trialDir, filename), 
                        FileMode.CreateNew, FileAccess.ReadWrite,
                        FileShare.ReadWrite, default, FileOptions.Asynchronous);
                schema2Stream.Add(schema, stream);
            }
            
            // Create New Queue
            asyncCancellationTokenSource = new CancellationTokenSource();
            writeQueue = new BlockingCollection<DataRecord>();
            // start reading from queue. function blocks if empty until cancelled
            parallelLoopResult = Parallel.ForEach(
                writeQueue.GetConsumingEnumerable(asyncCancellationTokenSource.Token), 
                RecordToFile);
        }

        public async void EndBlock()
        {
            await CleanUp();
        }

        private async Task CleanUp()
        {
            writeQueue.CompleteAdding();
            while (!writeQueue.IsCompleted)
            {
                await Task.Yield();
            }
            asyncCancellationTokenSource.Cancel();
            while (!parallelLoopResult.IsCompleted)
            {
                await Task.Yield();
            }

            var tasks = 
                schema2Stream.Values.Select(
                async stream => {
                    await stream.FlushAsync();
                    stream.Close();
                });
            await Task.WhenAll(tasks);
            writeQueue.Dispose();
        }

        private async void RecordToFile(DataRecord record)
        {
            var row = ToCsvFields(record.data.Values);
            var stream = schema2Stream[record.schema];
            await stream.WriteAsync(Encoding.UTF8.GetBytes(row));
        }

        public void AddRecord(DataRecord record)
        {
            // record was invalid
            if (record.data == null) return;
            writeQueue.Add(record);
        }

        public void AddRecords(IEnumerable<DataRecord> records)
        {
            foreach (var record in records)
            {
                AddRecord(record);
            }
        }

        private string ToCsvFields(IEnumerable<object> values, bool addEol = true)
        {
            // ToDo: Why is the null safe ToString commented out?
            var strValues = values.Cast<string>();
                // values.Convert(field => field == null ? "" : field.ToString());

                // ToDo Look up Vector2/3 ToString wrt precision loss
            return ToCsvFields(strValues, addEol);
        }

        private string ToCsvFields(IEnumerable<string> values, bool addEol=true)
        {
            var row = "";
            var delimReplace = Delimiter switch
            {
                "\t" => " ",
                "," => "\t",
                ";" => "|",
                "|" => ";",
                _ => "||"
            };
            foreach(var field in values)
            {
                var toAdd = field;
                if (field.Contains(Delimiter))
                {
                    toAdd = field.Replace(Delimiter, delimReplace);
                }

                toAdd = toAdd.Replace("\n", @"\");
                row += toAdd + Delimiter;
            }

            if (addEol) row += "\n";
            return row;
        }
        
        public class DataRecord
        {
            internal Schema schema;
            internal OrderedDictionary<string, object> data;

            private DataRecord(Schema schema)
            {
                this.schema = schema;
                this.data = null;
            }

            public DataRecord(Schema schema, OrderedDictionary<string, object> data)
            {
                this.schema = schema;
                this.data = data;
                if(!ValidateData())
                {
                    this.data = null;
                }
            }

            private bool ValidateData()
            {
                if (data == null)
                {
                    return false;
                }
                
                if (!schema.Mapping.Keys.Equals(data.Keys))
                {
                    Debug.LogWarning($"Data not fitting to schema. Keycollection differs in content or order.");
                    return false;
                }
                
                foreach (var entry in data)
                {
                    var key = entry.Key;
                    var value = entry.Value;
                    var dataDescription = schema.Mapping[key];
                    if (value.GetType() != dataDescription.DType)
                    {
                        Debug.LogWarning($"Data not fitting to schema. Expected {dataDescription.DType.Name} for {key}, but got {value.GetType().Name}. Nulling data.");
                        return false;
                    }
                }

                return true;
            }
            
            #region Convenience static record functions
            public static DataRecord CreateTrialConfigRecord(
                string subjID, string trialID, 
                EyeTracking.EyeTrackingConditions condition,
                HallwayCreator.Hallways hallway,
                Glasses glasses,
                float gazeRaySensitivity,
                string dataDelimiter
            )
            {
                var data = new OrderedDictionary<string, object>()
                {
                    { "SubjectID", subjID },
                    { "TrialID", trialID },
                    { "GazeCondition", Enum.GetName(typeof(EyeTracking.EyeTrackingConditions), condition) },
                    { "Hallway", Enum.GetName(typeof(HallwayCreator.Hallways), hallway) },
                    { "Glasses", Enum.GetName(typeof(Glasses), glasses) },
                    { "GazeRaySensitivity", gazeRaySensitivity },
                    { "DataDelimiter", dataDelimiter }
                };

                return new DataRecord(trialConfigSchema, data);
            }
        
            public static DataRecord CreateEngineDataRecord(
                string subjID, string trialID, long timestamp,
                Vector3 xrOriginPos, Vector3 xrOriginRot, bool xrOriginInBox, bool xrOriginInCheckpoint,
                Vector3 xrHeadPos, Vector3 xrHeadRot,
                Vector3 handLPos, Vector3 handLRot, bool handLInBox, bool handLInWall,
                Vector3 handRPos, Vector3 handRRot, bool handRInBox, bool handRInWall,
                int collisionCount, int checkpointCount, int frameCount
            )
            {
                var data = new OrderedDictionary<string, object>()
                {
                    { "SubjectID", subjID },
                    { "TrialID", trialID },
                    { "TimeStamp", timestamp},
                    { "XROrigin_Pos",  xrOriginPos},
                    { "XROrigin_Rot",  xrOriginRot},
                    { "XROrigin_InBox",  xrOriginInBox},
                    { "XROrigin_InCheckpoint",  xrOriginInCheckpoint},
                    { "XRHead_Pos",  xrHeadPos},
                    { "XRHead_Rot",  xrHeadRot},
                    { "Hand_L_Pos",  handLPos},
                    { "Hand_L_Rot",  handLRot},
                    { "Hand_L_InBox",  handLInBox},
                    { "Hand_L_InWall",  handLInWall},
                    { "Hand_R_Pos",  handRPos},
                    { "Hand_R_Rot",  handRRot},
                    { "Hand_R_InBox",  handRInBox},
                    { "Hand_R_InWall",  handRInWall},
                    { "CollisionCount",  collisionCount},
                    { "CheckpointCount",  checkpointCount},
                    { "FrameCount", frameCount }
                };

                return new DataRecord(engineDataSchema, data);                
            }
        
            public static DataRecord CreateEyeTrackerRecord(
                string subjID, string trialID, long timestamp,
                int trackerTimestamp, int trackerImprovementCount,
                int trackerFramecount, float convergenceDistance, bool convergenceValidity
            )
            {
                var data = new OrderedDictionary<string, object>()
                {
                    { "SubjectID", subjID },
                    { "TrialID", trialID },
                    { "TimeStamp", timestamp },
                    { "Tracker_TimeStamp", trackerTimestamp },
                    { "Tracker_TrackingImprovementCount", trackerImprovementCount },
                    { "Tracker_FrameCount", trackerFramecount },
                    { "ConvergenceDistance", convergenceDistance },
                    { "ConvergenceDistanceValidity", convergenceValidity }
                };

                return new DataRecord(eyeTrackerDataSchema, data);                
            }
        
            public static DataRecord CreateSingleEyeRecord(
                string subjID, string trialID, long timestamp, int trackerTimestamp,
                GazeIndex which, ulong validity, float openness, float pupilDiameter,
                Vector2 posInSensor, Vector3 gazeOrigin, Vector3 gazeDir,
                float frown, float squeeze, float wide
            )
            {
                var schema = which switch
                {
                    GazeIndex.LEFT => singleEyeDataSchemaL,
                    GazeIndex.RIGHT => singleEyeDataSchemaR,
                    GazeIndex.COMBINE => singleEyeDataSchemaC,
                    _ => throw new ArgumentOutOfRangeException(nameof(which), which, null)
                };
                var data = new OrderedDictionary<string, object>()
                {
                    { "SubjectID", subjID },
                    { "TrialID", trialID },
                    { "TimeStamp", timestamp },
                    { "Tracker_TimeStamp", trackerTimestamp },
                    { "EyeIndex", Enum.GetName(typeof(GazeIndex), which)},
                    { "Validity", validity },
                    { "Openness", openness },
                    { "PupilDiameter", pupilDiameter },
                    { "PosInSensor", posInSensor },
                    { "GazeOriginInEye", gazeOrigin },
                    { "GazeDirectionNormInEye", gazeDir },
                    { "Frown",  frown }, 
                    { "Squeeze", squeeze },
                    { "Wide", wide }
                };

                return new DataRecord(schema, data);                
            }
            #endregion
        }
    }

    public struct DataDescription
    {
        public Type DType;
        public float? Min;
        public float? Max;
    }

    public enum Glasses
    {
        None,
        Glasses,
        Contacts
    }

    public struct Schema
    {
        public OrderedDictionary<string, DataDescription> Mapping;
    }

    
    
}

