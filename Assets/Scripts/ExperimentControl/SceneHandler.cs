using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using SQLitePCL;
using TMPro;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Events;
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
        private Environment[] _practiceEnvironments;
        
        private int _currentEnvIdx;
        private Environment _currentEnv;
        public UnityEvent<Environment> environmentChanged;
        public Environment CurrentEnvironment
        {
            get { return _currentEnv; }
            set
            {
                if (value == _currentEnv)
                    return;

                _currentEnv = value;
                environmentChanged.Invoke(value);
            }
            
        }

        // The environments 
        
        
        private TargetObject _currentTargetObject; // Max. one target should be active at a given time.
        private int _currentTargetIdx;
 
        
        
        // The player and scene GameObjects 
        [Header("XR origin (Player location)")]
        [SerializeField] private GameObject XR_origin;
        
        [Header("Waiting screen and calibration testing screen")]
        [SerializeField] private GameObject waitingScreen;

        [SerializeField] private Environment calibrationTestScreen;
        
        public Environment[] GetAllEnvironments() => GetComponentsInChildren<Environment>(true);
        public Environment[] GetPracticeEnvironments()
        {        
            var allEnvs = GetAllEnvironments().ToList();
            return allEnvs.Where(value => value.practiceEnv).ToArray();
        }
        public Environment[] GetExperimentEnvironments()
        {        
            var allEnvs = GetAllEnvironments().ToList();
            return allEnvs.Where(value => value.practiceEnv == false).ToArray();
        }

        public void NextEnvironment(InputAction.CallbackContext ctx) => NextEnvironment();
        private void NextEnvironment()
        {
            // Cycle through house locations
            _currentEnvIdx = (_currentEnvIdx + 1) % _allEnvironments.Length;
            JumpToEnvironment(_allEnvironments[_currentEnvIdx]);
        }
        
        public void NextPracticeEnvironment(InputAction.CallbackContext ctx) => NextPracticeEnvironment();
        private void NextPracticeEnvironment()
        {
            // Cycle through house locations
            _currentEnvIdx = (_currentEnvIdx + 1) % _practiceEnvironments.Length;
            JumpToEnvironment(_practiceEnvironments[_currentEnvIdx]);
        }

        public void RandomTargetObject(InputAction.CallbackContext ctx) => RandomTargetObject();
        public void RandomTargetObject()
        {
            _currentTargetIdx = Random.Range(0, CurrentEnvironment.targetObjects.Length);
            var target = CurrentEnvironment.targetObjects[_currentTargetIdx];
            ActivateTargetObject(target);
            target.PlayClip();
        }
        
        public void NextTargetObject(InputAction.CallbackContext ctx) => NextTargetObject();
        public void NextTargetObject()
        {
            // Cycle through target objects in current env
            _currentTargetIdx = (_currentTargetIdx + 1) % CurrentEnvironment.targetObjects.Length;
            ActivateTargetObject(CurrentEnvironment.targetObjects[_currentTargetIdx]);
        }

        public void ActivateAllDefaultTargets()
        {
            foreach (var trg in CurrentEnvironment.targetObjects)
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
            Debug.Log(CurrentEnvironment);
            if (CurrentEnvironment != null)
                CurrentEnvironment.DeactivateScene();
            SetWaitScreenMessage("Please wait");
            waitingScreen.SetActive(true);
            XR_origin.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
        // public void DeactivateWaitingScreen()
        // {
        //     XR_origin.transform.position = CurrentEnvironment.playerStartLocation.transform.position;
        //     waitingScreen.SetActive(false);
        //     CurrentEnvironment.ActivateScene();
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
            // if (CurrentEnvironment != null)
            //     if(newEnv.GetComponent<Environment>().scene != CurrentEnvironment.GetComponent<Environment>().scene)
            //         CurrentEnvironment.DeactivateScene();

            DeactivateAll();

            // Update the current env with the new env and activate the new 3D scene model
            if (CurrentEnvironment != newEnv)
            {
                _currentTargetIdx = -1; // Reset the index
                CurrentEnvironment = newEnv;
            }

            CurrentEnvironment.ActivateScene();
            
            // Put the XR_origin at the new location
            XR_origin.transform.SetPositionAndRotation(CurrentEnvironment.playerStartLocation.transform.position, 
                                                       CurrentEnvironment.playerStartLocation.transform.rotation);
            Debug.Log(String.Format( "New location: {0} (category: {1})", CurrentEnvironment, CurrentEnvironment.roomCategory));
        }

        public void JumpToCalibrationTestScreen() => JumpToEnvironment(calibrationTestScreen);

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
            environmentChanged = new UnityEvent<Environment>();
            _allEnvironments = GetAllEnvironments();
            _practiceEnvironments = GetPracticeEnvironments();
            SenorSummarySingletons.RegisterType(this);
        }

    }
}