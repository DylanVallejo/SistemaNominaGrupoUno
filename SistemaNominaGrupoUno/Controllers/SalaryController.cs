using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaNominaGrupoUno.Context;
using SistemaNominaGrupoUno.Context.DTOS;

namespace SistemaNominaGrupoUno.Controllers
{
    /// <summary>
    /// Controlador para la gestión del historial salarial de empleados.
    /// RF-07: Registrar salarios por empleado con from_date/to_date.
    ///        Solo un salario activo por empleado en una fecha dada.
    /// RF-08: Registrar en Log_AuditoriaSalarios cada alta o cambio de salario
    ///        con usuario, fecha/hora, detalle y monto.
    /// RF-10: Búsqueda por texto y filtros en pantalla de consulta.
    /// RF-11: Validar que to_date no sea anterior a from_date; sin solapamientos; salario > 0.
    /// RNF-01: Listados paginados (10 filas por página).
    /// RNF-03: Documentación XML en controladores y modelos.
    /// </summary>
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class SalaryController(EmployeeManagementContext context) : Controller
    {
        private const int PageSize = 10;

        // ─────────────────────────────────────────────────────────────
        // HELPER — Log de actividad (RF-13)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Graba un evento en Log_Actividad para operaciones críticas del módulo.
        /// RF-13: Registro de actividad — alta/edición/término de vigencia de salarios.
        /// Nota: RF-08 (Log_AuditoriaSalarios) es independiente y más detallado.
        /// </summary>
        private async Task RegistrarActividadAsync(string accion, string detalle)
        {
            try
            {
                context.LogActividad.Add(new LogActividad
                {
                    Usuario = User.Identity?.Name ?? "sistema",
                    Accion  = accion,
                    Entidad = "Salarios",
                    Detalle = detalle,
                    Fecha   = DateTime.Now
                });
                await context.SaveChangesAsync();
            }
            catch
            {
                // No interrumpir el flujo principal si el log falla
            }
        }

        // ─────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Determina si un salario está actualmente vigente.
        /// Vigente = ToDate vacío O ToDate >= hoy.
        /// RF-07: Vigencia del salario activo.
        /// </summary>
        private static bool IsVigente(Salaries s) =>
            string.IsNullOrEmpty(s.ToDate) ||
            string.Compare(s.ToDate, DateTime.Today.ToString("yyyy-MM-dd")) >= 0;

        /// <summary>
        /// Verifica si el período [fromDate, toDate] se solapa con algún salario
        /// existente del mismo empleado, excluyendo el registro de excludeFromDate.
        /// RF-07: Solo un salario activo por empleado en una fecha dada.
        /// RF-11: Impedir solapamientos de salarios para el mismo empleado.
        /// </summary>
        private async Task<bool> HasOverlapAsync(
            int empNo, string excludeFromDate, string fromDate, string? toDate)
        {
            var effectiveTo = string.IsNullOrEmpty(toDate) ? "9999-12-31" : toDate;

            var existing = await context.Salaries
                .Where(s => s.EmpNo == empNo && s.FromDate != excludeFromDate)
                .ToListAsync();

            return existing.Any(s =>
            {
                var existingTo = string.IsNullOrEmpty(s.ToDate) ? "9999-12-31" : s.ToDate;
                return string.Compare(fromDate, existingTo) <= 0 &&
                       string.Compare(effectiveTo, s.FromDate) >= 0;
            });
        }

        /// <summary>
        /// Registra un evento en Log_AuditoriaSalarios.
        /// RF-08: Alta o cambio de salario auditado con usuario, fecha/hora, detalle y monto.
        /// </summary>
        private async Task RegistrarAuditoriaAsync(int empNo, long salario, string detalle)
        {
            // Hasta que el módulo de autenticación esté implementado se usa "sistema"
            var usuario = User.Identity?.Name ?? "sistema";

            context.LogAuditoriaSalarios.Add(new LogAuditoriaSalarios
            {
                EmpNo               = empNo,
                Salario             = salario,
                DetalleCambio       = detalle,
                FechaActualizacion  = DateTime.Now,
                Usuario             = usuario
            });

            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Carga el SelectList de empleados activos en ViewBag.
        /// RF-07: Solo se registran salarios para empleados activos.
        /// </summary>
        private async Task LoadEmployeesAsync(int? selectedEmpNo = null)
        {
            var employees = await context.Employees
                .Where(e => e.IsActive)
                .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                .Select(e => new { e.EmpNo, Display = $"#{e.EmpNo} — {e.LastName}, {e.FirstName}" })
                .ToListAsync();

            ViewBag.Employees = new SelectList(employees, "EmpNo", "Display", selectedEmpNo);
        }

        // ─────────────────────────────────────────────────────────────
        // INDEX — listado paginado con búsqueda y filtro de vigencia
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el historial salarial con paginación, búsqueda por nombre/CI del
        /// empleado, y filtro por vigencia.
        /// RF-07: Consultar salarios registrados.
        /// RF-10: Búsqueda por texto y filtros por campos clave.
        /// RNF-01: Paginación de 10 registros por página.
        /// </summary>
        public async Task<IActionResult> Index(string? search, string? status, int page = 1)
        {
            var query = context.Salaries
                .Include(s => s.Employee)
                .AsQueryable();

            // RF-10: Búsqueda por nombre o CI del empleado
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(s =>
                    s.Employee.FirstName.ToLower().Contains(term) ||
                    s.Employee.LastName.ToLower().Contains(term)  ||
                    s.Employee.Ci.ToLower().Contains(term));
            }

            var all = await query
                .OrderBy(s => s.Employee.LastName)
                .ThenByDescending(s => s.FromDate)
                .ToListAsync();

            var today = DateTime.Today.ToString("yyyy-MM-dd");
            var filtered = (status switch
            {
                "inactivo" => all.Where(s =>
                    !string.IsNullOrEmpty(s.ToDate) &&
                    string.Compare(s.ToDate, today) < 0),
                "activo" or null => all.Where(IsVigente),
                _ => all.AsEnumerable()
            }).ToList();

            int totalItems = filtered.Count;
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
            page = Math.Clamp(page, 1, totalPages);

            var items = filtered
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            ViewBag.Search      = search;
            ViewBag.Status      = status ?? "activo";
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages  = totalPages;
            ViewBag.TotalItems  = totalItems;

            return View(items);
        }

        // ─────────────────────────────────────────────────────────────
        // DETAILS
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el detalle de un registro salarial.
        /// La clave primaria es (EmpNo, FromDate).
        /// RF-07: Consultar salario registrado.
        /// </summary>
        public async Task<IActionResult> Details(int empNo, string fromDate)
        {
            var salary = await context.Salaries
                .Include(s => s.Employee)
                .FirstOrDefaultAsync(s => s.EmpNo == empNo && s.FromDate == fromDate);

            if (salary == null) return NotFound();
            return View(salary);
        }

        // ─────────────────────────────────────────────────────────────
        // CREATE
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el formulario de alta de un nuevo salario para un empleado.
        /// RF-07: Alta de salario con fecha de inicio.
        /// </summary>
        public async Task<IActionResult> Create()
        {
            await LoadEmployeesAsync();
            return View(new SalaryViewModel
            {
                FromDate = DateTime.Today.ToString("yyyy-MM-dd")
            });
        }

        /// <summary>
        /// Procesa el alta de un nuevo salario validando solapamientos y generando
        /// el registro de auditoría correspondiente.
        /// RF-07: Alta de salario con validación de período único activo.
        /// RF-08: Registro automático en Log_AuditoriaSalarios al crear salario.
        /// RF-11: to_date no puede ser anterior a from_date; sin solapamientos; salario > 0.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SalaryViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await LoadEmployeesAsync(vm.EmpNo);
                return View(vm);
            }

            // RF-11: to_date no puede ser anterior a from_date
            if (!string.IsNullOrEmpty(vm.ToDate) &&
                string.Compare(vm.ToDate, vm.FromDate) < 0)
            {
                ModelState.AddModelError(nameof(vm.ToDate),
                    "La fecha hasta no puede ser anterior a la fecha desde.");
                await LoadEmployeesAsync(vm.EmpNo);
                return View(vm);
            }

            // RF-07: La combinación (EmpNo, FromDate) no puede existir ya (PK)
            if (await context.Salaries.AnyAsync(
                    s => s.EmpNo == vm.EmpNo && s.FromDate == vm.FromDate))
            {
                ModelState.AddModelError(nameof(vm.FromDate),
                    "Ya existe un salario registrado para este empleado en esa fecha de inicio.");
                await LoadEmployeesAsync(vm.EmpNo);
                return View(vm);
            }

            // RF-07 / RF-11: Solapamiento con otros salarios del mismo empleado
            if (await HasOverlapAsync(vm.EmpNo, string.Empty, vm.FromDate, vm.ToDate))
            {
                ModelState.AddModelError(string.Empty,
                    "El empleado ya tiene un salario registrado cuyo período se solapa con el indicado.");
                await LoadEmployeesAsync(vm.EmpNo);
                return View(vm);
            }

            var salary = new Salaries
            {
                EmpNo    = vm.EmpNo,
                Salary   = vm.SalaryAmount,
                FromDate = vm.FromDate,
                ToDate   = vm.ToDate ?? string.Empty
            };

            context.Salaries.Add(salary);
            await context.SaveChangesAsync();

            // RF-08: Auditoría — alta de salario
            var emp = await context.Employees.FindAsync(vm.EmpNo);
            await RegistrarAuditoriaAsync(
                vm.EmpNo,
                vm.SalaryAmount,
                $"Alta de salario: ${vm.SalaryAmount:N0} desde {vm.FromDate} " +
                $"para {emp!.FirstName} {emp.LastName}");

            // RF-13: Registrar alta de salario en log de actividad general
            await RegistrarActividadAsync("Alta",
                $"Salario ${vm.SalaryAmount:N0} registrado para {emp.FirstName} {emp.LastName} (desde {vm.FromDate}).");

            TempData["Success"] =
                $"Salario ${vm.SalaryAmount:N0} registrado para {emp.FirstName} {emp.LastName} exitosamente.";

            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────
        // EDIT — se puede modificar monto y ToDate (FromDate es parte de PK)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el formulario de edición de un salario existente.
        /// Se puede modificar el monto y ToDate; FromDate es fijo (parte de PK).
        /// RF-07: Modificación de salario registrado.
        /// </summary>
        public async Task<IActionResult> Edit(int empNo, string fromDate)
        {
            var salary = await context.Salaries
                .Include(s => s.Employee)
                .FirstOrDefaultAsync(s => s.EmpNo == empNo && s.FromDate == fromDate);

            if (salary == null) return NotFound();

            var vm = new SalaryViewModel
            {
                EmpNo            = salary.EmpNo,
                SalaryAmount     = salary.Salary,
                FromDate         = salary.FromDate,
                OriginalFromDate = salary.FromDate,
                ToDate           = string.IsNullOrEmpty(salary.ToDate) ? null : salary.ToDate
            };

            ViewBag.EmpDisplay   = $"#{salary.Employee.EmpNo} — {salary.Employee.LastName}, {salary.Employee.FirstName}";
            ViewBag.SalarioAnterior = salary.Salary;
            return View(vm);
        }

        /// <summary>
        /// Procesa la edición del monto y ToDate de un salario, validando solapamientos
        /// y generando el registro de auditoría con el valor anterior y el nuevo.
        /// RF-07: Modificación de salario con validación de período único activo.
        /// RF-08: Registro automático en Log_AuditoriaSalarios al modificar salario.
        /// RF-11: to_date no puede ser anterior a from_date; sin solapamientos; salario > 0.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int empNo, string fromDate, SalaryViewModel vm)
        {
            if (empNo != vm.EmpNo || fromDate != vm.OriginalFromDate) return BadRequest();

            var salary = await context.Salaries
                .Include(s => s.Employee)
                .FirstOrDefaultAsync(s => s.EmpNo == empNo && s.FromDate == fromDate);

            if (salary == null) return NotFound();

            void SetDisplayBag()
            {
                ViewBag.EmpDisplay      = $"#{salary.Employee.EmpNo} — {salary.Employee.LastName}, {salary.Employee.FirstName}";
                ViewBag.SalarioAnterior = salary.Salary;
            }

            if (!ModelState.IsValid) { SetDisplayBag(); return View(vm); }

            // RF-11: to_date no puede ser anterior a from_date
            if (!string.IsNullOrEmpty(vm.ToDate) &&
                string.Compare(vm.ToDate, vm.FromDate) < 0)
            {
                ModelState.AddModelError(nameof(vm.ToDate),
                    "La fecha hasta no puede ser anterior a la fecha desde.");
                SetDisplayBag();
                return View(vm);
            }

            // RF-07 / RF-11: Solapamiento con otros salarios del mismo empleado
            if (await HasOverlapAsync(vm.EmpNo, vm.OriginalFromDate, vm.FromDate, vm.ToDate))
            {
                ModelState.AddModelError(string.Empty,
                    "El empleado ya tiene otro salario cuyo período se solapa con el indicado.");
                SetDisplayBag();
                return View(vm);
            }

            long salarioAnterior = salary.Salary;
            salary.Salary  = vm.SalaryAmount;
            salary.ToDate  = vm.ToDate ?? string.Empty;
            await context.SaveChangesAsync();

            // RF-08: Auditoría — modificación de salario con monto anterior y nuevo
            await RegistrarAuditoriaAsync(
                vm.EmpNo,
                vm.SalaryAmount,
                $"Modificación de salario: ${salarioAnterior:N0} → ${vm.SalaryAmount:N0} " +
                $"(desde {salary.FromDate}) para {salary.Employee.FirstName} {salary.Employee.LastName}");

            // RF-13: Registrar modificación de salario en log de actividad general
            await RegistrarActividadAsync("Edición",
                $"Salario de {salary.Employee.FirstName} {salary.Employee.LastName} modificado: ${salarioAnterior:N0} → ${vm.SalaryAmount:N0}.");

            TempData["Success"] =
                $"Salario de {salary.Employee.FirstName} {salary.Employee.LastName} " +
                $"actualizado a ${vm.SalaryAmount:N0} exitosamente.";

            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────
        // END ASSIGNMENT — terminar vigencia del salario
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Termina la vigencia de un salario estableciendo ToDate = hoy.
        /// RF-07: Terminar vigencia de salario activo.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EndAssignment(int empNo, string fromDate)
        {
            var salary = await context.Salaries
                .Include(s => s.Employee)
                .FirstOrDefaultAsync(s => s.EmpNo == empNo && s.FromDate == fromDate);

            if (salary == null) return NotFound();

            salary.ToDate = DateTime.Today.ToString("yyyy-MM-dd");
            await context.SaveChangesAsync();

            // RF-13: Registrar término de vigencia salarial en log de actividad general
            await RegistrarActividadAsync("Término de vigencia",
                $"Salario ${salary.Salary:N0} de {salary.Employee.FirstName} {salary.Employee.LastName} finalizado al {salary.ToDate}.");

            TempData["Warning"] =
                $"Vigencia del salario ${salary.Salary:N0} de {salary.Employee.FirstName} " +
                $"{salary.Employee.LastName} finalizada al {salary.ToDate}.";

            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────
        // LOG — consulta del registro de auditoría
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el log de auditoría de cambios salariales con paginación y
        /// búsqueda por empleado o usuario que realizó el cambio.
        /// RF-08: Consultar Log_AuditoriaSalarios por fecha/usuario/emp_no.
        /// RF-10: Búsqueda por texto y filtros por campos clave.
        /// </summary>
        public async Task<IActionResult> AuditLog(string? search, int page = 1)
        {
            var query = context.LogAuditoriaSalarios
                .Include(l => l.Employee)
                .AsQueryable();

            // RF-10: Búsqueda por nombre/CI del empleado o por usuario
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(l =>
                    l.Employee.FirstName.ToLower().Contains(term) ||
                    l.Employee.LastName.ToLower().Contains(term)  ||
                    l.Employee.Ci.ToLower().Contains(term)        ||
                    l.Usuario.ToLower().Contains(term)            ||
                    l.DetalleCambio.ToLower().Contains(term));
            }

            int totalItems = await query.CountAsync();
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
            page = Math.Clamp(page, 1, totalPages);

            var items = await query
                .OrderByDescending(l => l.FechaActualizacion)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            ViewBag.Search      = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages  = totalPages;
            ViewBag.TotalItems  = totalItems;

            return View(items);
        }
    }
}
