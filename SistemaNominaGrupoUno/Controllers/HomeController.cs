using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaNominaGrupoUno.Context;
using SistemaNominaGrupoUno.Context.DTOS;
using SistemaNominaGrupoUno.Models;
using System.Diagnostics;

namespace SistemaNominaGrupoUno.Controllers
{
    /// <summary>
    /// Controlador principal del sistema. Aloja el Dashboard.
    /// Sección 7 PDF: Pantalla Dashboard — resumen de empleados, departamentos,
    /// salarios vigentes, alertas de vigencias por vencer y accesos rápidos.
    /// RF-01: Requiere autenticación para ver el Dashboard.
    /// </summary>
    public class HomeController(EmployeeManagementContext context) : Controller
    {
        // ─────────────────────────────────────────────────────────────
        // DASHBOARD — Pantalla principal (Sección 7 PDF)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Construye y muestra el Dashboard principal del sistema con métricas
        /// en tiempo real y alertas de vigencias próximas a vencer (30 días).
        /// Sección 7 PDF: Dashboard — resumen y accesos rápidos a módulos.
        /// RF-02: Conteo de empleados activos e inactivos.
        /// RF-03: Conteo de departamentos activos e inactivos.
        /// RF-07: Total de salarios vigentes hoy.
        /// RF-01: Acceso restringido a usuarios autenticados.
        /// </summary>
        [Authorize]
        public async Task<IActionResult> Index()
        {
            var today    = DateTime.Today;
            var todayStr = today.ToString("yyyy-MM-dd");
            var in30Str  = today.AddDays(30).ToString("yyyy-MM-dd");

            // ── Métricas principales ──────────────────────────────

            // RF-02: Total empleados activos / inactivos
            var totalEmpActivos   = await context.Employees.CountAsync(e => e.IsActive);
            var totalEmpInactivos = await context.Employees.CountAsync(e => !e.IsActive);

            // RF-03: Total departamentos activos / inactivos
            var totalDeptActivos   = await context.Departments.CountAsync(d => d.IsActive);
            var totalDeptInactivos = await context.Departments.CountAsync(d => !d.IsActive);

            // RF-07: Salarios cuya vigencia está activa hoy (ToDate vacío o >= hoy)
            var allSalaries = await context.Salaries.ToListAsync();
            var totalSalariosVigentes = allSalaries.Count(s =>
                string.IsNullOrEmpty(s.ToDate) ||
                string.Compare(s.ToDate, todayStr) >= 0);

            // ── Alertas de vigencias próximas a vencer (≤ 30 días) ──

            var alertas = new List<AlertaVigencia>();

            // Asignaciones (DeptEmp) con ToDate en los próximos 30 días — RF-04
            var deptEmps = await context.DeptEmps
                .Include(de => de.Employee)
                .Include(de => de.Department)
                .Where(de =>
                    !string.IsNullOrEmpty(de.ToDate) &&
                    string.Compare(de.ToDate, todayStr) >= 0 &&
                    string.Compare(de.ToDate, in30Str)  <= 0)
                .ToListAsync();

            alertas.AddRange(deptEmps.Select(de => new AlertaVigencia
            {
                Tipo             = "Asignación",
                Descripcion      = $"{de.Employee.FirstName} {de.Employee.LastName} → {de.Department.DeptName}",
                FechaVencimiento = de.ToDate,
                DiasRestantes    = (DateTime.Parse(de.ToDate) - today).Days
            }));

            // Gerencias (DeptManager) con ToDate en los próximos 30 días — RF-05
            var deptManagers = await context.DeptManagers
                .Include(dm => dm.Employee)
                .Include(dm => dm.Department)
                .Where(dm =>
                    !string.IsNullOrEmpty(dm.ToDate) &&
                    string.Compare(dm.ToDate, todayStr) >= 0 &&
                    string.Compare(dm.ToDate, in30Str)  <= 0)
                .ToListAsync();

            alertas.AddRange(deptManagers.Select(dm => new AlertaVigencia
            {
                Tipo             = "Gerencia",
                Descripcion      = $"{dm.Employee.FirstName} {dm.Employee.LastName} (gerente {dm.Department.DeptName})",
                FechaVencimiento = dm.ToDate,
                DiasRestantes    = (DateTime.Parse(dm.ToDate) - today).Days
            }));

            // Títulos con ToDate en los próximos 30 días — RF-06
            var titles = await context.Titles
                .Include(t => t.Employee)
                .Where(t =>
                    !string.IsNullOrEmpty(t.ToDate) &&
                    string.Compare(t.ToDate, todayStr) >= 0 &&
                    string.Compare(t.ToDate, in30Str)  <= 0)
                .ToListAsync();

            alertas.AddRange(titles.Select(t => new AlertaVigencia
            {
                Tipo             = "Título",
                Descripcion      = $"{t.Employee.FirstName} {t.Employee.LastName} — {t.Title}",
                FechaVencimiento = t.ToDate,
                DiasRestantes    = (DateTime.Parse(t.ToDate) - today).Days
            }));

            // Salarios con ToDate en los próximos 30 días — RF-07
            var salariesAlertas = await context.Salaries
                .Include(s => s.Employee)
                .Where(s =>
                    !string.IsNullOrEmpty(s.ToDate) &&
                    string.Compare(s.ToDate, todayStr) >= 0 &&
                    string.Compare(s.ToDate, in30Str)  <= 0)
                .ToListAsync();

            alertas.AddRange(salariesAlertas.Select(s => new AlertaVigencia
            {
                Tipo             = "Salario",
                Descripcion      = $"{s.Employee.FirstName} {s.Employee.LastName} — ${s.Salary:N0}",
                FechaVencimiento = s.ToDate,
                DiasRestantes    = (DateTime.Parse(s.ToDate) - today).Days
            }));

            // Ordenar por días restantes (más urgentes primero)
            alertas = [.. alertas.OrderBy(a => a.DiasRestantes)];

            var vm = new DashboardViewModel
            {
                TotalEmpleadosActivos   = totalEmpActivos,
                TotalEmpleadosInactivos = totalEmpInactivos,
                TotalDeptosActivos      = totalDeptActivos,
                TotalDeptosInactivos    = totalDeptInactivos,
                TotalSalariosVigentes   = totalSalariosVigentes,
                AlertasVigencias        = alertas
            };

            return View(vm);
        }

        // ─────────────────────────────────────────────────────────────
        // ERROR
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Maneja errores HTTP no controlados.
        /// RNF-05: Mensajes de error claros al usuario.
        /// </summary>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
