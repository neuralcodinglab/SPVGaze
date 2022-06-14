using System;
using System.Collections;
using System.Net;
using UnityEngine;
using Random = UnityEngine.Random;
using indoorMobility.Scripts.Utils;
using Environment = indoorMobility.Scripts.Game.Environment; // prevent ambiguity with System.Environment

namespace indoorMobility.Scripts.Game
{
    public class GameManager : MonoBehaviour
    {
        #region;
        private Command _command;
        private Server _server;

#pragma warning disable 0649 //disable warnings about serializefields not being assigned that occur in certain unity versions
        [SerializeField] public AppData appData;
        [SerializeField] private GameObject Environment;
        [SerializeField] private GameObject GUI;
        private Environment environment;
        private GUIHandler guiHandler;

#pragma warning restore 0649 //reanable the unassigned variable warnings 
        public delegate void DataSentEventListener(byte[] data);
        public event DataSentEventListener DataSent;
        #endregion;



        #region;
        private void OnDataReceived(byte[] data)
        {
            environment.Input = data[1];
            _command = (Command)data[0];
        }

        private void OnDataSent()
        {
            DataSent?.Invoke(environment.Output);
        }


        // This enumerator is invoked as co-routine (constantly waiting for input commands)
        private IEnumerator Tick(float timescale)
        {
            _command = Command.None;
            Time.timeScale = 0;
            while (true)
                switch (_command)
                {   case Command.None:
                        yield return null;
                        continue;

                    // Reset environment
                    case Command.Reset:  
                        environment.Reset();
                        Time.timeScale = timescale;
                        yield return new WaitForFixedUpdate();
                        Time.timeScale = 0;
                        OnDataSent();
                        _command = Command.None;
                        guiHandler.UpdateRunningStatus();
                        break;

                    // Make the agent move
                    case Command.Step:
                        environment.Step();
                        Debug.Log("environment step command was executed");
                        Time.timeScale = timescale;
                        yield return new WaitForFixedUpdate();
                        Time.timeScale = 0;
                        OnDataSent();
                        _command = Command.None;
                        break;

                    // Manually set the random number generator seed 
                    case Command.SetSeed:
                        environment.SetManualSeed();
                        Debug.Log("using manually specified RNG seed");
                        Time.timeScale = timescale;
                        yield return new WaitForFixedUpdate();
                        Time.timeScale = 0;
                        OnDataSent();
                        _command = Command.None;
                        break;

                    default: throw new ArgumentOutOfRangeException();
                }
        }
        #endregion;


        #region;
        protected void Awake() //Gets run when the game starts once
        {   // Instantiate environment
            appData.RandomSeed = (int)System.DateTime.Now.Ticks;
            environment = Environment.GetComponent<Environment>(); // Get script component from GameObject 'Environment'

            // Let the GUI display the server status 
            guiHandler = GUI.GetComponent<GUIHandler>();
            guiHandler.UpdateRunningStatus();

            // Start server
            appData.ClientConnected = false;
            _server = new Server(IPAddress.Parse(appData.IpAddress), appData.Port);
            _server.DataRead += OnDataReceived;
            DataSent += _server.OnDataSent;
            _server.Start();
            StartCoroutine(Tick(appData.TimeScale));
        }

#endregion;
    }
}
 
 