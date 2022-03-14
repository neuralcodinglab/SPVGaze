using UnityEngine;
using System.IO;
using System.Collections.Generic;


namespace Xarphos.Scripts
{


  public class PhospheneConfiguration
  {   // Data class containing the phosphene configuration (count, locations, sizes)
      // instances can be directly deseriallized from a JSON file using the 'load' method

      public string description = "PHOSPHENE SPECIFICATIONS FILE.  'phospheneCount': the number of phosphenes. 'specifications': list of phosphenes, with their screen coordinates 'x','y' (range 0 to 1).  'z' indicates phosphene size (sigma), and 'w' is unused at the moment.";
      public int phospheneCount;
      public Vector4[] specifications;

      public static PhospheneConfiguration load(string filename)
      {
        string json = System.IO.File.ReadAllText(filename);
        return JsonUtility.FromJson<PhospheneConfiguration>(json);
      }

      public void save(string filename)
      {
        string json = JsonUtility.ToJson(this);
        File.WriteAllText(filename, json);
        Debug.Log("Saved phosphene configuration to " + filename);
      }
  }
}
