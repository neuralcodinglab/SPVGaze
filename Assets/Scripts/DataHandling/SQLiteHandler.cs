using System.Data;
using Microsoft.Data.Sqlite;
using UnityEngine;
using ViveSR.anipal.Eye;
using GazeConditions = Simulation.EyeTracking.EyeTrackingConditions;
using Hallways = ExperimentControl.HallwayCreator.Hallways;

namespace DataHandling
{
    public class SQLiteHandler
    {
        const string DBUri = @"Data Source=file:C:/Projects/Mo/ExperimentData/Experiment.sqlite";

        private static SQLiteHandler _instance = null;
        private static readonly object Padlock = new();

        private bool acceptNewRecords;
        private IDbConnection dbConn;
        private int currentTrialConfigIdx;

        private SQLiteHandler() { }
        public static SQLiteHandler Instance
        {
            get
            {
                lock (Padlock)
                    return _instance ??= new SQLiteHandler();
            }
        }

        public void AllowNewRecords()
        {
            acceptNewRecords = true;
        }

        public void DisallowNewRecords()
        {
            acceptNewRecords = false;
        }

        private void ExecuteSQLInsert(string command)
        {
            OpenDatabase();
            var cmd = dbConn.CreateCommand();
            cmd.CommandText = command;
            cmd.ExecuteNonQuery();
        }

        public void AddTrialConfig(
            string subjID, int blockId, int trialId,
            GazeConditions gazeCondition, Hallways hallways,
            Glasses glasses, double gazeRaySensitivity
        )
        {
            if (!acceptNewRecords) return;
            
            // insert config into database
            ExecuteSQLInsert(
                "insert into TrialConfigurations (SubjectId, BlockId, TrialId" +
                ", GazeCondition, Hallway, Glasses, GazeRaySensitivity)" +
                $"values ('{subjID}', {blockId}, {trialId}," +
                $"'{gazeCondition.ToString()}', '{hallways.ToString()}', " +
                $"'{glasses.ToString()}', {gazeRaySensitivity});"
            );

            // retrieve generated ID of config to reference in new entries
            OpenDatabase();
            var cmd = dbConn.CreateCommand();
            cmd.CommandText = "SELECT Id FROM TrialConfigurations WHERE" +
                              $" SubjectId = '{subjID}' AND BlockId = {blockId} AND TrialId = {trialId}";
            currentTrialConfigIdx = (int)cmd.ExecuteScalar();
        }

        public void AddEyeTrackerRecord(
            string subjID, int blockId, int trialID,
            int trackerTimestamp, long engineTimestamp, int trackingImprovementCount,
            int frameCount, float convergenceDistance, bool convergenceValidity
        )
        {
            if (!acceptNewRecords) return;
            
            ExecuteSQLInsert(
                "insert into EyeTrackerData (" +
                "TrialConfig, SubjectId, BlockId, TrialId, " +
                "TrackerTimestamp, EngineTimestamp, TrackingImprovementCount, " +
                "FrameCount, ConvergenceDistance, ConvergenceDistanceValidity)" +
                $"values ({currentTrialConfigIdx}, '{subjID}', {blockId}, {trialID}, " +
                $"{trackerTimestamp}, {engineTimestamp}, {trackingImprovementCount}, " +
                $"{frameCount}, {convergenceDistance}, {convergenceValidity} );"
            );
        }

        public void AddEngineRecord(
            string subjID, int blockId, int trialID, long timestamp,
            Transform xrOriginT, bool originInBox, bool originInCheckpoint, 
            Transform xrHeadT,
            Transform handL, bool handLInBox, bool handLInWall,
            Transform handR, bool handRInBox, bool handRInWall,
            int collisionCount, int checkPointCount, int frameCount
        )
        {
            if (!acceptNewRecords) return;
            
            var originPos = xrOriginT.position;
            var originRot = xrOriginT.rotation;
            var headPos = xrHeadT.position;
            var headRot = xrHeadT.rotation;
            var handLPos = handL.position;
            var handLRot = handL.rotation;
            var handRPos = handR.position;
            var handRRot = handR.rotation;
            ExecuteSQLInsert(
        "insert into EngineData (" +
                "SubjectId, BlockId, TrialId, EngineTimestamp, " +
                "XROriginPosX, XROriginPosY, XROriginPosZ," +
                "XROriginRotW, XROriginRotX, XROriginRotY, XROriginRotZ, " +
                "XROriginInBox, XROriginInCheckpoint, " +
                "XRHeadPosX, XRHeadPosY, XRHeadPosZ, " +
                "XRHeadRotW, XRHeadRotX, XRHeadRotY, XRHeadRotZ, " +
                "HandLPosX, HandLPosY, HandLPosZ, " +
                "HandLRotW, HandLRotX, HandLRotY, HandLRotZ, " +
                "HandLInBox, HandLInWall, " +
                "HandRPosX, HandRPosY, HandRPosZ, " +
                "HandRRotW, HandRRotX, HandRRotY, HandRRotZ, " +
                "HandRInBox, HandRInWall, " +
                "CollisionCount, CheckpointCount, FrameCount, TrialConfig)" +
                "values (" +
                $"'{subjID}', {blockId}, {trialID}, {timestamp}, " +
                $"{originPos.x}, {originPos.y}, {originPos.z}, " +
                $"{originRot.w}, {originRot.x}, {originRot.y}, {originRot.z}, " +
                $"{originInBox}, {originInCheckpoint}, " +
                $"{headPos.x}, {headPos.y}, {headPos.z}, " +
                $"{headRot.w}, {headRot.x}, {headRot.y}, {headRot.z}, " +
                $"{handLPos.x}, {handLPos.y}, {handLPos.z}, " +
                $"{handLRot.w}, {handLRot.x}, {handLRot.y}, {handLRot.z}, " +
                $"{handLInBox}, {handLInWall}, " +
                $"{handRPos.x}, {handRPos.y}, {handRPos.z}, " +
                $"{handRRot.w}, {handRRot.x}, {handRRot.y}, {handRRot.z}, " +
                $"{handRInBox}, {handRInWall}, " +
                $"{collisionCount}, {checkPointCount}, {frameCount}, " +
                $"{currentTrialConfigIdx});"
            );
        }

        public void AddSingleEyeRecord(
            string subjID, int blockId, int trialID,
            GazeIndex eye, int trackerTimestamp, long timestamp,
            ulong validity, float openness, float pupilDiameter,
            Vector2 eyePosInSensor, Vector3 gazeOriginInEye, Vector3 gazeDirNorm
        )
        {
            if (!acceptNewRecords) return;
            
            ExecuteSQLInsert(
        "insert into SingleEyeData (" +
                "TrialConfig, SubjectId, BlockId, TrialId, " +
                "WhichEye, TrackerTimestamp, EngineTimestamp, " +
                "Validity, Openness, PupilDiameter, " +
                "PosInSensorX, PosInSensorY, " +
                "GazeOriginInEyeX, GazeOriginInEyeY, GazeOriginInEyeZ, " +
                "GazeDirectionNormedX, GazeDirectionNormedY, GazeDirectionNormedZ " +
                ") values (" +
                $"{currentTrialConfigIdx}, '{subjID}', {blockId}, {trialID}, " +
                $"'{eye.ToString()}', {trackerTimestamp}, {timestamp}," +
                $"{validity}, {openness}, {pupilDiameter}," +
                $"{eyePosInSensor.x}, {eyePosInSensor.y}, " +
                $"{gazeOriginInEye.x}, {gazeOriginInEye.y}, {gazeOriginInEye.z}, " +
                $"{gazeDirNorm.x}, {gazeDirNorm.y}, {gazeDirNorm.z} );"    
            );
        }


        ~SQLiteHandler()
        {
            dbConn?.Dispose();
        }
        
        private void OpenDatabase()
        {
            dbConn ??= new SqliteConnection(DBUri);
            dbConn.Open();
        }
    }
}