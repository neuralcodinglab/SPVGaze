using UnityEngine;

namespace indoorMobility.Scripts.Utils
{
    [CreateAssetMenu(menuName = "IndoorMobility/AppData")]
    public class AppData : ScriptableObject
    {
        // Game data
        #region
        [SerializeField] private float _timescale;  // time scale for physics update (default: 1)
        [SerializeField] private int _width;        // image width (for state)
        [SerializeField] private int _height;       // image height (for state)

        public float TimeScale { get => _timescale; set => _timescale = value; }
        public int Width {get => _width; set => _width = value;}
        public int Height {  get => _height; set => _height = value; }
        #endregion

        // Server data 
        #region
        [SerializeField] private string _ipAddress;     // Server address
        [SerializeField] private int _port;             // Server port
        [SerializeField] private bool _clientConnected; // Flag for established connection with client
        
        public string IpAddress {get => _ipAddress; set => _ipAddress = value;}
        public int Port {get => _port; set => _port = value;}
        public bool ClientConnected {get => _clientConnected; set => _clientConnected = value;}
        #endregion

        // Rewards
        #region
        [SerializeField] private byte _forwardStepReward;   // Reward for (succesful) forward step
        [SerializeField] private byte _sideStepReward;      // (succesful) left- or rightward step
        [SerializeField] private byte _boxBumpReward;       // collision with box obstacle
        [SerializeField] private byte _wallBumpReward;      // collision with the wall
        [SerializeField] private byte _targetReachedReward; // reward for (every) forwardStepCount >= maxSteps 

        public byte ForwardStepReward{get => _forwardStepReward; set => _forwardStepReward = value;}
        public byte SideStepReward{get => _sideStepReward; set => _sideStepReward = value;}
        public byte BoxBumpReward{get => _boxBumpReward; set => _boxBumpReward = value;}
        public byte WallBumpReward{get => _wallBumpReward; set => _wallBumpReward = value;}
        public byte TargetReachedReward{get => _targetReachedReward; set => _targetReachedReward = value;}
        #endregion

        // Environment data
        #region
        [SerializeField] private int _randomSeed;           // random number generator (RNG) seed (default: randomly allocated at reset)
        [SerializeField] private int _maxSteps;             // target no. forward steps (hallway 'finish'). At test run (fixed hallway length) it is set at 560 
        [SerializeField] private float _forwardSpeed;       // forward displacement (z-direction) for each step
        [SerializeField] private float _sideStepDistance;   // sideways displacement
        [SerializeField] private float _camRotJitter;       // range for random camera rotations (training only)
        [SerializeField] private int _visibleHallwayPieces; // length of (constantly updated) hallway
        [SerializeField] private float _lightIntensity;     // Scaling factor to adjust the light intensity

        public int RandomSeed{get => _randomSeed; set => _randomSeed = value;}
        public int MaxSteps { get => _maxSteps; set => _maxSteps = value; }
        public float ForwardSpeed{get => _forwardSpeed; set => _forwardSpeed = value;}
        public float SideStepDistance{get => _sideStepDistance;set => _sideStepDistance = value;}
        public float CamRotJitter {get => _camRotJitter; set => _camRotJitter = value;}
        public int VisibleHallwayPieces {get => _visibleHallwayPieces;set => _visibleHallwayPieces = value;}
        public float LightIntensity { get => _lightIntensity; set =>_lightIntensity = value;}
        #endregion



        public void Reset()
        {
            // Game Manager
            _timescale = 1;
            _width = 128;
            _height = 128;

            // Server
            _ipAddress = "127.0.0.1";
            _port =  13000;
            _clientConnected = false;

            // Rewards
            _forwardStepReward = (byte)10;
            _sideStepReward = (byte)101;
            _boxBumpReward = (byte)120;
            _wallBumpReward = (byte)115;
            _targetReachedReward = (byte)10;

            // Random seed (for different hallway variations, random camera rotations)
            _randomSeed = 0;

            // Hallway data
            _visibleHallwayPieces = 20;
            _lightIntensity = 1.0f;

            // Player data
            _forwardSpeed = 0.5f;
            _sideStepDistance = 0.95f;
            _maxSteps = 100;
            _camRotJitter = 3.0f;
        }
    }
}