using ExperimentControl;
using Simulation;
using UnityEngine;
using ViveSR.anipal.Eye;

namespace DataHandling.Separated
{
    public interface IDataStructure{}
    
    public enum Glasses { None, Glasses, Contacts }
    
    public struct TrialConfigRecord : IDataStructure
    {
        public EyeTracking.EyeTrackingConditions GazeCondition { get; set; }
        public HallwayCreator.Hallways Hallway { get; set; }
        public Glasses Glasses { get; set; }
        public double GazeRaySensitivity { get; set; }
        public string DataDelimiter { get; set; }
    }

    public struct EngineDataRecord : IDataStructure
    {
        public long TimeStamp { get; set; }
        public Vector3 XROriginPos { get; set; }
        public Quaternion XROriginRot { get; set; }
        public bool XROriginInBox { get; set; }
        public bool XROriginInCheckpoint { get; set; }
        public Vector3 XRHeadPos { get; set; }
        public Quaternion XRHeadRot { get; set; }
        public Vector3 HandLPos { get; set; }
        public Quaternion HandLRot { get; set; }
        public bool HandLInBox { get; set; }
        public bool HandLInWall { get; set; }
        public Vector3 HandRPos { get; set; }
        public Quaternion HandRRot { get; set; }
        public bool HandRInBox { get; set; }
        public bool HandRInWall { get; set; }
        public int CollisionCount { get; set; }
        public int CheckpointCount { get; set; }
        public int FrameCount { get; set; }
    }

    public struct EyeTrackerDataRecord : IDataStructure
    {
        public long TimeStamp { get; set; }
        public int TrackerTimeStamp { get; set; }
        public TrackingImprovements TrackerTrackingImprovementCount { get; set; }
        public int TrackerFrameCount { get; set; }
        public float ConvergenceDistance { get; set; }
        public bool ConvergenceDistanceValidity { get; set; }
        public int ErrorsSinceLastUpdate { get; set; }
    }

    public struct SingleEyeDataRecord : IDataStructure
    {
        public long TimeStamp { get; set; }
        public int TrackerTimeStamp { get; set; }
        public GazeIndex EyeIndex { get; set; }
        public ulong Validity { get; set; }
        public float Openness { get; set; }
        public float PupilDiameter { get; set; }
        public UnityEngine.Vector2 PosInSensor { get; set; }
        public UnityEngine.Vector3 GazeOriginInEye { get; set; }
        public UnityEngine.Vector3 GazeDirectionNormInEye { get; set; }
    }
}