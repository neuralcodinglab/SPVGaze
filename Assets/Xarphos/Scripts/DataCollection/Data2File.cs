using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ViveSR.anipal.Eye;
using Quaternion = System.Numerics.Quaternion;


namespace Xarphos.DataCollection
{
    public interface IDataHandler
    {
        public void NewSubject(string subjId);
        public void NewTrial(int blockId, int trialId);
        public void StopTrial();
    }
    
    /// <summary>
    /// Class to handle streaming engine and eyetracking data into tsv files. Uses a blocks and trials to organise data.
    /// </summary>
    public class Data2File : MonoBehaviour, IDataHandler
    {
        public string FileName { get; internal set; }
        public const string FileEnding = ".tsv";
        public const string Delimiter = "\t";
        
        private bool isInitialised;
        private Type dataType;

        public Type DataStructure
        {
            get => dataType;
            set => Init(value);
        }

        protected string SubjectDir;
        protected bool IsRecording;
        protected FileStream Fs;
        protected Queue<IDataStructure> WriteQ;
        protected PropertyInfo[] Properties;
        protected string Header;
        
        internal IList<Task> TaskList;
        internal IList<Queue<IDataStructure>> RemainingItems;
        internal IList<FileStream> OldStreams;

        private void Init(Type dataStructure)
        {
            if (isInitialised) return;
            if ( dataStructure.GetInterface(nameof(IDataStructure)) == null )
            {
                throw new ArgumentException($"Tried to initialise Data Handler with a type that is not IDataStructure: {dataStructure}");
            }
            
            isInitialised = true;
            dataType = dataStructure;

            FileName = dataType.Name;
            Properties = dataType.GetProperties();
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

        public void NewTrial(int blockId, int trialId) => NewTrial(blockId, trialId, false);
        public void NewTrial(int blockId, int trialId, bool calibrationTest)
        {
            CheckInitialised();

            if (IsRecording) 
                throw new InvalidOperationException("Forgot to end last trial before starting new one");
            
            // If validationTest, add flag to filename
            var trialStr = $"{blockId:D2}_{trialId:D2}";
            if (calibrationTest) trialStr = $"{trialStr}_calibrationTest_";
            
            // If file exists, don't override, but create new filename (trailing _ inserted after trialStr)
            var path = Path.Join(SubjectDir, trialStr + FileName + FileEnding);
            while (File.Exists(path))
            {
                trialStr = $"{trialStr}_";
                path = Path.Join(SubjectDir, trialStr + FileName + FileEnding);
            }
            
            // Create filestream
            Fs = new FileStream(
                path,
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
                    string row;
                    try
                    {
                        row = Record2Row(entry);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Writing entry to file failed. DataStructture {dataType.Name}. Exception: {e}");
                        continue;
                    }
                    yield return Row2File(row);
                }
                yield return null;
            }
        }

        public void AddRecord(IDataStructure record)
        {
            CheckInitialised();
            if (record.GetType() != dataType)
            {
                throw new ArgumentException(
                    $"Handed over incorrect Data Structure. Expected {dataType}, but got {record.GetType()}");
            }

            WriteQ.Enqueue(record);
        }

        private async Task RunCleanUp()
        {
            IsRecording = false;
            if (WriteQ == null && Fs == null) return;
            
            StopAllCoroutines();
            var remainingEntries = WriteQ;
            RemainingItems ??= new List<Queue<IDataStructure>>();
            RemainingItems.Add(remainingEntries);

            OldStreams ??= new List<FileStream>();
            var oldFs = Fs;
            OldStreams.Add(oldFs);
            
            // Debug.Log($"Stopping Co-Routine. Still writing {remainingEntries.Length} entries to file.");
            while (remainingEntries.TryDequeue(out var entry))
            {
                var row = Record2Row(entry);
                await Row2File(row);
            }
            await oldFs.FlushAsync();
            oldFs.Close();
            
            RemainingItems.Remove(remainingEntries);
            OldStreams.Remove(oldFs);
        }

        public void StopTrial()
        {
            CheckInitialised();
            TaskList ??= new List<Task>();
            TaskList.Add(RunCleanUp());
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

        private void WaitForFileWritingToComplete()
        {
            if (TaskList == null) return;
            // Debug.Log($"Waiting for {TaskList.Count(t => !t.IsCompleted)} tasks to finish.");
            Task.WaitAll(TaskList.ToArray());
        }

        private void OnDestroy()
        {
            WaitForFileWritingToComplete();
        }

        private void OnDisable()
        {
            WaitForFileWritingToComplete();
        }

        ~Data2File()
        {
            WaitForFileWritingToComplete();
        }
    }
}