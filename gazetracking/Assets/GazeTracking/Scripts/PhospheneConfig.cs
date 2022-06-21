using UnityEngine;
using System.IO;


namespace Xarphos.Scripts
{

  public struct Phosphene
  {
    public Vector2 position;
    public float size;
    public Vector2 activation;
    public Vector2 trace;
  }

  public class PhospheneConfig
  {   // Data class containing the phosphene configuration (count, locations, sizes)
      // instances can be directly deseriallized from a JSON file using the 'load' method

      public string description = "PHOSPHENE SPECIFICATIONS FILE.  'nPhosphenes': the number of phosphenes. 'eccentricities': the radius (in degrees of visual angle) from the foveal center for each phosphene. 'azimuth_angles': the polar angle (in radians) for each phosphene. 'size': each phosphene's default size (in dva). ";
      public int nPhosphenes;
      public float[] eccentricities;
      public float[] azimuth_angles;
      public float[] sizes;

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

      public static Phosphene[] InitPhosphenesFromJSON(string filename)
      {
        // Initializes a struct-array with all phosphenes. Note that this struct
        // array (Phosphene) contains more properties than only position and size
        PhospheneConfig config = PhospheneConfig.load(filename);
        Debug.Log(config.nPhosphenes);
        Debug.Log(config.description);
        Phosphene[] phosphenes = new Phosphene[config.nPhosphenes];
        for (int i=0; i<config.nPhosphenes; i++)
        {
          // Vive Eye Pro covers 110 degrees visual field @ a resolution of 1440x1600 pixels per eye
          // since screen is slightly smaller than actual FOV, rescale to 110 degree FOV to get relative size of screen
          // this is imperfect but should be a rough estimation of actual placement
          // ----
          // calculate carthesian coordinates of phosphene in [0,1] ; eccentricities have fovea at 0,0
          // so move coordinates by 60 to and scale by 120 to get relative position
          var x = (config.eccentricities[i] / 2f * Mathf.Cos(config.azimuth_angles[i]) + 60f) / 120f;
          var y = (config.eccentricities[i] / 2f * Mathf.Sin(config.azimuth_angles[i]) + 60f) / 120f;
          var pos = new Vector2(x,y);
          phosphenes[i].position = pos;
          // scale size from dav to fraction of fov and to radius
          phosphenes[i].size = config.sizes[i] / 2f / 120f;
        }
        return phosphenes;
      }

  }
}
