using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaNominaGrupoUno.Context;
using SistemaNominaGrupoUno.Context.DTOS;

namespace SistemaNominaGrupoUno.Controllers
{
    /// <summary>
    /// Controlador para la gestión de gerentes de departamento.
    /// RF-05: Registrar el manager (emp_no) con from_date y to_date.
    ///        Validar un solo manager activo por departamento en una fecha dada.
    /// RF-10: Búsqueda por texto y filtros en pantalla de consulta.
    /// RF-11: Validar que to_date no sea anterior a from_date; un solo manager activo por depto.
    /// RNF-01: Listados paginados (10 filas por página).
    /// RNF-03: Documentación XML en controladores y modelos.
    /// CU-04: Nombrar Gerente de Departamento — validar exclusividad por fecha.
    /// RF-01: Requiere autenticación para acceder.
    /// </summary>
    [Authorize]
    public class DeptManagerController(EmployeeManagementContext context) : Controller
    {
        private const int PageSize = 10;

        // ─────────────────────────────────────────────────────────────
        // HELPER — Log de actividad
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Graba un evento en Log_Actividad para operaciones críticas del módulo.
        /// RF-13: Registro de actividad — asignar/editar/terminar vigencia de gerentes.
        /// </summary>
        private async Task RegistrarActividadAsync(string accion, string detalle)
        {
            try
            {
                context.LogActividad.Add(new LogActividad
                {
                    Usuario = User.Identity?.Name ?? "sistema",
                    Accion  = accion,
                    Entidad = "Gerentes",
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
        /// Determina si una asignación gerencial está actualmente vigente.
        /// Vigente = ToDate vacío O ToDate >= hoy.
        /// RF-05: Vigencia del cargo gerencial.
        /// </summary>
        private static bool IsVigente(DeptManager dm) =>
            string.IsNullOrEmpty(dm.ToDate) ||
            string.Compare(dm.ToDate, DateTime.Today.ToString("yyyy-MM-dd")) >= 0;

        /// <summary>
        /// Verifica si el departamento ya tiene otro manager activo cuyo período
        /// se solapa con [fromDate, toDate], excluyendo la combinación (excludeEmpNo, deptNo).
        /// RF-05: Un solo manager activo por departamento en una fecha dada.
        /// RF-11: Impedir solapamientos de gerencia en el mismo departamento.
        /// </summary>
        private async Task<bool> HasActiveManagerAsync(
            int deptNo, int excludeEmpNo, string fromDate, string? toDate)
        {
            var effectiveTo = string.IsNullOrEmpty(toDate) ? "9999-12-31" : toDate;

            var existing = await context.DeptManagers
                .Where(dm => dm.DeptNo == deptNo && dm.EmpNo != excludeEmpNo)
                .ToListAsync();

            return existing.Any(dm =>
            {
                var existingTo = string.IsNullOrEmpty(dm.ToDate) ? "9999-12-31" : dm.ToDate;
                // Solapamiento: inicio1 <= fin2 AND fin1 >= inicio2
                return string.Compare(fromDate, existingTo) <= 0 &&
                       string.Compare(effectiveTo, dm.FromDate) >= 0;
            });
        }

        /// <summary>
        /// Carga los SelectList de empleados activos y departamentos activos en ViewBag.
        /// RF-05: Solo se pueden asignar empleados y departamentos activos como gerentes.
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

            ViewBag.Employees  = new SelectList(employees,  "EmpNo",  "Display", selectedEmpNo);
            ViewBag.Departments = new SelectList(departments, "DeptNo", "Display", selectedDeptNo);
        }

        // ─────────────────────────────────────────────────────────────
        // INDEX — listado paginado con búsqueda y filtro de vigencia
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra la lista de gerentes de departamento con paginación, búsqueda por
        /// nombre/CI del empleado o nombre de departamento, y filtro por vigencia.
        /// RF-05: Consultar gerentes de departamento.
        /// RF-10: Búsqueda por texto y filtros por campos clave.
        /// RNF-01: Paginación de 10 registros por página.
        /// </summary>
        public async Task<IActionResult> Index(string? search, string? status, int page = 1)
        {
            var query = context.DeptManagers
                .Include(dm => dm.Employee)
                .Include(dm => dm.Department)
                .AsQueryable();

            // RF-10: Búsqueda por nombre/CI del empleado o nombre del departamento
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(dm =>
                    dm.Employee.FirstName.ToLower().Contains(term)  ||
                    dm.Employee.LastName.ToLower().Contains(term)   ||
                    dm.Employee.Ci.ToLower().Contains(term)         ||
                    dm.Department.DeptName.ToLower().Contains(term));
            }

            // Cargar en memoria para filtrar vigencia (ToDate es string)
            var all = await query
                .OrderBy(dm => dm.Department.DeptName)
                .ThenBy(dm => dm.FromDate)
                .ToListAsync();

            // Filtro por vigencia
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            var filtered = (status switch
            {
                "inactivo" => all.Where(dm =>
                    !string.IsNullOrEmpty(dm.ToDate) &&
                    string.Compare(dm.ToDate, today) < 0),
                "activo" or null => all.Where(IsVigente),
                _ => all.AsEnumerable()   // "" → todos
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
        /// Muestra el detalle de una asignación gerencial.
        /// RF-05: Consultar gerente de departamento.
        /// </summary>
        public async Task<IActionResult> Details(int empNo, int deptNo)
        {
            var manager = await context.DeptManagers
                .Include(dm => dm.Employee)
                .Include(dm => dm.Department)
                .FirstOrDefaultAsync(dm => dm.EmpNo == empNo && dm.DeptNo == deptNo);

            if (manager == null) return NotFound();
            return View(manager);
        }

        // ─────────────────────────────────────────────────────────────
        // CREATE
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el formulario para asignar un nuevo gerente a un departamento.
        /// RF-05: Alta de gerente de departamento.
        /// </summary>
        public async Task<IActionResult> Create()
        {
            await LoadSelectListsAsync();
            return View(new DeptManagerViewModel
            {
                FromDate = DateTime.Today.ToString("yyyy-MM-dd")
            });
        }

        /// <summary>
        /// Procesa la asignación de un nuevo gerente validando que no exista ya la
        /// combinación empleado–departamento y que el departamento no tenga otro
        /// manager activo con período solapado.
        /// RF-05: Alta de gerente con validación de exclusividad por fecha.
        /// RF-11: to_date no puede ser anterior a from_date; un solo manager activo.
        /// CU-04: Nombrar Gerente de Departamento — validar exclusividad por fecha.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DeptManagerViewModel vm)
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

            // RF-05: La combinación empleado–departamento no debe existir ya
            if (await context.DeptManagers.AnyAsync(
                    dm => dm.EmpNo == vm.EmpNo && dm.DeptNo == vm.DeptNo))
            {
                ModelState.AddModelError(string.Empty,
                    "Este empleado ya tiene un registro de gerencia en ese departamento.");
                await LoadSelectListsAsync(vm.EmpNo, vm.DeptNo);
                return View(vm);
            }

            // RF-05 / RF-11: El departamento no puede tener otro manager activo en el mismo período
            if (await HasActiveManagerAsync(vm.DeptNo, vm.EmpNo, vm.FromDate, vm.ToDate))
            {
                ModelState.AddModelError(string.Empty,
                    "El departamento ya tiene un gerente activo cuyo período se solapa con el indicado. " +
                    "Termine la vigencia del gerente actual antes de asignar uno nuevo.");
                await LoadSelectListsAsync(vm.EmpNo, vm.DeptNo);
                return View(vm);
            }

            var manager = new DeptManager
            {
                EmpNo    = vm.EmpNo,
                DeptNo   = vm.DeptNo,
                FromDate = vm.FromDate,
                ToDate   = vm.ToDate ?? string.Empty
            };

            context.DeptManagers.Add(manager);
            await context.SaveChangesAsync();

            var emp  = await context.Employees.FindAsync(vm.EmpNo);
            var dept = await context.Departments.FindAsync(vm.DeptNo);

            // RF-13: Registrar asignación de gerente en log de actividad
            await RegistrarActividadAsync("Alta",
                $"Gerente asignado: {emp!.FirstName} {emp.LastName} → \"{dept!.DeptName}\" (desde {vm.FromDate}).");

            TempData["Success"] =
                $"{emp.FirstName} {emp.LastName} asignado como gerente de \"{dept.DeptName}\" exitosamente.";

            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────
        // EDIT — solo se editan las fechas de vigencia
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el formulario de edición de fechas de una asignación gerencial.
        /// Solo se pueden modificar FromDate y ToDate; la clave (EmpNo, DeptNo) es fija.
        /// RF-05: Modificación de vigencia del cargo gerencial.
        /// </summary>
        public async Task<IActionResult> Edit(int empNo, int deptNo)
        {
            var manager = await context.DeptManagers
                .Include(dm => dm.Employee)
                .Include(dm => dm.Department)
                .FirstOrDefaultAsync(dm => dm.EmpNo == empNo && dm.DeptNo == deptNo);

            if (manager == null) return NotFound();

            var vm = new DeptManagerViewModel
            {
                EmpNo    = manager.EmpNo,
                DeptNo   = manager.DeptNo,
                FromDate = manager.FromDate,
                ToDate   = string.IsNullOrEmpty(manager.ToDate) ? null : manager.ToDate
            };

            ViewBag.EmpDisplay  = $"#{manager.Employee.EmpNo} — {manager.Employee.LastName}, {manager.Employee.FirstName}";
            ViewBag.DeptDisplay = $"#{manager.Department.DeptNo} — {manager.Department.DeptName}";

            return View(vm);
        }

        /// <summary>
        /// Procesa la edición de fechas de una asignación gerencial, validando
        /// to_date >= from_date y exclusividad de gerente activo por departamento.
        /// RF-05: Modificación de vigencia con validación de exclusividad.
        /// RF-11: to_date no puede ser anterior a from_date; un solo manager activo.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int empNo, int deptNo, DeptManagerViewModel vm)
        {
            if (empNo != vm.EmpNo || deptNo != vm.DeptNo) return BadRequest();

            var manager = await context.DeptManagers
                .Include(dm => dm.Employee)
                .Include(dm => dm.Department)
                .FirstOrDefaultAsync(dm => dm.EmpNo == empNo && dm.DeptNo == deptNo);

            if (manager == null) return NotFound();

            void SetDisplayBags()
            {
                ViewBag.EmpDisplay  = $"#{manager.Employee.EmpNo} — {manager.Employee.LastName}, {manager.Employee.FirstName}";
                ViewBag.DeptDisplay = $"#{manager.Department.DeptNo} — {manager.Department.DeptName}";
            }

            if (!ModelState.IsValid) { SetDisplayBags(); return View(vm); }

            // RF-11: to_date no puede ser anterior a from_date
            if (!string.IsNullOrEmpty(vm.ToDate) &&
                string.Compare(vm.ToDate, vm.FromDate) < 0)
            {
                ModelState.AddModelError(nameof(vm.ToDate),
                    "La fecha hasta no puede ser anterior a la fecha desde.");
                SetDisplayBags();
                return View(vm);
            }

            // RF-05 / RF-11: Exclusividad de gerente activo en el departamento
            if (await HasActiveManagerAsync(vm.DeptNo, vm.EmpNo, vm.FromDate, vm.ToDate))
            {
                ModelState.AddModelError(string.Empty,
                    "El departamento ya tiene otro gerente activo cuyo período se solapa con el indicado.");
                SetDisplayBags();
                return View(vm);
            }

            manager.FromDate = vm.FromDate;
            manager.ToDate   = vm.ToDate ?? string.Empty;
            await context.SaveChangesAsync();

            // RF-13: Registrar edición de gerencia en log de actividad
            await RegistrarActividadAsync("Edición",
                $"Gerencia: {manager.Employee.FirstName} {manager.Employee.LastName} → \"{manager.Department.DeptName}\" actualizada " +
                $"(desde {manager.FromDate}{(string.IsNullOrEmpty(manager.ToDate) ? "" : $" hasta {manager.ToDate}")}).");

            TempData["Success"] =
                $"Gerencia de {manager.Employee.FirstName} {manager.Employee.LastName} " +
                $"en \"{manager.Department.DeptName}\" actualizada exitosamente.";

            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────
        // END ASSIGNMENT — terminar vigencia del cargo gerencial
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Termina la vigencia del cargo gerencial estableciendo ToDate = hoy.
        /// RF-05: Terminar vigencia de gerente de departamento.
        /// CU-04: El sistema finaliza la vigencia del gerente actual cuando se asigna uno nuevo.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EndAssignment(int empNo, int deptNo)
        {
            var manager = await context.DeptManagers
                .Include(dm => dm.Employee)
                .Include(dm => dm.Department)
                .FirstOrDefaultAsync(dm => dm.EmpNo == empNo && dm.DeptNo == deptNo);

            if (manager == null) return NotFound();

            manager.ToDate = DateTime.Today.ToString("yyyy-MM-dd");
            await context.SaveChangesAsync();

            // RF-13: Registrar término de vigencia gerencial en log de actividad
            await RegistrarActividadAsync("Término de vigencia",
                $"Gerencia: {manager.Employee.FirstName} {manager.Employee.LastName} → \"{manager.Department.DeptName}\" finalizada al {manager.ToDate}.");

            TempData["Warning"] =
                $"Vigencia de {manager.Employee.FirstName} {manager.Employee.LastName} " +
                $"como gerente de \"{manager.Department.DeptName}\" finalizada al {manager.ToDate}.";

            return RedirectToAction(nameof(Index));
        }
    }
}
