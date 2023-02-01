
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace ExperimentControl
{
    public class KeyBlinking : MonoBehaviour
    {
        [SerializeField] public GameObject[] allTargetObjects;
        private GameObject _currentTargetObject;
        private bool _targetIsBlinking;
        public int numTargetObjects;


        public void ActivateTarget(int targetIdx)
        {
            DeactivateAllTargets();
            _currentTargetObject = allTargetObjects[targetIdx];
            _targetIsBlinking = true;
            StartCoroutine("Blink");
        }

        public void DeactivateAllTargets()
        {
            _targetIsBlinking = false;
            foreach (var obj in allTargetObjects) obj.SetActive(false);
        }


        IEnumerator Blink()
        {
            // Blink the _currentTargetKey
            while (_targetIsBlinking)
            {
                _currentTargetObject.SetActive(false);
                yield return new WaitForSeconds(0.2f);
                _currentTargetObject.SetActive(true);
                yield return new WaitForSeconds(0.2f);
            }
        }

        public void Awake()
        {
            numTargetObjects = allTargetObjects.Length;
            DeactivateAllTargets();
        }
    }
}