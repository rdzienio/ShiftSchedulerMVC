using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShiftSchedulerMVC.Models;

namespace ShiftSchedulerMVC.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<LeaveRequest> LeaveRequests { get; set; }

        public DbSet<HolidayOverride> HolidayOverrides { get; set; }

        public DbSet<FinalizedSchedule> FinalizedSchedules { get; set; }

        public DbSet<DraftSchedule> DraftSchedules { get; set; }


        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // 🔁 Relacja 1:N - Menedżer ma wielu pracowników
            builder.Entity<ApplicationUser>()
                .HasMany(m => m.Employees)
                .WithOne(e => e.Manager)
                .HasForeignKey(e => e.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<DraftSchedule>()
                .HasOne(d => d.Employee)
                .WithMany()
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);


        }
    }
}
