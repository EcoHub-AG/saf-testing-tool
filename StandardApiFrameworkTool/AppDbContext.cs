using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace StandardApiFrameworkTool
{
    public class AppDbContext : DbContext
    {
        public DbSet<EnvironmentSetting> EnvironmentSettings { get; set; }
        public DbSet<Profile> Profiles { get; set; }
        public DbSet<PrivateKey> PrivateKeys { get; set; }
        public DbSet<SignatureKey> SignatureKeys { get; set; }
        public DbSet<TechUser> TechUsers { get; set; }
        public DbSet<PublicKeyStore> PublicKeyStores { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // SQLite database connection
            optionsBuilder.UseSqlite("Data Source=appdata.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // EnvironmentSetting configuration
            modelBuilder.Entity<EnvironmentSetting>()
                .HasKey(e => e.Id);

            modelBuilder.Entity<EnvironmentSetting>()
                .Property(e => e.EnvironmentName)
                .IsRequired()
                .HasMaxLength(100);

            modelBuilder.Entity<EnvironmentSetting>()
                .Property(e => e.CsmHost)
                .IsRequired(false);

            modelBuilder.Entity<EnvironmentSetting>()
                .Property(e => e.ServicesApiUrl)
                .IsRequired(false);

            // Profile configuration
            modelBuilder.Entity<Profile>()
                .HasKey(p => p.Id);

            modelBuilder.Entity<Profile>()
                .Property(p => p.SelectedEnvironment)
                .IsRequired(false);

            modelBuilder.Entity<Profile>()
                .Property(p => p.IdpNumber)
                .IsRequired(false);

            modelBuilder.Entity<Profile>()
                .Property(p => p.LicenseKey)
                .IsRequired(false);

            modelBuilder.Entity<Profile>()
                .Property(p => p.Password)
                .IsRequired(false);

            modelBuilder.Entity<Profile>()
                .Property(p => p.Iak)
                .IsRequired(false);

            modelBuilder.Entity<Profile>()
                .Property(p => p.OutTopic)
                .HasDefaultValue(string.Empty);

            // PrivateKey configuration
            modelBuilder.Entity<PrivateKey>()
                .HasKey(pk => pk.Id);

            modelBuilder.Entity<PrivateKey>()
                .Property(pk => pk.Key)
                .IsRequired(false);

            modelBuilder.Entity<PrivateKey>()
                .Property(pk => pk.Version)
                .IsRequired();

            modelBuilder.Entity<PrivateKey>()
                .Property(pk => pk.CreatedAt)
                .IsRequired();

            modelBuilder.Entity<PrivateKey>()
                .Property(pk => pk.IsActive)
                .IsRequired();

            // SignatureKey configuration
            modelBuilder.Entity<SignatureKey>()
                .HasKey(pk => pk.Id);

            modelBuilder.Entity<SignatureKey>()
                .Property(pk => pk.Key)
                .IsRequired(false);

            modelBuilder.Entity<SignatureKey>()
                .Property(pk => pk.Version)
                .IsRequired();

            modelBuilder.Entity<SignatureKey>()
                .Property(pk => pk.CreatedAt)
                .IsRequired();

            modelBuilder.Entity<SignatureKey>()
                .Property(pk => pk.IsActive)
                .IsRequired();

            // TechUser configuration
            modelBuilder.Entity<TechUser>()
                .HasKey(tu => tu.Id);

            modelBuilder.Entity<TechUser>()
                .Property(tu => tu.TechUserCert)
                .IsRequired(false);

            modelBuilder.Entity<TechUser>()
                .Property(tu => tu.OAuthClientId)
                .IsRequired();

            modelBuilder.Entity<TechUser>()
                .Property(tu => tu.OAuthClientPassword)
                .IsRequired();

            // PublicKeyStore configuration
            modelBuilder.Entity<PublicKeyStore>()
                .HasKey(pk => pk.Id);

            modelBuilder.Entity<PublicKeyStore>()
                .Property(pk => pk.KeyId)
                .IsRequired();

            modelBuilder.Entity<PublicKeyStore>()
                .HasIndex(pk => pk.KeyId)
                .IsUnique();

            modelBuilder.Entity<PublicKeyStore>()
                .Property(pk => pk.MembershipId)
                .IsRequired();

            modelBuilder.Entity<PublicKeyStore>()
                .Property(pk => pk.Version)
                .IsRequired();

            modelBuilder.Entity<PublicKeyStore>()
                .Property(pk => pk.Key)
                .IsRequired();

            modelBuilder.Entity<PublicKeyStore>()
                .Property(pk => pk.CreatedAt)
                .IsRequired();

            modelBuilder.Entity<PublicKeyStore>()
                .Property(pk => pk.LastUpdatedAt)
                .IsRequired();

            modelBuilder.Entity<PublicKeyStore>()
                .Property(pk => pk.ActivatedAt)
                .IsRequired();

            modelBuilder.Entity<PublicKeyStore>()
                .Property(pk => pk.ExpiryDate)
                .IsRequired();

            modelBuilder.Entity<PublicKeyStore>()
                .Property(pk => pk.EcoHubStatus)
                .IsRequired();

            modelBuilder.Entity<PublicKeyStore>()
                .Property(pk => pk.IdpNumber)
                .IsRequired();
        }
    }

    public class EnvironmentSetting
    {
        public int Id { get; set; }
        public string EnvironmentName { get; set; }
        public string CsmHost { get; set; }
        public string ServicesApiUrl { get; set; }
    }

    public class Profile
    {
        public int Id { get; set; }
        public string SelectedEnvironment { get; set; }
        public string IdpNumber { get; set; }
        public string LicenseKey { get; set; }
        public string Password { get; set; }
        public string Iak { get; set; }
        public string OutTopic { get; set; }
    }

    public class PrivateKey
    {
        public int Id { get; set; }
        public string Key { get; set; }
        public string Version { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SignatureKey
    {
        public int Id { get; set; }
        public string Key { get; set; }
        public string Version { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class TechUser
    {
        public int Id { get; set; }
        public string TechUserCert { get; set; }
        public string OAuthClientId { get; set; }
        public string OAuthClientPassword { get; set; }
    }

    public class PublicKeyStore
    {
        public int Id { get; set; }
        public string KeyId { get; set; }
        public Guid MembershipId { get; set; }
        public string Version { get; set; }
        public string Key { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
        public DateTime ActivatedAt { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string EcoHubStatus { get; set; }
        public string IdpNumber { get; set; }
    }
}
