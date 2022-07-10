using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Sirenix.Utilities;
using UnityEngine;
using ViveSR.anipal.Eye;
using Quaternion = System.Numerics.Quaternion;
// ReSharper disable MemberCanBePrivate.Global

namespace DataHandling.Separated
{
    public interface IDataHandler
    {
        public void NewSubject(string subjId);
        public void NewTrial(int blockId, int trialId);
        public void StopTrial();
    }
    
    /// <summary>
    /// 
    /// </summary>
    public class Data2File : MonoBehaviour, IDataHandler
    {
        public string FileName { get; protected set; }
        public const string FileEnding = ".csv";
        public const string Delimiter = "\t";
        
        private bool isInitialised;
        private Type _dataType;

        public Type DataStructure
        {
            get => _dataType;
            set => Init(value);
        }

        protected string SubjectDir;
        protected bool IsRecording;
        protected FileStream Fs;
        protected Queue<IDataStructure> WriteQ;
        protected PropertyInfo[] Properties;
        protected string Header;

        private void Init(Type dataStructure)
        {
            if (isInitialised) return;
            if (!dataStructure.ImplementsOrInherits(typeof(IDataStructure)))
            {
                throw new ArgumentException($"Tried to initialse Data Handler with a type that is not IDataStructure: {dataStructure}");
            }
            
            isInitialised = true;
            _dataType = dataStructure;

            FileName = _dataType.Name;
            Properties = _dataType.GetProperties();
            Header = ToCsvFields(Properties.Select(prop => prop.Name));
        }

        private void CheckInitialised()
        {
            if (!isInitialised)
                throw new InvalidOperationException("Trying to interact with Datahandler before it is initialised");
        }

        public void NewSubject(string subjId)
        {
            CheckInitialised();
            
            var dataDir = Application.persistentDataPath;
            SubjectDir = Path.Join(dataDir, subjId);
            Directory.CreateDirectory(SubjectDir);
            IsRecording = false;
        }
        
        public void NewTrial(int blockId, int trialId)
        {
            CheckInitialised();

            if (IsRecording) 
                throw new InvalidOperationException("Forgot to end last trial before starting new one");

            var trialStr = $"{blockId:D2}_{trialId:D2}";
            Fs = new FileStream(
                Path.Join(SubjectDir, trialStr + FileName + FileEnding),
                FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.Asynchronous
            );

            IsRecording = true;
            // Write Header
            Fs.Write(Encoding.UTF8.GetBytes(Header));
            
            WriteQ = new Queue<IDataStructure>();
            StartCoroutine(ReadFromQueue());
        }

        protected IEnumerator ReadFromQueue()
        {
            while (IsRecording)
            {
                while (WriteQ.TryDequeue(out var entry))
                {
                    var row = Record2Row(entry);
                    yield return Row2File(row);
                }
                yield return null;
            }
        }

        public void AddRecord(IDataStructure record)
        {
            CheckInitialised();
            if (record.GetType() != _dataType)
            {
                throw new ArgumentException(
                    $"Handed over incorrect Data Structure. Expected {_dataType}, but got {record.GetType()}");
            }

            WriteQ.Enqueue(record);
        }

        public async void StopTrial()
        {
            CheckInitialised();

            IsRecording = false;
            while (WriteQ.Count > 0)
            {
                await Task.Yield();
            }
            await Fs.FlushAsync();
            Fs.Close();
        }
        
        protected string Record2Row(IDataStructure entry)
        {
            var props = Properties
                .Select(prop => Object2String(prop.GetValue(entry)))
                .ToList();
            return ToCsvFields(props);
        }

        protected async Task Row2File(string row)
        {
            await Fs.WriteAsync(Encoding.UTF8.GetBytes(row));
        }

        protected string Object2String(object field)
        {
            return field switch
            {
                null => "",
                float f => f.ToString("F5"),
                double d => d.ToString("F3"),
                Vector2 vector2 => vector2.ToString("F5"),
                Vector3 vector3 => vector3.ToString("F5"),
                Quaternion quaternion => quaternion.ToString(),
                ulong bitmask => Convert.ToString((long) bitmask, 2),
                TrackingImprovements improvements => 
                    Object2String(improvements.items.Aggregate((ulong) 0,
                        (agg, improvement) => agg | (uint) (1 << (int)improvement))),
                _ => field.ToString()
            };
        }

        protected string ToCsvFields(IEnumerable<string> values, bool addEol=true)
        {
            var row = "";
            var delimReplace = Delimiter switch
            {
                // ReSharper disable HeuristicUnreachableCode
                "\t" => " ",
                "," => "\t",
                ";" => "|",
                "|" => ";",
                _ => "||"
                // ReSharper restore HeuristicUnreachableCode
            };
            foreach(var field in values)
            {
                var toAdd = field;
                if (field.Contains(Delimiter))
                {
                    toAdd = field.Replace(Delimiter, delimReplace);
                }
                // remove any line endings
                toAdd = toAdd
                    .Replace("\r\n", @"\n")
                    .Replace("\r", @"\n")
                    .Replace("\n", @"\n");
                row += toAdd + Delimiter;
            }

            if (addEol) row += "\n";
            return row;
        }
    }
}