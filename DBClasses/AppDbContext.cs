using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.EntityFrameworkCore;
using AgentSystem.Classes;

namespace AgentSystem.DBClasses
{
    public class AppDbContext : DbContext 
    {
        public DbSet<StationData> ListStationData { get; set; }
        //static object lock_db = new object();
        private string[] colName;

        public AppDbContext(DbContextOptions options, string[] colname): base(options)
        {
            colName = colname;          //получение наименований столбцов = названиям датчиков в строке
            try
            {
                Database.EnsureCreated();
            }
            catch (Exception e)
            {
                SystemTools.WriteLog($"Ошибка при открытии базы данных: {e.Message}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StationData>().Property(u => u.Fid00).HasColumnName(colName[0]);
            modelBuilder.Entity<StationData>().Property(u => u.Fid01).HasColumnName(colName[1]);
            modelBuilder.Entity<StationData>().Property(u => u.Fid02).HasColumnName(colName[2]);
            modelBuilder.Entity<StationData>().Property(u => u.Fid03).HasColumnName(colName[3]);
            modelBuilder.Entity<StationData>().Property(u => u.Fid04).HasColumnName(colName[4]);
            modelBuilder.Entity<StationData>().Property(u => u.Fid05).HasColumnName(colName[5]);
            modelBuilder.Entity<StationData>().Property(u => u.Fid06).HasColumnName(colName[6]);
            modelBuilder.Entity<StationData>().Property(u => u.Fid07).HasColumnName(colName[7]);
            modelBuilder.Entity<StationData>().Property(u => u.Fid08).HasColumnName(colName[8]);
            modelBuilder.Entity<StationData>().Property(u => u.Fid09).HasColumnName(colName[9]);
        }

        /*
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=agentsystemdb;Trusted_Connection=True;");
        }
        */

    }
}
