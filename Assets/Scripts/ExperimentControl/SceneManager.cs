using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

namespace ExperimentControl
{
    public class SceneManager : MonoBehaviour
    {
        [SerializeField] private GameObject player;
        [SerializeField] private GameObject[] scenes;
        public Location[] _allLocations;
        public Location _currentLocation;

        public struct Location 
        {
            public int idx;
            public GameObject scene;
            public Vector3 position; 
            public string category;
        }
        public Location[] InitStartLocations()
        {
            // Each startlocation (scene, position) points to a specific room 
            var locs = new Location[13];
            locs[0] = new Location{ idx = 1, scene = scenes[0], position = new Vector3(0, 0f, 0.25f), category = "living"} ;
            locs[1] = new Location{ idx = 2, scene = scenes[1], position = new Vector3(0.4f, 0f, 3.6f), category = "living"};
            locs[2] = new Location{ idx = 3, scene = scenes[1], position = new Vector3(6.0f, 0f, 7.2f), category = "bedroom"};
            locs[3] = new Location{ idx = 4, scene = scenes[1], position = new Vector3(2.5f, 0f, 6.9f), category = "bedroom"};
            locs[4] = new Location{ idx = 5, scene = scenes[1], position = new Vector3(6.3f, 0f, 3.5f), category = "kitchen"};
            locs[5] = new Location{ idx = 6, scene = scenes[1], position = new Vector3(-1.3f, 0f, 7.2f), category = "bathroom"};
            locs[6] = new Location{ idx = 7, scene = scenes[2], position = new Vector3(2.3f, 0f, 0.5f), category = "living"};
            locs[7] = new Location{ idx = 8, scene = scenes[2], position = new Vector3(11.2f, 0f, -1.65f), category = "bedroom"};
            locs[8] = new Location{ idx = 9, scene = scenes[2], position = new Vector3(-4.6f, 0f, -1.5f), category = "kitchen"};
            locs[9] = new Location{ idx = 10, scene = scenes[2], position = new Vector3(-8.0f, 0f, -2.0f), category = "bathroom"};
            locs[10] = new Location{ idx = 11, scene = scenes[3], position = new Vector3(-12.0f, 0.97f, -5.7f), category = "living"};
            locs[11] = new Location{ idx = 12, scene = scenes[3], position = new Vector3(-15.7f, 0.97f, 0.9f), category = "kitchen"};
            locs[12] = new Location{ idx = 13, scene = scenes[3], position = new Vector3(1.6f, 3.05f, -3.2f), category = "bedroom"};
            return locs;
        }
        
        // Serialized field instead, for easier referencing.
        // public GameObject[] InitScenes()
        // {
        //     var scn = new GameObject[4];
        //     scn[0] = GameObject.Find("indoor_1");
        //     scn[1] = GameObject.Find("indoor_3");
        //     scn[2] = GameObject.Find("indoor_5");
        //     scn[3] = GameObject.Find("indoor_6");
        //     return scn;
        // }
        
        public void SetLocation(Location location)
        {
            foreach (var loc in _allLocations) loc.scene.SetActive(false);
            location.scene.SetActive(true);
            player.transform.position = location.position;
            Debug.Log(String.Format( "New location: {0} (category: {1})", location.idx, location.category));
        }
        
        public void NextLocation(InputAction.CallbackContext ctx) => NextLocation();
        
        public void NextLocation()
        {
            // Cycle through locations
            var nextIdx = _currentLocation.idx + 1 % _allLocations.Length;
            _currentLocation = _allLocations[nextIdx];
            SetLocation(_currentLocation);
        }
        
        private void Awake()
        {
            _allLocations = InitStartLocations();
            _currentLocation = _allLocations[0];
            SetLocation(_currentLocation);
            SenorSummarySingletons.RegisterType(this);
        }
        


        // public void NextLocation(){
        //     _currentLocation = (Locations)((int)(_currentLocation + 1) % _nLocations);
        //     SetLocation(_currentLocation);
        // }

        // public void SetLocation(Locations location)
        // {
        //     Debug.Log("Jumping to location: "+ location);
        //     Vector3 position = new Vector3();
        //     foreach (var scene in scenes) scene.SetActive(false);
        //
        //     switch (location)
        //     {
        //         case Locations.Living1:
        //             position = new Vector3(0, 0f, 0.25f);
        //             scenes[0].SetActive(true);
        //             break;
        //         case Locations.Living2:
        //             position = new Vector3(0.4f, 0f, 3.6f);
        //             scenes[1].SetActive(true);
        //             break;
        //         case Locations.Bedroom1:
        //             position = new Vector3(6.0f, 0f, 7.2f);
        //             scenes[1].SetActive(true);
        //             break;
        //         case Locations.Bedroom2:
        //             position = new Vector3(2.5f, 0f, 6.9f);
        //             scenes[1].SetActive(true);
        //             break;
        //         case Locations.Kitchen1:
        //             position = new Vector3(6.3f, 0f, 3.5f);
        //             scenes[1].SetActive(true);
        //             break;
        //         case Locations.Bathroom1:
        //             position = new Vector3(-1.3f, 0f, 7.2f);
        //             scenes[1].SetActive(true);
        //             break;
        //         case Locations.Living3:
        //             position = new Vector3(2.3f, 0f, 0.5f);
        //             scenes[2].SetActive(true);
        //             break;
        //         case Locations.Bedroom3:
        //             position = new Vector3(11.2f, 0f, -1.65f);
        //             scenes[2].SetActive(true);
        //             break;
        //         case Locations.Kitchen2:
        //             position = new Vector3(-4.6f, 0f, -1.5f);
        //             scenes[2].SetActive(true);
        //             break;
        //         case Locations.Bathroom2:
        //             position = new Vector3(-8.0f, 0f, -2.0f);
        //             scenes[2].SetActive(true);
        //             break;
        //         case Locations.Living4:
        //             position = new Vector3(-12.0f, 0.97f, -5.7f);
        //             scenes[3].SetActive(true);
        //             break;
        //         case Locations.Kitchen3:
        //             position = new Vector3(-15.7f, 0.97f, 0.9f);
        //             scenes[3].SetActive(true);
        //             break;
        //         case Locations.Bedroom4:
        //             position = new Vector3(1.6f, 3.05f, -3.2f);
        //             scenes[3].SetActive(true);
        //             break;
        //     }
        //
        //     player.transform.position = position;
        // }
        
        //
        // public enum Locations
        // {
        //     // ArchVizPro 1 (Scene 1)
        //     Living1 = 0,
        //     
        //     // ArchVizPro 3 (Scene 2)
        //     Living2 = 1,
        //     Bedroom1 = 2,
        //     Bedroom2 = 3,
        //     Kitchen1 = 4,
        //     Bathroom1 = 5,
        //     
        //     // ArchvizPro 5 (Scene 3)
        //     Living3 = 6,
        //     Bedroom3 = 7,
        //     Kitchen2 = 8,
        //     Bathroom2 = 9,
        //     
        //     // ArchVizPro 6 (Scene 4) 
        //     Living4 = 10,
        //     Kitchen3 = 11,
        //     Bedroom4 = 12,
        // }
        // private int _nLocations = Enum.GetValues(typeof(Locations)).Length;



    }
}