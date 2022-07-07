using System.Data.SQLite.Linq;
using System.IO;
using Microsoft.EntityFrameworkCore;
using UnityEngine;
using static UnityEngine.Application;

namespace DataHandling.EntityFramework
{
    public class DataContext : DbContext
    {
        public string DbPath;
        
        public DbSet<TrialConfig> Trials { get; set; }
        public DbSet<EngineData> EngineData { get; set; }
        public DbSet<EyeTrackerData> EyeTrackerData { get; set; }
        public DbSet<SingleEyeData> EyeDataLeft { get; set; }
        public DbSet<SingleEyeData> EyeDataRight { get; set; }
        public DbSet<SingleEyeData> EyeDataCombined { get; set; }

        public DataContext()
        {
            DbPath = Path.Join(
                persistentDataPath,
                "ExperimentDB.db"
            );
            Debug.Log($"Created context with path {DbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Set Up Keys
            modelBuilder.Entity<TrialConfig>()
                .HasKey(t =>
                new {
                    t.SubjectId, t.BlockId, t.TrialId
                });
            modelBuilder.Entity<EngineData>()
                .HasKey(e => new
                {
                    e.SubjectId, e.BlockId, e.TrialId
                });
            modelBuilder.Entity<EyeTrackerData>()
                .HasKey(e => new
                {
                    e.SubjectId, e.BlockId, e.TrialId
                });
            modelBuilder.Entity<SingleEyeData>()
                .HasKey(e => new
                {
                    e.SubjectId, e.BlockId, e.TrialId
                });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {

            optionsBuilder.UseSqlite($"Data Source={DbPath}");
        }
    }
}