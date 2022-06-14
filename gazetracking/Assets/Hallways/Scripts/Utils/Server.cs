using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using System.Collections;
using indoorMobility.Scripts.Game;

namespace indoorMobility.Scripts.Utils
{
    public class Server
    {
        #region;

        private AppData appData; // (store connection status in appData)
        private readonly IPAddress _ip;
        private readonly int _port;
        private TcpClient _tcpClient;
        private TcpListener _tcpListener;

        public delegate void DataReadEventListener(byte[] data);

        public event DataReadEventListener DataRead;

     
        #endregion;

        #region;

        public Server(IPAddress ip, int port)
        {
            _ip = ip;
            _port = port;



    }


        public async void Start()
        {
            appData = GameObject.Find("GameManager").GetComponent<GameManager>().appData;
            try
            {
                _tcpListener = new TcpListener(_ip, _port);
                _tcpListener.Start();
                Debug.Log("Server was started.");
                while (_tcpListener != null)
                {
                    var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                    if (_tcpClient == null)
                    {
                        _tcpClient = tcpClient;
                        OnDataRead();

                        // Visualize connection status in GUI
                        Debug.Log("Client was connected.");
                        appData.ClientConnected = true;
                        GameObject.Find("GUI").GetComponent<GUIHandler>().UpdateRunningStatus();
                    }
                    else
                    {
                        Debug.Log("Client was not connected.");
                    }
                }
            }
            catch (Exception e)
            {
                if (_tcpListener != null) Debug.Log(e.ToString());
            }
        }

        public void Stop()
        {
            try
            {
                _tcpClient?.Close();
                _tcpClient = null;
                _tcpListener?.Stop();
                _tcpListener = null;
                Debug.Log("Server was stopped.");
            }
            catch (Exception e)
            {
                if (_tcpListener != null) Debug.Log(e.ToString());
            }
        }

        private async void OnDataRead()
        {
            try
            {
                var data = new byte[2];
                var networkStream = _tcpClient.GetStream();
                while (_tcpClient != null)
                {
                    var length = 0;
                    while (length < data.Length)
                    {
                        var task = await networkStream.ReadAsync(data, length, 2 - length);
                        if (task == 0)
                        {
                            _tcpClient?.Close();
                            _tcpClient = null;
                            break;
                        }

                        length += task;
                    }

                    DataRead?.Invoke(data);
                    Array.Clear(data, 0, 2);
                }

                // Visualize connection status
                Debug.Log("Client was disconnected.");
                appData.ClientConnected = false;
                GameObject.Find("GUI").GetComponent<GUIHandler>().UpdateRunningStatus();
            }
            catch (Exception e)
            {
                if (_tcpListener != null) Debug.Log(e.ToString());
            }
        }

        public void OnDataSent(byte[] data)
        {
            try
            {
                _tcpClient.GetStream().WriteAsync(data, 0, data.Length);
            }
            catch (Exception e)
            {
                if (_tcpListener != null) Debug.Log(e.ToString());
            }
        }

        #endregion;
    }
}