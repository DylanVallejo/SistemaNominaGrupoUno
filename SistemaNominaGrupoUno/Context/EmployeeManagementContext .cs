using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SistemaNominaGrupoUno.Context
{
    public class EmployeeManagementContext : DbContext
    {
        public EmployeeManagementContext(DbContextOptions<EmployeeManagementContext> options)
          : base(options)
        {
        }

        public DbSet<Departments> Departments { get; set; }
        public DbSet<DeptManager> DeptManagers { get; set; }
        public DbSet<DeptEmp> DeptEmps { get; set; }
        public DbSet<Employees> Employees { get; set; }
        public DbSet<Salaries> Salaries { get; set; }
        public DbSet<Titles> Titles { get; set; }
        public DbSet<LogAuditoriaSalarios> LogAuditoriaSalarios { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Primary Keys
            modelBuilder.Entity<Departments>().HasKey(d => d.DeptNo);
            modelBuilder.Entity<Employees>().HasKey(e => e.EmpNo);
            modelBuilder.Entity<Salaries>().HasKey(s => new { s.EmpNo, s.FromDate });
            modelBuilder.Entity<Users>().HasKey(u => u.EmpNo);

            modelBuilder.Entity<Titles>().HasKey(t => new { t.EmpNo, t.FromDate });
            modelBuilder.Entity<DeptManager>().HasKey(dm => new { dm.EmpNo, dm.DeptNo });
            modelBuilder.Entity<DeptEmp>().HasKey(de => new { de.EmpNo, de.DeptNo });
            modelBuilder.Entity<LogAuditoriaSalarios>().HasKey(l => l.Id);

            // Relationships
            modelBuilder.Entity<DeptManager>()
                .HasOne(dm => dm.Employee)
                .WithMany(e => e.ManagedDepartments)
                .HasForeignKey(dm => dm.EmpNo);

            modelBuilder.Entity<DeptManager>()
                .HasOne(dm => dm.Department)
                .WithMany(d => d.Managers)
                .HasForeignKey(dm => dm.DeptNo);

            modelBuilder.Entity<DeptEmp>()
                .HasOne(de => de.Employee)
                .WithMany(e => e.Departments)
                .HasForeignKey(de => de.EmpNo);

            modelBuilder.Entity<DeptEmp>()
                .HasOne(de => de.Department)
                .WithMany(d => d.Employees)
                .HasForeignKey(de => de.DeptNo);

            modelBuilder.Entity<Users>()
                .HasOne(u => u.Employee)
                .WithOne(e => e.User)
                .HasForeignKey<Users>(u => u.EmpNo);

            modelBuilder.Entity<Salaries>()
                .HasOne(s => s.Employee)
                .WithMany(e => e.Salaries)
                .HasForeignKey(s => s.EmpNo);

            modelBuilder.Entity<Titles>()
                .HasOne(t => t.Employee)
                .WithMany(e => e.Titles)
                .HasForeignKey(t => t.EmpNo);

            modelBuilder.Entity<LogAuditoriaSalarios>()
                .HasOne(l => l.Employee)
                .WithMany()
                .HasForeignKey(l => l.EmpNo);

            // Opcional: nombres de tablas explícitos (si quieres asegurarte)
            modelBuilder.Entity<Departments>().ToTable("Departments");
            modelBuilder.Entity<Employees>().ToTable("Employees");
            modelBuilder.Entity<Users>().ToTable("Users");
            modelBuilder.Entity<Salaries>().ToTable("Salaries");
            modelBuilder.Entity<Titles>().ToTable("Titles");
            modelBuilder.Entity<DeptManager>().ToTable("DeptManager");
            modelBuilder.Entity<DeptEmp>().ToTable("DeptEmp");
            modelBuilder.Entity<LogAuditoriaSalarios>().ToTable("Log_AuditoriaSalarios");
        }
    }

}
