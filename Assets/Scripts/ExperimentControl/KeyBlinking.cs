
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class KeyBlinking : MonoBehaviour
{
    [SerializeField] GameObject[] _allTargetKeys;
    public GameObject _currentTargetKey;

    public bool blinkingActive;
    
    
    
    
    // Start is called before the first frame update
    void Start()
    {
        _currentTargetKey = _allTargetKeys[0];
        StartBlinking();
    }

    public void StartBlinking()
    {
        blinkingActive = true;
        StartCoroutine("Blink");
    }
    
    public void StopBlinking()
    {
        blinkingActive = false;
    }

    IEnumerator Blink()
    {
        // Disable all targets
        foreach (var obj in _allTargetKeys) obj.SetActive(false);

        // Blink the _currentTargetKey
        while (blinkingActive)
        {
            _currentTargetKey.SetActive(false);
            yield return new WaitForSeconds(0.2f);
            _currentTargetKey.SetActive(true);
            yield return new WaitForSeconds(0.2f);
        }

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
