using Microsoft.EntityFrameworkCore;
using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Section> Sections { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<AttendanceEditLog> AttendanceEditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Role with table name
            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("AppRoles");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Description).HasMaxLength(200);
                entity.HasIndex(e => e.Name).IsUnique();
            });

            // Configure Section with table name
            modelBuilder.Entity<Section>(entity =>
            {
                entity.ToTable("AppSections");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
            });

            // Configure User with table name
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Phone).HasColumnName("Phone").HasMaxLength(20);
                entity.Property(e => e.Address).HasMaxLength(200);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.HasIndex(e => e.Email).IsUnique();

                entity.HasOne(e => e.Role)
                    .WithMany(r => r.Users)
                    .HasForeignKey(e => e.RoleId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.Section)
                    .WithMany(s => s.Users)
                    .HasForeignKey(e => e.SectionId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Configure Attendance with table name
            modelBuilder.Entity<Attendance>(entity =>
            {
                entity.ToTable("AppAttendances");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
                entity.Property(e => e.IsLocked).HasDefaultValue(false);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.Attendances)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Ensure unique attendance record per user per day
                entity.HasIndex(e => new { e.UserId, e.AttendanceDate }).IsUnique();
            });

            // Configure AttendanceEditLog with table name
            modelBuilder.Entity<AttendanceEditLog>(entity =>
            {
                entity.ToTable("AttendanceEditLogs");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.EditReason).HasMaxLength(1000);

                entity.HasOne(e => e.Attendance)
                    .WithMany(a => a.EditLogs)
                    .HasForeignKey(e => e.AttendanceId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.EditedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.EditedByUserId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // Seed initial data
            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            // Seed Roles
            var roles = new[]
            {
                new Role { Id = 1, Name = RoleNames.SuperAdmin, Description = "Super Administrator with full system access", CreatedAt = DateTime.UtcNow },
                new Role { Id = 2, Name = RoleNames.Admin, Description = "Section Administrator with limited access", CreatedAt = DateTime.UtcNow },
                new Role { Id = 3, Name = RoleNames.Worker, Description = "Regular worker who can mark attendance", CreatedAt = DateTime.UtcNow }
            };

            modelBuilder.Entity<Role>().HasData(roles);

            // Seed Sections
            var sections = new[]
            {
                new Section { Id = 1, Name = "Information Technology", Description = "IT Department", CreatedAt = DateTime.UtcNow, IsActive = true },
                new Section { Id = 2, Name = "Human Resources", Description = "HR Department", CreatedAt = DateTime.UtcNow, IsActive = true },
                new Section { Id = 3, Name = "Finance", Description = "Finance and Accounting", CreatedAt = DateTime.UtcNow, IsActive = true },
                new Section { Id = 4, Name = "Marketing", Description = "Marketing and Sales", CreatedAt = DateTime.UtcNow, IsActive = true },
                new Section { Id = 5, Name = "Operations", Description = "Operations Department", CreatedAt = DateTime.UtcNow, IsActive = true },
                new Section { Id = 6, Name = "Quality Assurance", Description = "QA Department", CreatedAt = DateTime.UtcNow, IsActive = true },
                new Section { Id = 7, Name = "Research & Development", Description = "R&D Department", CreatedAt = DateTime.UtcNow, IsActive = true },
                new Section { Id = 8, Name = "Customer Support", Description = "Customer Service", CreatedAt = DateTime.UtcNow, IsActive = true },
                new Section { Id = 9, Name = "Administration", Description = "General Administration", CreatedAt = DateTime.UtcNow, IsActive = true },
                new Section { Id = 10, Name = "Production", Description = "Production Department", CreatedAt = DateTime.UtcNow, IsActive = true }
            };

            modelBuilder.Entity<Section>().HasData(sections);

            // Seed Super Admin
            var superAdmin = new User
            {
                Id = 1,
                FirstName = "Super",
                LastName = "Admin",
                Email = "superadmin@attendance.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                Phone = "1234567890",
                RoleId = 1, // Super Admin
                SectionId = null,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            modelBuilder.Entity<User>().HasData(superAdmin);
        }
    }
}
