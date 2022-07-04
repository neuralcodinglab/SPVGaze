using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

// using Sirenix.Utilities;

namespace DataHandling
{
    public class DataCollector
    {
        public readonly string subjID;
        public readonly string trialID;

        public string Delimiter { get; }
        private string _filePath;
        private FileStream _fileStream;
        private Task _currentWritingOp;

        private Queue<object> _writeQueue;

        private string _fileHeader;
        private Dictionary<string, DataDescription> _schema;
        private Dictionary<string, DataDescription> _defaultSchema = new()
        {
            { "TimeStamp", new DataDescription{ DType=typeof(long) } }, // DateTime.Now.Ticks 
            { "XROrigin_Pos", new DataDescription{ DType=typeof(Vector3)} },
            { "XROrigin_Rot", new DataDescription{ DType=typeof(Vector3)} },
            { "XRHead_Pos", new DataDescription{ DType=typeof(Vector3)} },
            { "XRHead_Rot", new DataDescription{ DType=typeof(Vector3)} },
            { "HandL_Pos", new DataDescription{ DType=typeof(Vector3)} },
            { "HandL_Rot", new DataDescription{ DType=typeof(Vector3)} },
            { "HandR_Pos", new DataDescription{ DType=typeof(Vector3)} },
            { "HandL_Rot", new DataDescription{ DType=typeof(Vector3)} },
            { "CollisionCount", new DataDescription{ DType=typeof(int), Min=0} },
            // Eye Data
            { "Eye_TimeStamp", new DataDescription{DType=typeof(int)} },
            { "Eye_TrackingImprovementCount", new DataDescription{DType=typeof(int)} },
            { "Eye_GazeRaySensitivity", new DataDescription{DType=typeof(double), Min=0, Max=1} },
            // Combined Data
            { "Eye_ConvergenceDistance", new DataDescription{DType=typeof(float)} },
            { "Eye_ConvergenceDistanceValidity", new DataDescription{DType = typeof(bool)} },
            { "Eye_FrameCount", new DataDescription{DType=typeof(int), Min=0} },
            { "Eye_TrackingImprovementCount", new DataDescription{DType=typeof(int)} },
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
    };

        public DataCollector(string subjectID = "_TEST", string trialID = "001", string delim = "\t")
        {
            subjID = subjectID;
            this.trialID = trialID;
            Delimiter = delim;
            
            _filePath = Path.Join(
                Application.persistentDataPath,
                subjID,
                (trialID + ".csv")
            );

            SenorSummarySingletons.RegisterType(this);
        }

        public void Initialise(Dictionary<string, DataDescription> schema = null)
        {
            schema ??= _defaultSchema;
            _schema = schema;
            
            _fileStream = new FileStream(_filePath, 
                FileMode.Create, FileAccess.Write, FileShare.None, 
                4096, FileOptions.Asynchronous); // default buffer size is 4096

            _writeQueue = new Queue<object>();
            
            _fileHeader = ToCSVFields(schema.Keys.ToArray());
            WriteHead();
        }

        public void AddRecord(IEnumerable<object> data)
        {
            // ToDo
        }

        private async void WriteHead()
        {
            var byteArray = Encoding.UTF8.GetBytes(_fileHeader);
            _currentWritingOp = _fileStream.WriteAsync(byteArray).AsTask();
            await _currentWritingOp;
        }

        private string ToCSVFields(IEnumerable<object> values, bool addEOL = true)
        {
            var strValues = values.Cast<string>();
                // values.Convert(field => field == null ? "" : field.ToString());

            return ToCSVFields(strValues, addEOL);
        }

        private string ToCSVFields(IEnumerable<string> values, bool addEOL=true)
        {
            var row = "";
            var delimReplace = Delimiter switch
            {
                "\t" => " ",
                "," => ".",
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

            if (addEOL) row += "\n";
            return row;
        }
    }

    public struct DataDescription
    {
        public Type DType;
        public float Min;
        public float Max;
    }
}