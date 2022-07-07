using System.Numerics;
using ExperimentControl;
using Simulation;
using ViveSR.anipal.Eye;

namespace DataHandling.EntityFramework
{
    public class TrialConfig
    {
        public string SubjectId { get; set; }
        public int BlockId { get; set; }
        public int TrialId { get; set; }
        // public EyeTracking.EyeTrackingConditions GazeCondition { get; set; }
        // public HallwayCreator.Hallways Hallway { get; set; }
        // public Glasses Glasses { get; set; }
        public float GazeRaySensitivity { get; set; }
        public string DataDelimiter { get; set; }
        // public virtual EngineData EngineData { get; set; }
        // public virtual EyeTrackerData EyeTrackerData { get; set; }
        // public virtual SingleEyeData LeftEyeData { get; set; }
        // public virtual SingleEyeData RightEyeData { get; set; }
        // public virtual SingleEyeData CombinedEyeData { get; set; }
    }

    public class EngineData
    {
        public string SubjectId { get; set; }
        public int BlockId { get; set; }
        public int TrialId { get; set; }
        public long TimeStamp { get; set; }
        public float XROriginPosX { get; set; }
        public float XROriginPosY { get; set; }
        public float XROriginPosZ { get; set; }
        public float XROriginRotW { get; set; }
        public float XROriginRotX { get; set; }
        public float XROriginRotY { get; set; }
        public float XROriginRotZ { get; set; }
        public bool XROriginInBox { get; set; }
        public bool XROriginInCheckpoint { get; set; }
        public float XRHeadPosX { get; set; }
        public float XRHeadPosY { get; set; }
        public float XRHeadPosZ { get; set; }
        public float XRHeadRotW { get; set; }
        public float XRHeadRotX { get; set; }
        public float XRHeadRotY { get; set; }
        public float XRHeadRotZ { get; set; }
        public float HandLPosX { get; set; }
        public float HandLPosY { get; set; }
        public float HandLPosZ { get; set; }
        public float HandLRotW { get; set; }
        public float HandLRotX { get; set; }
        public float HandLRotY { get; set; }
        public float HandLRotZ { get; set; }
        public bool HandLInBox { get; set; }
        public bool HandLInWall { get; set; }
        public float HandRPosX { get; set; }
        public float HandRPosY { get; set; }
        public float HandRPosZ { get; set; }
        public float HandRRotW { get; set; }
        public float HandRRotX { get; set; }
        public float HandRRotY { get; set; }
        public float HandRRotZ { get; set; }
        public bool HandRInBox { get; set; }
        public bool HandRInWall { get; set; }
        public int CollisionCount { get; set; }
        public int CheckpointCount { get; set; }
        public int FrameCount { get; set; }
    }

    public class EyeTrackerData
    {
        public string SubjectId { get; set; }
        public int BlockId { get; set; }
        public int TrialId { get; set; }
        public long TimeStamp { get; set; }
        public int TrackerTimeStamp { get; set; }
        public int TrackerTrackingImprovementCount { get; set; }
        public int TrackerFrameCount { get; set; }
        public float ConvergenceDistance { get; set; }
        public bool ConvergenceDistanceValidity { get; set; }
    }

    public class SingleEyeData
    {
        public string SubjectId { get; set; }
        public int BlockId { get; set; }
        public int TrialId { get; set; }
        public long TimeStamp { get; set; }
        public int TrackerTimeStamp { get; set; }
        public GazeIndex EyeIndex { get; set; }
        public ulong Validity { get; set; }
        public float Openness { get; set; }
        public float PupilDiameter { get; set; }
        public float PosInSensorX { get; set; }
        public float PosInSensorY { get; set; }
        public float GazeOriginInEyeX { get; set; }
        public float GazeOriginInEyeY { get; set; }
        public float GazeOriginInEyeZ { get; set; }
        public float GazeDirectionNormInEyeX { get; set; }
        public float GazeDirectionNormInEyeY { get; set; }
        public float GazeDirectionNormInEyeZ { get; set; }
        public float Frown { get; set; }
        public float Squeeze { get; set; }
        public float Wide { get; set; }
    }
}