using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ViveSR.anipal.Eye;

namespace Xarphos.DataCollection
{
    public class DataCollector : MonoBehaviour
    {
        public static DataCollector Instance { get; private set; }
        private Data2File TrialConfigHandler { get; set; }
        private Data2File EngineDataHandler { get; set; }
        private Data2File EyeTrackerDataHandler { get; set; }
        private Data2File SingleEyeDataHandlerL { get; set; }
        private Data2File SingleEyeDataHandlerR { get; set; }
        private Data2File SingleEyeDataHandlerC { get; set; }
        private Dictionary<Type, Data2File> allHandlers; 
        
        internal bool recordingPaused = true;

        private void Awake()
        {
            if (Instance != null)
                throw new InvalidOperationException("Can only have 1 'DataCollector' class active");
            Instance = this;

            TrialConfigHandler = gameObject.AddComponent<Data2File>();
            TrialConfigHandler.DataStructure = typeof(TrialConfigRecord);
            EngineDataHandler = gameObject.AddComponent<Data2File>();
            EngineDataHandler.DataStructure = typeof(EngineDataRecord);
            EyeTrackerDataHandler = gameObject.AddComponent<Data2File>();
            EyeTrackerDataHandler.DataStructure = typeof(EyeTrackerDataRecord);
            SingleEyeDataHandlerL = gameObject.AddComponent<Data2File>();
            SingleEyeDataHandlerL.DataStructure = typeof(SingleEyeDataRecord);
            SingleEyeDataHandlerL.FileName += "L";
            SingleEyeDataHandlerR = gameObject.AddComponent<Data2File>();
            SingleEyeDataHandlerR.DataStructure = typeof(SingleEyeDataRecord);
            SingleEyeDataHandlerR.FileName += "R";
            SingleEyeDataHandlerC = gameObject.AddComponent<Data2File>();
            SingleEyeDataHandlerC.DataStructure = typeof(SingleEyeDataRecord);
            SingleEyeDataHandlerC.FileName += "C";

            allHandlers = new Dictionary<Type, Data2File>
            {
                { typeof(TrialConfigRecord), TrialConfigHandler },
                { typeof(EngineDataRecord), EngineDataHandler },
                { typeof(EyeTrackerDataRecord), EyeTrackerDataHandler },
                { typeof(SingleEyeDataRecord), SingleEyeDataHandlerL },
                { typeof(SingleEyeDataRecord), SingleEyeDataHandlerR },
                { typeof(SingleEyeDataRecord), SingleEyeDataHandlerC}
            };
        }

        public void RegisterNewHandler<T>() where T:IDataStructure
        {
            var handler = gameObject.AddComponent<Data2File>();
            handler.DataStructure = typeof(T);
            allHandlers.Add(typeof(T), handler);
        }

        public void StartRecordingNewSubject(string subjId)
        {
            foreach(var h in allHandlers) h.Value.NewSubject(subjId);
        }
    
        public void NewTrial(int blockId, int trialId, bool calibrationTest=false)
        {
            foreach(var h in allHandlers) h.Value.NewTrial(blockId, trialId, calibrationTest);
        }

        public void StopTrial()
        {
            foreach(var h in allHandlers) h.Value.StopTrial();
        }

        public Data2File[] GetHandlerRefs()
        {
            return allHandlers.Values.ToArray();
        }

        public void RecordDataEntry(IDataStructure entry)
        {
            var type = entry.GetType();
            if (!allHandlers.ContainsKey(type))
                throw new InvalidOperationException($"No handler registered for type '{type}'");
            
            if (entry is SingleEyeDataRecord eyeEntry)
            {
                switch (eyeEntry.EyeIndex)
                {
                    case GazeIndex.LEFT:
                        RecordDataEntry(entry, SingleEyeDataHandlerL);
                        break;
                    case GazeIndex.RIGHT:
                        RecordDataEntry(entry, SingleEyeDataHandlerR);
                        break;
                    case GazeIndex.COMBINE:
                        RecordDataEntry(entry, SingleEyeDataHandlerC);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            else
            {
                RecordDataEntry(entry, allHandlers[type]);
            }
        }

        private void RecordDataEntry(IDataStructure entry, Data2File handler)
        {
            if(recordingPaused) return;
            
            handler.AddRecord(entry);
        }
    }
}