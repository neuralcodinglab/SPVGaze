using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using ImgSynthesis = indoorMobility.Scripts.ImageSynthesis.ImgSynthesis;
using indoorMobility.Scripts.Utils;

namespace indoorMobility.Scripts.Game
{
    public class Environment : MonoBehaviour {
        #region;

        // Flag for using user-specified seed for RNG (instead of random time-based allocation)
        private bool _manualSeed;

        // Game settings 
        private AppData appData;

        // Children environment GameObjects and corresponding scripts
        [SerializeField] private GameObject Hallway;
        [SerializeField] private GameObject Player;
        private Player player;
        private Hallway hallway;

        // Image processing variables
        private Camera _camera;
        private List<Color32[]> _state;
        private RenderTexture _targetTexture;
        private ImgSynthesis imgSynthesis; // Img processing script attached to the camera

        // Input/Output variables
        private int _action;
        private byte[] _data;
        private byte _end, _reward;

        // Can be accessed by player script or game manager
        public byte Reward { set => _reward = value;}
        public byte End { set => _end = value;}

        public byte Input { set => _action = value; }

        public byte[] Output { //output to python: 1 byte to determine if the loop ended, 1 byte for the reward and 16 x w x h bytes with the camera view of the agent (state)
            get {
                _data[0] = _end;
                _data[1] = _reward;

                // Render the state (for the different render types: colors, semantic segmentation, depth, etc.)
                var tex = new Texture2D(appData.Width, appData.Height);
                _state = new List<Color32[]>();
                for(var idx = 0; idx<=5; idx++)
                {
                    // Get hidden camera 
                    var cam = ImgSynthesis.capturePasses[idx].camera;

                    // Render
                    RenderTexture.active = _targetTexture; //renderRT;
                    cam.targetTexture = _targetTexture; // renderRT;
                    cam.Render();
                    tex.ReadPixels(new Rect(0, 0, _targetTexture.width, _targetTexture.height), 0, 0);
                    tex.Apply();
                    _state.Add(tex.GetPixels32());
                }
                Object.Destroy(tex);

                // Color32 arrays for each of the render types:
                var colors  = _state.ElementAt(0);
                var objseg  = _state.ElementAt(1);
                var semseg  = _state.ElementAt(2);
                var depth   = _state.ElementAt(3);
                var normals = _state.ElementAt(4);
                var flow    = _state.ElementAt(5);
                
                // Write state to _data
                for (var y = 0;
                    y < appData.Height;
                    y++)
                    for (var x = 0;
                        x < appData.Width;
                        x++) {
                        var i = 16 * (x - y * appData.Width + (appData.Height - 1) * appData.Width);
                        var j = 1 * (x + y * appData.Width);
                        _data[i + 2]  = colors[j].r;
                        _data[i + 3]  = colors[j].g;
                        _data[i + 4]  = colors[j].b;
                        _data[i + 5]  = objseg[j].r;
                        _data[i + 6]  = objseg[j].g;
                        _data[i + 7]  = objseg[j].b;
                        _data[i + 8]  = semseg[j].r;
                        _data[i + 9]  = semseg[j].g;
                        _data[i + 10] = semseg[j].b;
                        _data[i + 11] = normals[j].r;
                        _data[i + 12] = normals[j].g;
                        _data[i + 13] = normals[j].b;
                        _data[i + 14] = flow[j].r;
                        _data[i + 15] = flow[j].g;
                        _data[i + 16] = flow[j].b;
                        _data[i + 17] = depth[j].r;

                    }

                return _data;
            }
        }

        #endregion;

        #region;

        public void Reset() 
        { 
           // Generate new (random) RNG seed (unless manual seed was specified) 
           appData.RandomSeed = _manualSeed ? appData.RandomSeed : (int)System.DateTime.Now.Ticks;
           Random.InitState(appData.RandomSeed);
           _manualSeed = false; // pick a random seed at next reset
           
           // Reset hallway and agent
           hallway.Reset(_action);
           player.Reset(_action);

           // Reset image processing script
           imgSynthesis.OnSceneChange();

        }



        public void Step()
        {   //Move the player (environment.Reward and environment.End are updated by player)
            player.Move(_action);

            // Generate new hallway pieces as player moves towards finish
            if (hallway.EndPosition - player.transform.position.z <= 2*appData.VisibleHallwayPieces -6)
                hallway.updateHallway();
        }

        public void SetManualSeed()
        {
            // set manual RNG seed (becomes effective at Reset())
            _manualSeed = true;
            appData.RandomSeed = _action;
        }

        private void Start() {
            
            // All Game settings are stored in appData
            appData = GameObject.Find("GameManager").GetComponent<GameManager>().appData;
            
            // Scripts of the children GameObjects that constitute the environment
            player = Player.GetComponent<Player>();
            hallway = Hallway.GetComponent<Hallway>();
            
            // Initialize camera
            _camera = Camera.main;
            (_camera.targetTexture = new RenderTexture(appData.Width, appData.Height, 0)).Create();
            _targetTexture = Camera.main.targetTexture;
            imgSynthesis = _camera.GetComponent<ImgSynthesis>();
            
            // Output data
            _data = new byte[2 + 16 * appData.Width * appData.Height];
        }

        #endregion;

   
        
    }
}