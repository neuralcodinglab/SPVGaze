using System;
using DataHandling;
using UnityEngine;

namespace ExperimentControl
{
    public class CollisionHandler : MonoBehaviour
    {
        private InputHandler inputHandler;
        internal bool InBox;

        private void Start()
        {
            inputHandler = GetComponentInParent<InputHandler>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (RunExperiment.Instance.betweenTrials) return;
            if (collision.contactCount < 1) return;
            if (InBox)
            {
                Debug.LogWarning("Walked into a box, while we thought we are in a box.");
            }
            InBox = true;
            StaticDataReport.CollisionCount += 1;
        
            // start vibration pattern
            inputHandler.StartCollisionVibration();
        }

        private void OnCollisionExit(Collision other)
        {
            if (RunExperiment.Instance.betweenTrials) return;
            InBox = false;
            // stop vibration pattern
            inputHandler.StopCollisionVibration();
        }
    }
}
