using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Xarphos.Scripts
{
    public class ExperimentSetup : MonoBehaviour
    {
        public Camera targetCam;
        [Header("Experiment Setup")] public TMP_InputField subjectID;
        public TMP_Dropdown condition;
        public Button btnStartExperiment;

        [Header("Control Buttons")] public Button btnCalibrateEyeTracking;
        public Button btnTestHaptic,
            btnResetPosition,
            btnTogglePhosphenes,
            btnToggleCamLock,
            btn6;

        [Header("Monitoring")]
        public TMP_Text avgFrameTimeMS, avgFrameTimeFPS;
        
        private readonly float[] _frameTimes = new float[5];
        private int _frameCount = 0;
        
        // Start is called before the first frame update
        void Start()
        {
            targetCam ??= GetComponentInChildren<Camera>();
        }

        // Update is called once per frame
        void Update()
        {
            _frameTimes[_frameCount] = Time.deltaTime;
            _frameCount = (_frameCount + 1) % _frameTimes.Length;
            var runningavg = _frameTimes.Average();
            avgFrameTimeMS.text = $"{runningavg*1000f:F}";
            avgFrameTimeFPS.text = $"{1/runningavg:F1}";
        }
        
        
    }
}
