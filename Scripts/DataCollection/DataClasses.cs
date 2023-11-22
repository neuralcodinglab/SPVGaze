using Xarphos.Simulation;
using UnityEngine;
using ViveSR.anipal.Eye;

/// <summary>
/// A collection of structs to organise the data fields that are to be tracked and saved.
/// </summary>
namespace Xarphos.DataCollection
{
    public interface IDataStructure{}
    
    public enum Glasses { None, Glasses, Contacts }
    
    /// <summary>
    /// Minimal data structure for the trial configuration, collecting the most common information.
    /// This should most likely be replaced with a more custom fit data structure.
    /// </summary>
    public struct TrialConfigRecord : IDataStructure
    {
        public EyeTracking.EyeTrackingConditions GazeCondition { get; set; }
        public Glasses Glasses { get; set; }
        public double GazeRaySensitivity { get; set; }
        public string DataDelimiter { get; set; }
        public float TrialDuration { get; set; }
    }

    /// <summary>
    /// Collecting the data from the engine, including the transform data of the XR rig and the hand controllers.
    /// Can be extended to include more data from the engine, like environment information (e.g. collision counts, etc.)
    /// Though those could arguably also be collected in a separate data structure. 
    /// </summary>
    public struct EngineDataRecord : IDataStructure
    {
        public long TimeStamp { get; set; }
        public Vector3 XROriginPos { get; set; }
        public Quaternion XROriginRot { get; set; }
        public Vector3 XRHeadPos { get; set; }
        public Quaternion XRHeadRot { get; set; }
        public Vector3 HandLPos { get; set; }
        public Quaternion HandLRot { get; set; }
        public Vector3 HandRPos { get; set; }
        public Quaternion HandRRot { get; set; }
        public int FrameCount { get; set; }        
        public Vector3 PointLocationHand { get; set; }
        public Vector3 PointLocationEye { get; set; }
        public Vector3 PointLocationHead { get; set; }
    }

    /// <summary>
    /// Collecting all available data from the eye tracker.
    /// </summary>
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

    /// <summary>
    /// All data from the eye tracker that is available for the combined and individual eyes.
    /// </summary>
    public struct SingleEyeDataRecord : IDataStructure
    {
        public long TimeStamp { get; set; }
        public int TrackerTimeStamp { get; set; }
        public GazeIndex EyeIndex { get; set; }
        public ulong Validity { get; set; }
        public float Openness { get; set; }
        public float PupilDiameter { get; set; }
        public Vector2 PosInSensor { get; set; }
        public Vector3 GazeOriginInEye { get; set; }
        public Vector3 GazeDirectionNormInEye { get; set; }
    }
}