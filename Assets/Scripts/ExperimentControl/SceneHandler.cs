using System.Collections;
using System.Collections.Generic;
using System;
using SQLitePCL;
using TMPro;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Random = UnityEngine.Random;

namespace ExperimentControl
{
    /// <summary>
    /// This script controls which 3D scene is active and where the player spawns.
    /// </summary>
    public class SceneHandler : MonoBehaviour
    {
        public static SceneHandler Instance { get; private set; }
       
        private Environment[] _allEnvironments;
        private Environment _currentEnv;
        private int _currentEnvIdx;

        // The environments 
        
        
        private TargetObject _currentTargetObject; // Max. one target should be active at a given time.
        private int _currentTargetIdx;
 
        
        
        // The player and scene GameObjects 
        [Header("XR origin (Player location)")]
        [SerializeField] private GameObject XR_origin;
        [SerializeField] private GameObject waitingScreen;
        
        public Environment[] GetEnvironments()
        {
            return GetComponentsInChildren<Environment>(true);
        }

        public void NextEnvironment(InputAction.CallbackContext ctx) => NextEnvironment();
        public void NextEnvironment()
        {
            // Cycle through house locations
            _currentEnvIdx = (_currentEnvIdx + 1) % _allEnvironments.Length;
            JumpToEnvironment(_allEnvironments[_currentEnvIdx]);
        }

        public void RandomTargetObject(InputAction.CallbackContext ctx) => RandomTargetObject();
        public void RandomTargetObject()
        {
            _currentTargetIdx = Random.Range(0, _currentEnv.targetObjects.Length);
            var target = _currentEnv.targetObjects[_currentTargetIdx];
            ActivateTargetObject(target);
            target.PlayClip();
        }
        
        public void NextTargetObject(InputAction.CallbackContext ctx) => NextTargetObject();
        public void NextTargetObject()
        {
            // Cycle through target objects in current env
            _currentTargetIdx = (_currentTargetIdx + 1) % _currentEnv.targetObjects.Length;
            ActivateTargetObject(_currentEnv.targetObjects[_currentTargetIdx]);
        }

        public void ActivateAllDefaultTargets()
        {
            foreach (var trg in _currentEnv.targetObjects)
            {
                if (trg.defaultActive)
                    trg.Activate();
            }
        }
        
        public void DeactivateAll()
        {
            foreach (var env in _allEnvironments)
            {
                // Deactivate the 3D scene model
                env.DeactivateScene();
                
                // Deactivate target objects within that scene
                foreach (var trg in env.targetObjects) trg.Deactivate();
            }
            Debug.Log("Deactivating waiting screen");
            waitingScreen.SetActive(false);
        }
        
        
        public void JumpToWaitingScreen()
        {
            Debug.Log("Jumping to waiting screen....");
            Debug.Log(_currentEnv);
            if (_currentEnv != null)
                _currentEnv.DeactivateScene();
            SetWaitScreenMessage("Please wait");
            waitingScreen.SetActive(true);
            XR_origin.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
        // public void DeactivateWaitingScreen()
        // {
        //     XR_origin.transform.position = _currentEnv.playerStartLocation.transform.position;
        //     waitingScreen.SetActive(false);
        //     _currentEnv.ActivateScene();
        // }

        public void SetWaitScreenMessage(string message)
        {
            Debug.Log(message);
            var text = waitingScreen.GetComponentInChildren<TextMeshProUGUI>();
            text.text = message;
        }


        public void JumpToEnvironment(Environment newEnv)
        {
            // // If the new room is in a different 3D scene model, then deactivate the old 3D scene. 
            // if (_currentEnv != null)
            //     if(newEnv.GetComponent<Environment>().scene != _currentEnv.GetComponent<Environment>().scene)
            //         _currentEnv.DeactivateScene();

            DeactivateAll();

            // Update the current env with the new env and activate the new 3D scene model
            if (_currentEnv != newEnv)
            {
                _currentTargetIdx = -1; // Reset the index
                _currentEnv = newEnv;
            }

            _currentEnv.ActivateScene();
            
            // Put the XR_origin at the new location
            XR_origin.transform.SetPositionAndRotation(_currentEnv.playerStartLocation.transform.position, 
                                                       _currentEnv.playerStartLocation.transform.rotation);
            Debug.Log(String.Format( "New location: {0} (category: {1})", _currentEnv, _currentEnv.roomCategory));
        }

        public void ActivateTargetObject(TargetObject newTargetObject)
        {
            if (_currentTargetObject != null) _currentTargetObject.Deactivate();
            _currentTargetObject = newTargetObject;
            _currentTargetObject.Activate();
        }

        private void Awake()
        {
            if (Instance != null)
                throw new InvalidOperationException("Can only have 1 'SceneHandler' class active");
            Instance = this;

            _allEnvironments = GetEnvironments();
            SenorSummarySingletons.RegisterType(this);
        }

    }
}