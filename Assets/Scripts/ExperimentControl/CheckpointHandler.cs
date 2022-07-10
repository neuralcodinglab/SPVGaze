using DataHandling;
using UnityEngine;

namespace ExperimentControl
{
    public class CheckpointHandler : MonoBehaviour
    {
        internal bool InCheckpoint => checkpointID == int.MaxValue;
        private int checkpointID = int.MaxValue;
        private bool checkpointInFrontOnEnter = false; 
        
        private void OnCollisionEnter(Collision collision)
        {
            var other = collision.gameObject;
            // starting to cross checkpoint
            if (checkpointID != int.MaxValue)
            {
                Debug.LogWarning("Checkpoint ID was not reset correctly.");
            }
            checkpointID = other.GetInstanceID();
            checkpointInFrontOnEnter = Mathf.Sign(other.transform.position.z - transform.position.z) > 0;
        }

        private void OnCollisionExit(Collision collision)
        {
            var other = collision.gameObject;
            if (other.GetInstanceID() != checkpointID)
            {
                Debug.LogWarning("Exited a Checkpoint with a different ID than last entered. What?");
                checkpointID = int.MaxValue;
                return;
            }
            checkpointID = int.MaxValue;

            var checkpointIsBehind = Mathf.Sign(other.transform.position.z - transform.position.z) < 0;
            switch (checkpointInFrontOnEnter)
            {
                // moved forward in the hallway
                case true when checkpointIsBehind:
                    StaticDataReport.InZone += 1;
                    break;
                // moved backwards through checkpoint
                case false when !checkpointIsBehind:
                    StaticDataReport.InZone -= 1;
                    break;
                default:
                    break; // did not cross, but entered and moved back out            
            }
            Debug.Log($"Passed checkpoint! Now in zone {StaticDataReport.InZone}");
        }
    }
}
