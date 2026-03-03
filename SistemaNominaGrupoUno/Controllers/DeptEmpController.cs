using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaNominaGrupoUno.Context;
using SistemaNominaGrupoUno.Context.DTOS;

namespace SistemaNominaGrupoUno.Controllers
{
    /// <summary>
    /// Controlador para la gestión de asignaciones de empleados a departamentos.
    /// RF-04: Registrar relaciones empleado–departamento con from_date y to_date.
    ///        Evitar solapamientos de vigencias para un mismo empleado.
    /// RF-10: Búsqueda por texto y filtros en pantalla de consulta.
    /// RF-11: Validar que to_date no sea anterior a from_date; impedir solapamientos.
    /// RNF-01: Listados paginados (10 filas por página).
    /// RNF-03: Documentación XML en controladores y modelos.
    /// RF-01: Requiere autenticación para acceder.
    /// </summary>
    [Authorize]
    public class DeptEmpController(EmployeeManagementContext context) : Controller
    {
        private const int PageSize = 10;

        // ─────────────────────────────────────────────────────────────
        // HELPER — Log de actividad
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Graba un evento en Log_Actividad para operaciones críticas del módulo.
        /// RF-13: Registro de actividad — crear/editar/terminar vigencia de asignaciones.
        /// </summary>
        private async Task RegistrarActividadAsync(string accion, string detalle)
        {
            try
            {
                context.LogActividad.Add(new LogActividad
                {
                    Usuario = User.Identity?.Name ?? "sistema",
                    Accion  = accion,
                    Entidad = "Asignaciones",
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
        /// Determina si una asignación está actualmente vigente.
        /// Una asignación es activa si ToDate está vacío o es mayor/igual a hoy.
        /// RF-04: Vigencia de asignaciones.
        /// </summary>
        private static bool IsVigente(DeptEmp de)
        {
            if (string.IsNullOrEmpty(de.ToDate)) return true;
            return string.Compare(de.ToDate, DateTime.Today.ToString("yyyy-MM-dd")) >= 0;
        }

        /// <summary>
        /// Verifica si el rango [fromDate, toDate] de un empleado se solapa con
        /// alguna asignación existente en otro departamento (excluyendo excludeDeptNo).
        /// RF-04: Evitar solapamientos de vigencias para un mismo empleado.
        /// RF-11: Validación de negocio — no permitir solapamientos.
        /// </summary>
        private async Task<bool> HasOverlapAsync(
            int empNo, int excludeDeptNo, string fromDate, string? toDate)
        {
            // Fecha de fin efectiva: null/vacío equivale a asignación abierta (∞)
            var effectiveTo = string.IsNullOrEmpty(toDate) ? "9999-12-31" : toDate;

            var existing = await context.DeptEmps
                .Where(de => de.EmpNo == empNo && de.DeptNo != excludeDeptNo)
                .ToListAsync();

            return existing.Any(de =>
            {
                var existingTo = string.IsNullOrEmpty(de.ToDate) ? "9999-12-31" : de.ToDate;
                // Dos períodos se solapan si: inicio1 <= fin2 AND fin1 >= inicio2
                return string.Compare(fromDate, existingTo) <= 0 &&
                       string.Compare(effectiveTo, de.FromDate) >= 0;
            });
        }

        /// <summary>
        /// Carga los SelectList de empleados activos y departamentos activos en ViewBag.
        /// Se usa en Create y Edit para los dropdowns del formulario.
        /// RF-04: Solo se pueden asignar empleados y departamentos activos.
        /// </summary>
        private async Task LoadSelectListsAsync(int? selectedEmpNo = null, int? selectedDeptNo = null)
        {
            var employees = await context.Employees
                .Where(e => e.IsActive)
                .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                .Select(e => new { e.EmpNo, Display = $"#{e.EmpNo} — {e.LastName}, {e.FirstName}" })
                .ToListAsync();

            var departments = await context.Departments
                .Where(d => d.IsActive)
                .OrderBy(d => d.DeptName)
                .Select(d => new { d.DeptNo, Display = $"#{d.DeptNo} — {d.DeptName}" })
                .ToListAsync();

            ViewBag.Employees = new SelectList(employees, "EmpNo", "Display", selectedEmpNo);
            ViewBag.Departments = new SelectList(departments, "DeptNo", "Display", selectedDeptNo);
        }

        // ─────────────────────────────────────────────────────────────
        // INDEX — listado paginado con búsqueda y filtro de estado
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra la lista de asignaciones con paginación, búsqueda por nombre/CI de
        /// empleado o nombre de departamento, y filtro por estado de vigencia.
        /// RF-04: Consultar asignaciones empleado–departamento.
        /// RF-10: Búsqueda por texto y filtros por campos clave.
        /// RNF-01: Paginación de 10 registros por página.
        /// </summary>
        public async Task<IActionResult> Index(string? search, string? status, int page = 1)
        {
            var query = context.DeptEmps
                .Include(de => de.Employee)
                .Include(de => de.Department)
                .AsQueryable();

            // RF-10: Búsqueda por nombre/CI del empleado o nombre del departamento
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(de =>
                    de.Employee.FirstName.ToLower().Contains(term) ||
                    de.Employee.LastName.ToLower().Contains(term)  ||
                    de.Employee.Ci.ToLower().Contains(term)        ||
                    de.Department.DeptName.ToLower().Contains(term));
            }

            // Cargar para filtrar vigencia en memoria (ToDate es string)
            var all = await query
                .OrderBy(de => de.Employee.LastName)
                .ThenBy(de => de.FromDate)
                .ToListAsync();

            // Filtro por estado de vigencia
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            var filtered = (status switch
            {
                "inactivo" => all.Where(de =>
                    !string.IsNullOrEmpty(de.ToDate) &&
                    string.Compare(de.ToDate, today) < 0),
                "activo" or null => all.Where(IsVigente),
                _ => all.AsEnumerable()          // "" → todos
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
        /// Muestra el detalle de una asignación empleado–departamento.
        /// RF-04: Consultar asignación.
        /// </summary>
        public async Task<IActionResult> Details(int empNo, int deptNo)
        {
            var deptEmp = await context.DeptEmps
                .Include(de => de.Employee)
                .Include(de => de.Department)
                .FirstOrDefaultAsync(de => de.EmpNo == empNo && de.DeptNo == deptNo);

            if (deptEmp == null) return NotFound();
            return View(deptEmp);
        }

        // ─────────────────────────────────────────────────────────────
        // CREATE
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el formulario de alta de una nueva asignación.
        /// RF-04: Alta de asignación empleado–departamento.
        /// </summary>
        public async Task<IActionResult> Create()
        {
            await LoadSelectListsAsync();
            return View(new DeptEmpViewModel
            {
                FromDate = DateTime.Today.ToString("yyyy-MM-dd")
            });
        }

        /// <summary>
        /// Procesa la creación de una nueva asignación validando que no exista la
        /// combinación empleado–departamento y que no haya solapamiento de vigencias.
        /// RF-04: Alta de asignación con validación de solapamientos.
        /// RF-11: to_date no puede ser anterior a from_date; sin solapamientos.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DeptEmpViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await LoadSelectListsAsync(vm.EmpNo, vm.DeptNo);
                return View(vm);
            }

            // RF-11: to_date no puede ser anterior a from_date
            if (!string.IsNullOrEmpty(vm.ToDate) &&
                string.Compare(vm.ToDate, vm.FromDate) < 0)
            {
                ModelState.AddModelError(nameof(vm.ToDate),
                    "La fecha hasta no puede ser anterior a la fecha desde.");
                await LoadSelectListsAsync(vm.EmpNo, vm.DeptNo);
                return View(vm);
            }

            // RF-04: La combinación empleado–departamento no debe existir ya
            if (await context.DeptEmps.AnyAsync(
                    de => de.EmpNo == vm.EmpNo && de.DeptNo == vm.DeptNo))
            {
                ModelState.AddModelError(string.Empty,
                    "Este empleado ya tiene una asignación registrada en ese departamento.");
                await LoadSelectListsAsync(vm.EmpNo, vm.DeptNo);
                return View(vm);
            }

            // RF-04 / RF-11: Validar que no haya solapamiento con otras asignaciones
            if (await HasOverlapAsync(vm.EmpNo, vm.DeptNo, vm.FromDate, vm.ToDate))
            {
                ModelState.AddModelError(string.Empty,
                    "El empleado ya tiene una asignación activa en otro departamento que se solapa con el período indicado.");
                await LoadSelectListsAsync(vm.EmpNo, vm.DeptNo);
                return View(vm);
            }

            var deptEmp = new DeptEmp
            {
                EmpNo    = vm.EmpNo,
                DeptNo   = vm.DeptNo,
                FromDate = vm.FromDate,
                ToDate   = vm.ToDate ?? string.Empty
            };

            context.DeptEmps.Add(deptEmp);
            await context.SaveChangesAsync();

            // Cargar nombres para el mensaje
            var emp  = await context.Employees.FindAsync(vm.EmpNo);
            var dept = await context.Departments.FindAsync(vm.DeptNo);

            // RF-13: Registrar alta de asignación en log de actividad
            await RegistrarActividadAsync("Alta",
                $"Asignación: {emp!.FirstName} {emp.LastName} → \"{dept!.DeptName}\" (desde {vm.FromDate}).");

            TempData["Success"] =
                $"Empleado {emp.FirstName} {emp.LastName} asignado al departamento \"{dept.DeptName}\" exitosamente.";

            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────
        // EDIT — solo se editan las fechas de vigencia
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el formulario de edición de fechas de una asignación existente.
        /// Solo se pueden modificar FromDate y ToDate; la clave (EmpNo, DeptNo) es fija.
        /// RF-04: Modificación de vigencia de asignación.
        /// </summary>
        public async Task<IActionResult> Edit(int empNo, int deptNo)
        {
            var deptEmp = await context.DeptEmps
                .Include(de => de.Employee)
                .Include(de => de.Department)
                .FirstOrDefaultAsync(de => de.EmpNo == empNo && de.DeptNo == deptNo);

            if (deptEmp == null) return NotFound();

            var vm = new DeptEmpViewModel
            {
                EmpNo    = deptEmp.EmpNo,
                DeptNo   = deptEmp.DeptNo,
                FromDate = deptEmp.FromDate,
                ToDate   = string.IsNullOrEmpty(deptEmp.ToDate) ? null : deptEmp.ToDate
            };

            ViewBag.EmpDisplay  = $"#{deptEmp.Employee.EmpNo} — {deptEmp.Employee.LastName}, {deptEmp.Employee.FirstName}";
            ViewBag.DeptDisplay = $"#{deptEmp.Department.DeptNo} — {deptEmp.Department.DeptName}";

            return View(vm);
        }

        /// <summary>
        /// Procesa la edición de fechas de una asignación, validando que to_date no sea
        /// anterior a from_date y que no haya solapamientos con otras asignaciones.
        /// RF-04: Modificación de vigencia con validación de solapamientos.
        /// RF-11: to_date no puede ser anterior a from_date; sin solapamientos.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int empNo, int deptNo, DeptEmpViewModel vm)
        {
            if (empNo != vm.EmpNo || deptNo != vm.DeptNo) return BadRequest();

            var deptEmp = await context.DeptEmps
                .Include(de => de.Employee)
                .Include(de => de.Department)
                .FirstOrDefaultAsync(de => de.EmpNo == empNo && de.DeptNo == deptNo);

            if (deptEmp == null) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.EmpDisplay  = $"#{deptEmp.Employee.EmpNo} — {deptEmp.Employee.LastName}, {deptEmp.Employee.FirstName}";
                ViewBag.DeptDisplay = $"#{deptEmp.Department.DeptNo} — {deptEmp.Department.DeptName}";
                return View(vm);
            }

            // RF-11: to_date no puede ser anterior a from_date
            if (!string.IsNullOrEmpty(vm.ToDate) &&
                string.Compare(vm.ToDate, vm.FromDate) < 0)
            {
                ModelState.AddModelError(nameof(vm.ToDate),
                    "La fecha hasta no puede ser anterior a la fecha desde.");
                ViewBag.EmpDisplay  = $"#{deptEmp.Employee.EmpNo} — {deptEmp.Employee.LastName}, {deptEmp.Employee.FirstName}";
                ViewBag.DeptDisplay = $"#{deptEmp.Department.DeptNo} — {deptEmp.Department.DeptName}";
                return View(vm);
            }

            // RF-04 / RF-11: Solapamiento con otras asignaciones del mismo empleado
            if (await HasOverlapAsync(vm.EmpNo, vm.DeptNo, vm.FromDate, vm.ToDate))
            {
                ModelState.AddModelError(string.Empty,
                    "El empleado ya tiene una asignación activa en otro departamento que se solapa con el período indicado.");
                ViewBag.EmpDisplay  = $"#{deptEmp.Employee.EmpNo} — {deptEmp.Employee.LastName}, {deptEmp.Employee.FirstName}";
                ViewBag.DeptDisplay = $"#{deptEmp.Department.DeptNo} — {deptEmp.Department.DeptName}";
                return View(vm);
            }

            deptEmp.FromDate = vm.FromDate;
            deptEmp.ToDate   = vm.ToDate ?? string.Empty;
            await context.SaveChangesAsync();

            // RF-13: Registrar edición de asignación en log de actividad
            await RegistrarActividadAsync("Edición",
                $"Asignación: {deptEmp.Employee.FirstName} {deptEmp.Employee.LastName} → \"{deptEmp.Department.DeptName}\" actualizada " +
                $"(desde {deptEmp.FromDate}{(string.IsNullOrEmpty(deptEmp.ToDate) ? "" : $" hasta {deptEmp.ToDate}")}).");

            TempData["Success"] =
                $"Asignación de {deptEmp.Employee.FirstName} {deptEmp.Employee.LastName} " +
                $"en \"{deptEmp.Department.DeptName}\" actualizada exitosamente.";

            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────
        // END ASSIGNMENT — terminar vigencia
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Termina la vigencia de una asignación estableciendo ToDate = hoy.
        /// RF-04: Terminar vigencia de asignación empleado–departamento.
        /// CU-02: El sistema finaliza la vigencia anterior cuando corresponde.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EndAssignment(int empNo, int deptNo)
        {
            var deptEmp = await context.DeptEmps
                .Include(de => de.Employee)
                .Include(de => de.Department)
                .FirstOrDefaultAsync(de => de.EmpNo == empNo && de.DeptNo == deptNo);

            if (deptEmp == null) return NotFound();

            deptEmp.ToDate = DateTime.Today.ToString("yyyy-MM-dd");
            await context.SaveChangesAsync();

            // RF-13: Registrar término de vigencia en log de actividad
            await RegistrarActividadAsync("Término de vigencia",
                $"Asignación: {deptEmp.Employee.FirstName} {deptEmp.Employee.LastName} → \"{deptEmp.Department.DeptName}\" finalizada al {deptEmp.ToDate}.");

            TempData["Warning"] =
                $"Vigencia de {deptEmp.Employee.FirstName} {deptEmp.Employee.LastName} " +
                $"en \"{deptEmp.Department.DeptName}\" finalizada al {deptEmp.ToDate}.";

            return RedirectToAction(nameof(Index));
        }
    }
}
