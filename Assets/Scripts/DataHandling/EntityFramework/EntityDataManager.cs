using System.Threading.Tasks;

namespace DataHandling.EntityFramework
{
    public class EntityDataManager
    {
        private DataContext db;
        public TrialConfig CurrentTrial { get; private set;  }

        public void StartNewTrial(TrialConfig cfg)
        {
            CurrentTrial = cfg;
            db = new DataContext();
            db.Add(cfg);
            db.SaveChanges();
        }
    }
}