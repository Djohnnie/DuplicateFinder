using System;
using DuplicateFinder.Model;
using Microsoft.EntityFrameworkCore;

namespace DuplicateFinder.DataAccess
{
    public class DatabaseContext : DbContext
    {
        public DbSet<File> Files { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connectionString = Environment.GetEnvironmentVariable("CONNECTIONSTRING");
            if (String.IsNullOrEmpty(connectionString))
            {
                optionsBuilder.UseInMemoryDatabase($"{Guid.NewGuid()}");
            }
            else
            {
                optionsBuilder.UseSqlServer(connectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<File>(e =>
            {
                e.ToTable("FILES").HasKey(x => x.Id).ForSqlServerIsClustered(false);
                e.Property<Int32>("SysId").UseSqlServerIdentityColumn();
                e.HasIndex("SysId").ForSqlServerIsClustered();
            });
        }
    }
}