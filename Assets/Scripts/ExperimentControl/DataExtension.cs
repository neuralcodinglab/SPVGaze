using Xarphos.DataCollection;
using Xarphos.Simulation;

namespace ExperimentControl
{
    public struct TrialConfigRecord : IDataStructure
    {
        public RunExperiment.Task ExperimentalTask { get; set; }
        public EyeTracking.EyeTrackingConditions GazeCondition { get; set; }
        public string EnvironmentName { get; set; }
        public Environment.RoomCategory EnvironmentClass { get; set; }
        public Glasses Glasses { get; set; }
        public double GazeRaySensitivity { get; set; }
        public string DataDelimiter { get; set; }
        public Environment.RoomCategory ReportedRoomCategory { get; set; }
        public int ReportedSubjectiveRating { get; set; }
        public int ReportedEventsCount { get; set; }
        public float TrialDuration { get; set; }
    }
}