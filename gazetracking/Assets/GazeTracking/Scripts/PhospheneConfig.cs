using System;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Text;
using MathNet.Numerics;
using MathNet.Numerics.Random;


namespace Xarphos.Scripts
{

  public struct Phosphene
  {
    public Vector2 position;
    public float size;
    public Vector2 activation; // activation pro phosphene is eye dependent as stimulation is different (texture offset)
    public Vector2 trace; // same reason as activation
  }
  
  public struct CortexModelParameters
  {
    public bool dipoleModel;
    public float a;
    public float b;
    public float k;
  }

  public class PhospheneConfig
  {   // Data class containing the phosphene configuration (count, locations, sizes)
      // instances can be directly deseriallized from a JSON file using the 'load' method

      public Phosphene[] phosphenes;
      public string description = "PHOSPHENE SPECIFICATIONS.  'nPhosphenes': the number of phosphenes. 'eccentricities': the radius (in degrees of visual angle) from the foveal center for each phosphene. 'azimuth_angles': the polar angle (in radians) for each phosphene. 'size': each phosphene's default size (in dva). ";
      public int nPhosphenes;
      public float[] eccentricities;
      public float[] azimuth_angles;
      public float[] sizes;

      // approximation of human field of vision
      public const int TotalFOV = 120;
      // generally values are found to be: k = 15~17, a ~ .7, b = 120~180 
      // according to Horten&Hoyt (ToDo: Double check if ref is correc)
      public static CortexModelParameters Monopole = new() { dipoleModel = false, a = .75f, k = 17.3f };
      // following Wu et al.
      public static CortexModelParameters Dipole = new() { dipoleModel = true, k = 20.13f, a = 1.717f, b = 7.5e5f };
      // maureen's unknown paper ref
      public static CortexModelParameters Dipole2 = new() { dipoleModel = true, k = 15, a = .7f, b = 120 };
      
      public static PhospheneConfig load(string filename)
      {
        string json = System.IO.File.ReadAllText(filename);
        return JsonUtility.FromJson<PhospheneConfig>(json);
      }

      public void save(string filename)
      {
        string json = JsonUtility.ToJson(this);
        File.WriteAllText(filename, json);
        Debug.Log("Saved phosphene configuration to " + filename);
      }

      public static PhospheneConfig InitPhosphenesFromJSON(string filename)
      {
        // Initializes a struct-array with all phosphenes. Note that this struct
        // array (Phosphene) contains more properties than only position and size
        PhospheneConfig config = load(filename);
        Debug.Log(config.nPhosphenes);
        Debug.Log(config.description);
        config.phosphenes = new Phosphene[config.nPhosphenes];
        for (int i=0; i<config.nPhosphenes; i++)
        {
          // Vive Eye Pro covers 110 degrees visual field @ a resolution of 1440x1600 pixels per eye
          // since screen is slightly smaller than actual FOV, rescale to 110 degree FOV to get relative size of screen
          // this is imperfect but should be a rough estimation of actual placement
          // ----
          // calculate cartesian coordinates of phosphene in [0,1] ; eccentricities have fovea at 0,0
          // so move coordinates by 60 to and scale by 120 to get relative position
          var x = (config.eccentricities[i] / 2f * Mathf.Cos(config.azimuth_angles[i]) + 60f) / TotalFOV;
          var y = (config.eccentricities[i] / 2f * Mathf.Sin(config.azimuth_angles[i]) + 60f) / TotalFOV;
          var pos = new Vector2(x,y);
          config.phosphenes[i].position = pos;
          // scale size from dav to fraction of fov
          config.phosphenes[i].size = config.sizes[i] / (2f * TotalFOV);
        }
        return config;
      }

      /// <summary>
      /// Initialises a phosphene configuration probabilistically with approximations from the literature
      /// </summary>
      /// <param name="nPhosphenes">The number of phosphenes to generate across the entire FOV</param>
      /// <param name="maxEccentricity">Max radius of phosphenes in fraction of total fov [0,1)</param>
      /// <param name="parameters">Which cortex model to use for magnification factor along with the parameters</param>
      /// <returns></returns>
      public static PhospheneConfig InitPhosphenesProbabilistically(int nPhosphenes, float maxEccentricity, CortexModelParameters parameters)
      {
        var config = new PhospheneConfig
        {
          nPhosphenes = nPhosphenes,
          eccentricities = new float[nPhosphenes],
          azimuth_angles = new float[nPhosphenes],
          sizes = new float[nPhosphenes],
          phosphenes = new Phosphene[nPhosphenes]
        };

        // cortical magnification function depends on cortex model
        Func<double[], double[]> eccentricityScaling = parameters.dipoleModel
          ? radii => radii.Select(r => parameters.k * (1 / (r + parameters.a) - 1 / (r + parameters.b))).ToArray()
          : radii => radii.Select(r => parameters.k / (r + parameters.a)).ToArray();

        // create a probability distribution according to cortical magnification and literature approximations
        // custom probability distribution that goes from near-0 to max radius and is higher close to the fovea
        // since continuous distributions are a pain, we approximate with a binned discrete variant.
        var validEcc = Generate.LinearSpaced(10000, 1e-3, maxEccentricity * TotalFOV);
        
        // Thank you, EvK: https://stackoverflow.com/questions/43303538/python-numpy-random-choice-in-c-sharp-with-non-uniform-probability-distribution
        // apply cortical magnification weights (closer to fovea have higher weights, peripheral is less)
        var weights = eccentricityScaling(validEcc);
        var sum = weights.Sum();
        var cumSum = 0.0;
        // we use cumulative probabilities, because those allow us to transform a uniform variable to our custom distribution
        var cumProbs = weights.Select(w =>
        {
          var scaled = w / sum;
          var cp = cumSum + scaled;
          cumSum += scaled;
          return cp;
        }).ToArray();

        // generate a uniform variable in [0,1) for each phosphene to determine eccentricity & angle from
        var randomVals = SystemRandomSource.Doubles(nPhosphenes * 2, BitConverter.ToInt32(Encoding.UTF8.GetBytes("penis"), 0));
          
        for (int i =0; i<nPhosphenes; i++)
        {
          var p = new Phosphene();
          // transform uniform variable into scaled by checking in which cumulative bin it falls
          var idx = Array.BinarySearch(cumProbs, randomVals[i]);
            
          // if exact match is not found, Array.BinarySearch will return index of the first items greater than
          // passed value, but as a complement of the index of the item
          // Negating the complement with ~ gets the real index
          if (idx < 0)
            idx = ~idx; 
          // very rare case when probabilities do not sum to 1 because of double precision issues (so sum is 0.999943 and so on)
          if (idx > cumProbs.Length - 1)
            idx = cumProbs.Length - 1;

          var ecc = validEcc[idx];
          config.eccentricities[i] = (float)ecc;
          var theta = Math.PI * 2 * randomVals[idx + nPhosphenes];
          
          // calculate cartesian coordinates of phosphene in [0,1] ; eccentricities have fovea at 0,0
          // so move coordinates by 60 to and scale by 120 to get relative position
          // use randomly generated polar coordinates to get x-y coordinates and scale to fraction of fov
          // also cast down to float for unity compatibility
          var x = (float)((ecc * Math.Cos(theta) + 60.0) / TotalFOV);
          var y = (float)((ecc * Math.Sin(theta) + 60.0) / TotalFOV);

          p.position = new Vector2(x, y);

          // eccentricity scaling function is in activated cortex area per degrees of visual angle ([mm^2]/[degrees])
          // taking the inverse to get visual angle per cortex area activation
          var magnification = 1.0 / eccentricityScaling(new double[] { ecc })[0];
          // approximating cortex activation with an average stimulation strength to calculate size
          double avgStim = 30; // micro ampere, stimulation can range from 20-60 μA
          // according to Tehovnik, 2007, Depth-dependent detection of microampere currents delivered to monkey V1
          double apprxSpreadRadius = Math.Sqrt(avgStim / 675f); // K is current distance constant in μA/mm2
          // multiply apprx area with magnification factor of eccentricity to get diameter of phosphene in dVA
          // divide by 2 and 120 to get radius as fraction of total fov
          p.size = (float)(magnification * apprxSpreadRadius) / (2 * TotalFOV);

          config.phosphenes[i] = p;
        }
        return config;
      }
  }
}
