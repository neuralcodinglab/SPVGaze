using System.Collections;
using System.Collections.Generic;
using System;
using SQLitePCL;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ExperimentControl
{
    /// <summary>
    /// This script controls which 3D scene is active and where the player spawns.
    /// </summary>
    public class SceneHandler : MonoBehaviour
    {
        public static SceneHandler Instance { get; private set; }
        public enum Task 
        {
            Mobility = 0, // Navigation task in the 3D obstacle course environments
            Orientation = 1, // Orientation task (scene-recognition) in the 3D house environments
            VisualSearch = 2 // Localization of blinking target objects in the 3D house environment (living room 1) 
        }
        private Task _currentTask;

        public class Location 
        {
            public int Idx {get; set;}
            public GameObject Scene {get; set;} // The 3D environment (gameobject) 
            public Vector3 StartPosition {get; set;} // The position within the house
            public string RoomCategory {get; set;} // the category of the room
            public void DeactivateScene() => Scene.SetActive(false);
            public void ActivateScene() => Scene.SetActive(true);
        }
        private Location _activeLocation; // Only one scene should be active at a given time.

        public class Target
        {
            public int Idx {get; set;}
            public GameObject Object {get; set;}
            private bool _blinking;
            
            public void Activate()
            {
                Object.SetActive(true);
                _blinking = true;
                Instance.StartCoroutine(Blink());
            }
            
            public void Deactivate()
            {
                Object.SetActive(false);
                _blinking = false;
                Instance.StopCoroutine(Blink());
            }
            
            private IEnumerator Blink()
            {
                var renderers = Object.GetComponentsInChildren<Renderer>();
                // Blink this target on and off
                while (_blinking)
                {
                    foreach (var r in renderers) r.enabled = false;
                    yield return new WaitForSeconds(0.2f);
                    foreach (var r in renderers) r.enabled = true;
                    yield return new WaitForSeconds(0.2f);
                }
            }

        }
        private Target _activeTarget; // Max. one target should be active at a given time.
        
        
        // The player and scene GameObjects 
        [Header("Player (XR origin)")]
        [SerializeField] private GameObject player;
        
        
        [Header("Mobility courses (navigation task)")]
        [SerializeField] public GameObject[] mobilityCourses;
        private Location[] _allMobilityCourses;
        
        [Header("Orientation scenes (scene recognition task)")]
        [SerializeField] private GameObject[] houses;
        private Location[] _allHouseLocations;

        [Header ("Target Objects (visual search task)")]
        [SerializeField] public GameObject[] targetObjects;
        private Target[] _allTargets;

        private Location[] InitHouseLocations()
        {
            // Initialize locations (house, position) for the orientation tasks 
            var locs = new Location[13];
            locs[0] = new Location{Idx = 0, Scene = houses[0], StartPosition = new Vector3(0, 0f, 0.25f), RoomCategory = "living"} ;
            locs[1] = new Location{Idx = 1, Scene = houses[1], StartPosition = new Vector3(0.4f, 0f, 3.6f), RoomCategory = "living"};
            locs[2] = new Location{Idx = 2, Scene = houses[1], StartPosition = new Vector3(6.0f, 0f, 7.2f), RoomCategory = "bedroom"};
            locs[3] = new Location{Idx = 3, Scene = houses[1], StartPosition = new Vector3(2.5f, 0f, 6.9f), RoomCategory = "bedroom"};
            locs[4] = new Location{Idx = 4, Scene = houses[1], StartPosition = new Vector3(6.3f, 0f, 3.5f), RoomCategory = "kitchen"};
            locs[5] = new Location{Idx = 5, Scene = houses[1], StartPosition = new Vector3(-1.3f, 0f, 7.2f), RoomCategory = "bathroom"};
            locs[6] = new Location{Idx = 6, Scene = houses[2], StartPosition = new Vector3(2.3f, 0f, 0.5f), RoomCategory = "living"};
            locs[7] = new Location{Idx = 7, Scene = houses[2], StartPosition = new Vector3(11.2f, 0f, -1.65f), RoomCategory = "bedroom"};
            locs[8] = new Location{Idx = 8, Scene = houses[2], StartPosition = new Vector3(-4.6f, 0f, -1.5f), RoomCategory = "kitchen"};
            locs[9] = new Location{Idx = 9, Scene = houses[2], StartPosition = new Vector3(-8.0f, 0f, -2.0f), RoomCategory = "bathroom"};
            locs[10] = new Location{Idx = 10, Scene = houses[3], StartPosition = new Vector3(-12.0f, 0.97f, -5.7f), RoomCategory = "living"};
            locs[11] = new Location{Idx = 11, Scene = houses[3], StartPosition = new Vector3(-15.7f, 0.97f, 0.9f), RoomCategory = "kitchen"};
            locs[12] = new Location{Idx = 12, Scene = houses[3], StartPosition = new Vector3(1.6f, 3.05f, -3.2f), RoomCategory = "bedroom"};
            return locs;
        }
        
        private Location[] InitMobilityCourses()
        {
            // Initialize locations (house, StartPosition) for the orientation tasks 
            var locs = new Location[6];
            locs[0] = new Location{Idx = 0, Scene = mobilityCourses[0], StartPosition = new Vector3(0, 0f, -3.0f), RoomCategory = "A"};
            locs[1] = new Location{Idx = 1, Scene = mobilityCourses[1], StartPosition = new Vector3(0, 0f, -4.0f), RoomCategory = "B"};
            locs[2] = new Location{Idx = 2, Scene = mobilityCourses[2], StartPosition = new Vector3(0, 0f, -4.0f), RoomCategory = "C"};
            locs[3] = new Location{Idx = 3, Scene = mobilityCourses[3], StartPosition = new Vector3(0, 0f, -4.0f), RoomCategory = "D"};
            locs[4] = new Location{Idx = 4, Scene = mobilityCourses[4], StartPosition = new Vector3(0, 0f, -4.0f), RoomCategory = "E"};
            locs[5] = new Location{Idx = 5, Scene = mobilityCourses[5], StartPosition = new Vector3(0, 0f, -4.0f), RoomCategory = "F"};
            return locs;
        }

        private Target[] InitTargets()
        {
            var i = 0;
            var targets = new Target[targetObjects.Length];
            foreach (var obj in targetObjects) targets[i] = new Target {Idx = i++, Object = obj};
            return targets;
        }

        public void JumpToLocation(Location newLocation)
        {
            _activeLocation = newLocation;
            _activeLocation.ActivateScene();
            player.transform.position = _activeLocation.StartPosition;
            Debug.Log(String.Format( "New location: {0} (category: {1})", _activeLocation.Idx, _activeLocation.RoomCategory));
        }
        public void DeactivateAll()
        {
            foreach (var loc in _allHouseLocations) loc.DeactivateScene();
            foreach (var loc in _allMobilityCourses) loc.DeactivateScene();
            foreach (var trg in _allTargets) trg.Deactivate();
            _activeLocation = new Location(); // Empty location
            _activeTarget = new Target(); // Empty Target
        }

        public void DeactivateAllTargetObjects() {foreach (var trg in _allTargets) trg.Deactivate();}

        public void NextHouseLocation(InputAction.CallbackContext ctx) => NextHouseLocation();
        
        public void NextHouseLocation()
        {
            // Cycle through house locations
            if (_activeLocation.Scene == null || _currentTask != Task.Orientation)
            {
                _currentTask = Task.Orientation;
                DeactivateAll();
                JumpToLocation(_allHouseLocations[0]);
            }
            else
            {
                var nextIdx = (_activeLocation.Idx + 1) % _allHouseLocations.Length;
                DeactivateAll();
                JumpToLocation(_allHouseLocations[nextIdx]);
            }
        }
        
        public void NextMobilityCourse(InputAction.CallbackContext ctx) => NextMobilityCourse();
        
        public void NextMobilityCourse()
        {
            // Cycle through mobility courses
            if (_activeLocation.Scene == null || _currentTask != Task.Mobility)
            {
                _currentTask = Task.Mobility;
                DeactivateAll();
                JumpToLocation(_allMobilityCourses[0]);
            }
            else
            {
                var nextIdx = (_activeLocation.Idx + 1) % _allMobilityCourses.Length;
                Debug.Log("current: " + _activeLocation.Idx + "  Next: " + nextIdx);
                DeactivateAll();
                JumpToLocation(_allMobilityCourses[nextIdx]);
            }
        }

        public void NextVisualSearchTarget(InputAction.CallbackContext ctx) => NextVisualSearchTarget();
        public void NextVisualSearchTarget()
        {   // Cycle through visual search targets
            if (_activeTarget.Object == null || _currentTask != Task.VisualSearch)
            {
                DeactivateAll();
                _currentTask = Task.VisualSearch;
                _activeTarget = _allTargets[0];
                JumpToLocation(_allHouseLocations[0]); // JR: the visual search task EXCLUSIVELY uses the first house for now
            }
            else
            {
                _activeTarget.Deactivate();
                var nextIdx = (_activeTarget.Idx + 1) % _allTargets.Length;
                _activeTarget = _allTargets[nextIdx];
            }
            _activeTarget.Activate(); 
        }
        
        private void Awake()
        {
            if (Instance != null)
                throw new InvalidOperationException("Can only have 1 'SceneHandler' class active");
            Instance = this;

            _allHouseLocations = InitHouseLocations();
            _allMobilityCourses = InitMobilityCourses();
            _allTargets = InitTargets();
            Debug.Log("First target " + _allTargets[0].Object);
            DeactivateAll();
            SenorSummarySingletons.RegisterType(this);
        }

    }
}