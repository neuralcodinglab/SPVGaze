using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace indoorMobility.Scripts.Utils
{
    public class GUIHandler : MonoBehaviour
    {
        [SerializeField] private AppData appData;
        [SerializeField] private GameObject ipField;
        [SerializeField] private GameObject portField;
        [SerializeField] private GameObject fwdField;
        [SerializeField] private GameObject sideField;
        [SerializeField] private GameObject boxField;
        [SerializeField] private GameObject wallField;
        [SerializeField] private GameObject trgField;
        [SerializeField] private GameObject maxStepsField;
        [SerializeField] private GameObject lightingSlider;
        [SerializeField] private GameObject lightingSliderValue;
        [SerializeField] private Text ipStatus;
        [SerializeField] private Text portStatus;
        [SerializeField] private Text seedStatus;
        [SerializeField] private Text clientStatus;


        // Store values from input field in appData
        public void ChangeIP () => appData.IpAddress = ipField.GetComponent<InputField>().text;
        public void ChangePort() => appData.Port = int.Parse(portField.GetComponent<InputField>().text);
        public void ChangeForward() => appData.ForwardStepReward = (byte) int.Parse(fwdField.GetComponent<InputField>().text);
        public void ChangeSide() => appData.SideStepReward = (byte) int.Parse(sideField.GetComponent<InputField>().text);
        public void ChangeBox() => appData.BoxBumpReward = (byte) int.Parse(boxField.GetComponent<InputField>().text);
        public void ChangeWall() => appData.WallBumpReward = (byte) int.Parse(wallField.GetComponent<InputField>().text);
        public void ChangeTarget() => appData.TargetReachedReward = (byte)int.Parse(trgField.GetComponent<InputField>().text);
        public void ChangeMaxSteps() => appData.MaxSteps = int.Parse(maxStepsField.GetComponent<InputField>().text);
        public void ChangeLightingSlider(){
            appData.LightIntensity = lightingSlider.GetComponent<Slider>().value / 10;
            lightingSliderValue.GetComponent<Text>().text = "Light Intensity: " + appData.LightIntensity;
        }

        // Start server upon clicking the button
        public void StartServer()
        {
            SceneManager.LoadScene(1);
        }

        // Close application upon clicking the quit button 
        public void QuitApplication()
        {
            Application.Quit();
            SceneManager.LoadScene(0); // only works in unity editor
        }

        // If server is running, the connection status is displayed (and updated using below function)
        public void UpdateRunningStatus()
        {
            ipStatus.text = appData.IpAddress;
            portStatus.text = appData.Port.ToString();
            seedStatus.text = appData.RandomSeed.ToString();
            clientStatus.text = appData.ClientConnected ? "Connected" : "Not connected";
        }

        public void ResetAppData()
        {
            Debug.Log("Appdata was restored to default values");
            appData.Reset();

            // Set text fields to null (so the placeholders will be visible)
            ipField.GetComponent<InputField>().text = null;
            portField.GetComponent<InputField>().text = null;
            fwdField.GetComponent<InputField>().text = null;
            sideField.GetComponent<InputField>().text = null;
            boxField.GetComponent<InputField>().text = null;
            wallField.GetComponent<InputField>().text = null;
            trgField.GetComponent<InputField>().text = null;
            maxStepsField.GetComponent<InputField>().text = null;

            // Update the placeholders according to values in appData
            Start();
        }

        public void Start()
        {
            // Update the GUI-placeholder texts with the actual settings
            ipField.GetComponent<InputField>().placeholder.GetComponent<Text>().text = appData.IpAddress;
            portField.GetComponent<InputField>().placeholder.GetComponent<Text>().text = appData.Port.ToString(); 
            fwdField.GetComponent<InputField>().placeholder.GetComponent<Text>().text = ((int)appData.ForwardStepReward).ToString();
            sideField.GetComponent<InputField>().placeholder.GetComponent<Text>().text = ((int)appData.SideStepReward).ToString();
            boxField.GetComponent<InputField>().placeholder.GetComponent<Text>().text = ((int)appData.BoxBumpReward).ToString();
            wallField.GetComponent<InputField>().placeholder.GetComponent<Text>().text = ((int)appData.WallBumpReward).ToString();
            trgField.GetComponent<InputField>().placeholder.GetComponent<Text>().text = ((int)appData.TargetReachedReward).ToString();
            maxStepsField.GetComponent<InputField>().placeholder.GetComponent<Text>().text = appData.MaxSteps.ToString();
            lightingSlider.GetComponent<Slider>().value = appData.LightIntensity * 10;
        }
    }
}