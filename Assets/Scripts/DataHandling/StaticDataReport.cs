using System.IO;
using UnityEngine.Device;

namespace DataHandling
{
    public static class StaticDataReport
    {
        // public static readonly string DataDir = Path.Join(Application.persistentDataPath, "Data");
        
        public static int InZone = 0;
        public static int CollisionCount = 0;

        public static string subjID;
        public static int blockId;
        public static int trialId;
        public static string TrialIdentifier => $"{blockId:D2}_{trialId:D2}";
    }
}
