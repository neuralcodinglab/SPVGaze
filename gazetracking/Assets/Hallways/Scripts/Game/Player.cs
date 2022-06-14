using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using indoorMobility.Scripts.Utils;

namespace indoorMobility.Scripts.Game
{
    public class Player : MonoBehaviour
    {
        //[SerializeField] private AppData appData;
        private AppData appData;
        private Environment environment;
        private Camera camera;

        private int _forwardStepCount;
        private string _collidedWith;
        private int _maxForwardSteps;

        public int ForwardStepCount { get => _forwardStepCount;}
        public string CollidedWith { get => _collidedWith;}
   

        private void OnCollisionEnter(Collision collision) 
        {  //Test for collisions (automatic Unity process, runs at fixed update)
            _collidedWith = collision.gameObject.name;
            environment.Reward = appData.BoxBumpReward;
            environment.End    = 1;

            // LOG
            Debug.Log("player hit");
            Debug.Log(_collidedWith);
        }


        public void Move(int action)
        {
            environment.End = 0;
            _collidedWith = "";
            Vector3 currentPos = transform.position;
            
            switch (action)
            {
                case 0: // forward
                    {
                        transform.position = currentPos + new Vector3(0f, 0f, appData.ForwardSpeed);
                        environment.Reward = appData.ForwardStepReward;
                        _forwardStepCount++;

                        if (_forwardStepCount >= _maxForwardSteps) // TODO Is this necessary? Or do it in Python?
                        {
                            environment.Reward = appData.TargetReachedReward;
                            environment.End = 3;
                            _forwardStepCount = 0;
                        }
                        break;
                    }

                case 1: // agent wants to go left
                    {
                        if (currentPos.x == -appData.SideStepDistance)
                        {
                            environment.Reward = appData.WallBumpReward;
                            environment.End = 2;
                        }
                        else
                        {
                            environment.Reward = appData.SideStepReward;
                            transform.position = currentPos + new Vector3(-appData.SideStepDistance, 0f, 0f);
                        }
                        break;
                    }

                case 2: // agent wants to go right
                    {
                        if (currentPos.x == appData.SideStepDistance)
                        {
                            environment.Reward = appData.WallBumpReward;
                            environment.End = 2;
                        }
                        else
                        {
                            environment.Reward = appData.SideStepReward;
                            transform.position = currentPos + new Vector3(appData.SideStepDistance, 0f, 0f);
                        }
                        break;
                    }
                default:
                    break; // No action 
            }

        }

        private void SetRandomCamRotation()
        {
            float xRot = Random.Range(-appData.CamRotJitter, appData.CamRotJitter);
            float yRot = Random.Range(-appData.CamRotJitter, appData.CamRotJitter);
            float zRot = Random.Range(-appData.CamRotJitter, appData.CamRotJitter);
            camera.transform.rotation = Quaternion.Euler(xRot, yRot, zRot);
        }


        private void Start()
        {

            environment = GameObject.Find("Environment").GetComponent<Environment>();
            appData = GameObject.Find("GameManager").GetComponent<GameManager>().appData;
            camera = Camera.main;
            Reset(0); //JR 11-10-'21: was Reset(2); but don't know why
        }

        public void Reset(int action)
        {
            transform.position = new Vector3(0f, 1.1f, 0f);
            SetRandomCamRotation();
            _collidedWith = "";
            _forwardStepCount = 0;
            if (action == 2 || action == 3) // Test condition
            {
                camera.transform.rotation = Quaternion.Euler(0, 0, 0);
                _maxForwardSteps = 560; // TODO: Hardcoded for now (14 test hallways * 10 pieces * 4 steps)
            }
            else
            {
                _maxForwardSteps = appData.MaxSteps;
            }
        }

    }
}