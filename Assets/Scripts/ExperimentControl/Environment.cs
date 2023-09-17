using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ExperimentControl
{
    public class Environment : MonoBehaviour
    {
        [SerializeField] public bool practiceEnv;
        public string Name;

        public enum RoomCategory
        {
            None = 0,
            Living = 1,
            Bedroom = 2,
            Bathroom = 3,
            Kitchen = 4,
        }

        public GameObject scene; //  The 3D environment (gameobject) 
        public RoomCategory roomCategory; //  the category of the room
        public GameObject playerStartLocation; // The spawn location for the player
        public TargetObject[] targetObjects; // The spawn locations where the targets can appear 
        [NonSerialized] public string ActiveTargetName = "None";
        public void DeactivateScene() => scene.SetActive(false);
        public void ActivateScene() => scene.SetActive(true);
        
    }
}