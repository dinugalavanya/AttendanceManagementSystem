using Microsoft.EntityFrameworkCore;
using AttendanceManagementSystem.Models;

namespace AttendanceManagementSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        private static readonly DateTime SeedRoleCreatedAt = new DateTime(2026, 5, 13, 5, 55, 3, 354, DateTimeKind.Utc).AddTicks(4489);
        private static readonly DateTime SeedRoleCreatedAt2 = new DateTime(2026, 5, 13, 5, 55, 3, 354, DateTimeKind.Utc).AddTicks(4650);
        private static readonly DateTime SeedRoleCreatedAt3 = new DateTime(2026, 5, 13, 5, 55, 3, 354, DateTimeKind.Utc).AddTicks(4651);
        private static readonly DateTime SeedSectionCreatedAt0 = new DateTime(2026, 5, 13, 5, 55, 3, 355, DateTimeKind.Utc).AddTicks(390);
        private static readonly DateTime SeedSectionCreatedAt1 = new DateTime(2026, 5, 13, 5, 55, 3, 355, DateTimeKind.Utc).AddTicks(391);
        private static readonly DateTime SeedSectionCreatedAt2 = new DateTime(2026, 5, 13, 5, 55, 3, 355, DateTimeKind.Utc).AddTicks(665);
        private static readonly DateTime SeedSectionCreatedAt3 = new DateTime(2026, 5, 13, 5, 55, 3, 355, DateTimeKind.Utc).AddTicks(666);
        private static readonly DateTime SeedSectionCreatedAt4 = new DateTime(2026, 5, 13, 5, 55, 3, 355, DateTimeKind.Utc).AddTicks(668);
        private static readonly DateTime SeedSectionCreatedAt5 = new DateTime(2026, 5, 13, 5, 55, 3, 355, DateTimeKind.Utc).AddTicks(669);
        private static readonly DateTime SeedSectionCreatedAt6 = new DateTime(2026, 5, 13, 5, 55, 3, 355, DateTimeKind.Utc).AddTicks(670);
        private static readonly DateTime SeedSectionCreatedAt7 = new DateTime(2026, 5, 13, 5, 55, 3, 355, DateTimeKind.Utc).AddTicks(671);
        private static readonly DateTime SeedSectionCreatedAt8 = new DateTime(2026, 5, 13, 5, 55, 3, 355, DateTimeKind.Utc).AddTicks(672);
        private static readonly DateTime SeedSectionCreatedAt9 = new DateTime(2026, 5, 13, 5, 55, 3, 355, DateTimeKind.Utc).AddTicks(673);
        private static readonly DateTime SeedSectionCreatedAt10 = new DateTime(2026, 5, 13, 5, 55, 3, 355, DateTimeKind.Utc).AddTicks(674);
        private const string SeedSuperAdminPasswordHash = "$2a$11$jBmPeOcm/RJmOd4/1nrSjei2PtF7efgQUfPiU6r.RZjh2R0qSmgni";

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
                entity.Property(e => e.ServiceId).IsRequired().HasMaxLength(50);
                entity.Property(e => e.UpdatedAt);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.ServiceId).IsUnique();

                entity.HasOne(e => e.Role)
                    .WithMany(r => r.Users)
                    .HasForeignKey(e => e.RoleId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.Section)
                    .WithMany(s => s.Users)
                    .HasForeignKey(e => e.SectionId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasQueryFilter(u => u.IsActive);
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
                new Role { Id = 1, Name = RoleNames.SuperAdmin, Description = "Super Administrator with full system access", CreatedAt = SeedRoleCreatedAt },
                new Role { Id = 2, Name = RoleNames.Admin, Description = "Section Administrator with limited access", CreatedAt = SeedRoleCreatedAt2 },
                new Role { Id = 3, Name = RoleNames.Worker, Description = "Regular worker who can mark attendance", CreatedAt = SeedRoleCreatedAt3 },
                new Role { Id = 4, Name = RoleNames.GM, Description = "General Manager with department oversight", CreatedAt = SeedRoleCreatedAt2 },
                new Role { Id = 5, Name = RoleNames.DGM, Description = "Deputy General Manager with department oversight", CreatedAt = SeedRoleCreatedAt2 },
                new Role { Id = 6, Name = RoleNames.Engineer, Description = "Engineer with attendance edit privileges", CreatedAt = SeedRoleCreatedAt2 },
                new Role { Id = 7, Name = RoleNames.LeaveAgent, Description = "Leave Agent with attendance edit privileges", CreatedAt = SeedRoleCreatedAt2 }
            };

            modelBuilder.Entity<Role>().HasData(roles);

            // Seed Sections
            var sections = new[]
            {
                new Section { Id = -1, Name = "All Sections", Description = "All Departments", CreatedAt = SeedSectionCreatedAt0, IsActive = true },
                new Section { Id = 1, Name = "Information Technology", Description = "IT Department", CreatedAt = SeedSectionCreatedAt1, IsActive = true },
                new Section { Id = 2, Name = "Human Resources", Description = "HR Department", CreatedAt = SeedSectionCreatedAt2, IsActive = true },
                new Section { Id = 3, Name = "Finance", Description = "Finance and Accounting", CreatedAt = SeedSectionCreatedAt3, IsActive = true },
                new Section { Id = 4, Name = "Marketing", Description = "Marketing and Sales", CreatedAt = SeedSectionCreatedAt4, IsActive = true },
                new Section { Id = 5, Name = "Operations", Description = "Operations Department", CreatedAt = SeedSectionCreatedAt5, IsActive = true },
                new Section { Id = 6, Name = "Quality Assurance", Description = "QA Department", CreatedAt = SeedSectionCreatedAt6, IsActive = true },
                new Section { Id = 7, Name = "Research & Development", Description = "R&D Department", CreatedAt = SeedSectionCreatedAt7, IsActive = true },
                new Section { Id = 8, Name = "Customer Support", Description = "Customer Service", CreatedAt = SeedSectionCreatedAt8, IsActive = true },
                new Section { Id = 9, Name = "Administration", Description = "General Administration", CreatedAt = SeedSectionCreatedAt9, IsActive = true },
                new Section { Id = 10, Name = "Production", Description = "Production Department", CreatedAt = SeedSectionCreatedAt10, IsActive = true }
            };

            modelBuilder.Entity<Section>().HasData(sections);

            // Seed Super Admin
            var superAdmin = new User
            {
                Id = 1,
                FirstName = "Super",
                LastName = "Admin",
                Email = "superadmin@attendance.com",
                PasswordHash = SeedSuperAdminPasswordHash,
                Phone = "1234567890",
                ServiceId = "EMP900",
                RoleId = 1, // Super Admin
                SectionId = null,
                IsActive = true
            };

            modelBuilder.Entity<User>().HasData(superAdmin);
        }
    }
}
