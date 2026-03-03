using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaNominaGrupoUno.Context;
using SistemaNominaGrupoUno.Context.DTOS;

namespace SistemaNominaGrupoUno.Controllers
{
    /// <summary>
    /// Controlador para la gestión del historial de títulos y cargos de empleados.
    /// RF-06: Registrar títulos por empleado con histórico (from_date/to_date).
    ///        Permitir múltiples títulos en el tiempo, sin solapamiento.
    /// RF-10: Búsqueda por texto y filtros en pantalla de consulta.
    /// RF-11: Validar que to_date no sea anterior a from_date; impedir solapamientos.
    /// RNF-01: Listados paginados (10 filas por página).
    /// RNF-03: Documentación XML en controladores y modelos.
    /// RF-01: Requiere autenticación para acceder.
    /// </summary>
    [Authorize]
    public class TitleController(EmployeeManagementContext context) : Controller
    {
        private const int PageSize = 10;

        // ─────────────────────────────────────────────────────────────
        // HELPER — Log de actividad
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Graba un evento en Log_Actividad para operaciones críticas del módulo.
        /// RF-13: Registro de actividad — alta/edición/término de vigencia de títulos.
        /// </summary>
        private async Task RegistrarActividadAsync(string accion, string detalle)
        {
            try
            {
                context.LogActividad.Add(new LogActividad
                {
                    Usuario = User.Identity?.Name ?? "sistema",
                    Accion  = accion,
                    Entidad = "Títulos",
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
        /// Determina si un título está actualmente vigente.
        /// Vigente = ToDate vacío O ToDate >= hoy.
        /// RF-06: Vigencia del cargo.
        /// </summary>
        private static bool IsVigente(Titles t) =>
            string.IsNullOrEmpty(t.ToDate) ||
            string.Compare(t.ToDate, DateTime.Today.ToString("yyyy-MM-dd")) >= 0;

        /// <summary>
        /// Verifica si el período [fromDate, toDate] del empleado se solapa con
        /// algún título existente, excluyendo el registro identificado por excludeFromDate.
        /// RF-06: Múltiples títulos permitidos, pero sin solapamiento en el tiempo.
        /// RF-11: Impedir solapamientos de títulos para el mismo empleado.
        /// </summary>
        private async Task<bool> HasOverlapAsync(
            int empNo, string excludeFromDate, string fromDate, string? toDate)
        {
            var effectiveTo = string.IsNullOrEmpty(toDate) ? "9999-12-31" : toDate;

            var existing = await context.Titles
                .Where(t => t.EmpNo == empNo && t.FromDate != excludeFromDate)
                .ToListAsync();

            return existing.Any(t =>
            {
                var existingTo = string.IsNullOrEmpty(t.ToDate) ? "9999-12-31" : t.ToDate;
                // Solapamiento: inicio1 <= fin2 AND fin1 >= inicio2
                return string.Compare(fromDate, existingTo) <= 0 &&
                       string.Compare(effectiveTo, t.FromDate) >= 0;
            });
        }

        /// <summary>
        /// Carga el SelectList de empleados activos en ViewBag para el formulario de alta.
        /// RF-06: Solo se pueden registrar títulos para empleados activos.
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
        /// Muestra el historial de títulos con paginación, búsqueda por nombre de
        /// empleado, cédula o nombre del título, y filtro por vigencia.
        /// RF-06: Consultar historial de títulos.
        /// RF-10: Búsqueda por texto y filtros por campos clave.
        /// RNF-01: Paginación de 10 registros por página.
        /// </summary>
        public async Task<IActionResult> Index(string? search, string? status, int page = 1)
        {
            var query = context.Titles
                .Include(t => t.Employee)
                .AsQueryable();

            // RF-10: Búsqueda por nombre/CI del empleado o por título
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(t =>
                    t.Employee.FirstName.ToLower().Contains(term) ||
                    t.Employee.LastName.ToLower().Contains(term)  ||
                    t.Employee.Ci.ToLower().Contains(term)        ||
                    t.Title.ToLower().Contains(term));
            }

            // Cargar en memoria para filtrar vigencia (ToDate es string)
            var all = await query
                .OrderBy(t => t.Employee.LastName)
                .ThenByDescending(t => t.FromDate)
                .ToListAsync();

            // Filtro por vigencia
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            var filtered = (status switch
            {
                "inactivo" => all.Where(t =>
                    !string.IsNullOrEmpty(t.ToDate) &&
                    string.Compare(t.ToDate, today) < 0),
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
        /// Muestra el detalle de un registro de título/cargo.
        /// La clave primaria es (EmpNo, FromDate).
        /// RF-06: Consultar registro de título.
        /// </summary>
        public async Task<IActionResult> Details(int empNo, string fromDate)
        {
            var title = await context.Titles
                .Include(t => t.Employee)
                .FirstOrDefaultAsync(t => t.EmpNo == empNo && t.FromDate == fromDate);

            if (title == null) return NotFound();
            return View(title);
        }

        // ─────────────────────────────────────────────────────────────
        // CREATE
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el formulario de alta de un nuevo título para un empleado.
        /// RF-06: Alta de título/cargo con fecha de inicio.
        /// </summary>
        public async Task<IActionResult> Create()
        {
            await LoadEmployeesAsync();
            return View(new TitleViewModel
            {
                FromDate = DateTime.Today.ToString("yyyy-MM-dd")
            });
        }

        /// <summary>
        /// Procesa la creación de un nuevo título validando que no exista ya la
        /// combinación (EmpNo, FromDate) y que no haya solapamiento de períodos.
        /// RF-06: Alta de título con validación de solapamientos históricos.
        /// RF-11: to_date no puede ser anterior a from_date; sin solapamientos.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TitleViewModel vm)
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

            // RF-06: La combinación (EmpNo, FromDate) no puede existir ya (PK)
            if (await context.Titles.AnyAsync(
                    t => t.EmpNo == vm.EmpNo && t.FromDate == vm.FromDate))
            {
                ModelState.AddModelError(nameof(vm.FromDate),
                    "Ya existe un título registrado para este empleado en esa fecha de inicio.");
                await LoadEmployeesAsync(vm.EmpNo);
                return View(vm);
            }

            // RF-06 / RF-11: Solapamiento con otros títulos del mismo empleado
            if (await HasOverlapAsync(vm.EmpNo, string.Empty, vm.FromDate, vm.ToDate))
            {
                ModelState.AddModelError(string.Empty,
                    "El empleado ya tiene un título registrado cuyo período se solapa con el indicado.");
                await LoadEmployeesAsync(vm.EmpNo);
                return View(vm);
            }

            var title = new Titles
            {
                EmpNo    = vm.EmpNo,
                Title    = vm.TitleName.Trim(),
                FromDate = vm.FromDate,
                ToDate   = vm.ToDate ?? string.Empty
            };

            context.Titles.Add(title);
            await context.SaveChangesAsync();

            var emp = await context.Employees.FindAsync(vm.EmpNo);

            // RF-13: Registrar alta de título en log de actividad
            await RegistrarActividadAsync("Alta",
                $"Título \"{title.Title}\" registrado para {emp!.FirstName} {emp.LastName} (desde {title.FromDate}).");

            TempData["Success"] =
                $"Título \"{title.Title}\" registrado para {emp.FirstName} {emp.LastName} exitosamente.";

            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────
        // EDIT — se puede modificar Title y ToDate (FromDate es parte de PK)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el formulario de edición de un título existente.
        /// Se puede modificar el nombre del cargo y ToDate; FromDate es fijo (parte de PK).
        /// RF-06: Modificación de título/cargo.
        /// </summary>
        public async Task<IActionResult> Edit(int empNo, string fromDate)
        {
            var title = await context.Titles
                .Include(t => t.Employee)
                .FirstOrDefaultAsync(t => t.EmpNo == empNo && t.FromDate == fromDate);

            if (title == null) return NotFound();

            var vm = new TitleViewModel
            {
                EmpNo            = title.EmpNo,
                TitleName        = title.Title,
                FromDate         = title.FromDate,
                OriginalFromDate = title.FromDate,
                ToDate           = string.IsNullOrEmpty(title.ToDate) ? null : title.ToDate
            };

            ViewBag.EmpDisplay = $"#{title.Employee.EmpNo} — {title.Employee.LastName}, {title.Employee.FirstName}";
            return View(vm);
        }

        /// <summary>
        /// Procesa la edición del nombre del cargo y ToDate, validando que el período
        /// actualizado no genere solapamiento con otros títulos del mismo empleado.
        /// RF-06: Modificación de título con validación de solapamientos.
        /// RF-11: to_date no puede ser anterior a from_date; sin solapamientos.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int empNo, string fromDate, TitleViewModel vm)
        {
            if (empNo != vm.EmpNo || fromDate != vm.OriginalFromDate) return BadRequest();

            var title = await context.Titles
                .Include(t => t.Employee)
                .FirstOrDefaultAsync(t => t.EmpNo == empNo && t.FromDate == fromDate);

            if (title == null) return NotFound();

            void SetDisplayBag() =>
                ViewBag.EmpDisplay = $"#{title.Employee.EmpNo} — {title.Employee.LastName}, {title.Employee.FirstName}";

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

            // RF-06 / RF-11: Solapamiento con otros títulos del mismo empleado
            if (await HasOverlapAsync(vm.EmpNo, vm.OriginalFromDate, vm.FromDate, vm.ToDate))
            {
                ModelState.AddModelError(string.Empty,
                    "El empleado ya tiene otro título cuyo período se solapa con el indicado.");
                SetDisplayBag();
                return View(vm);
            }

            title.Title   = vm.TitleName.Trim();
            title.ToDate  = vm.ToDate ?? string.Empty;
            await context.SaveChangesAsync();

            // RF-13: Registrar edición de título en log de actividad
            await RegistrarActividadAsync("Edición",
                $"Título \"{title.Title}\" de {title.Employee.FirstName} {title.Employee.LastName} modificado " +
                $"(hasta {(string.IsNullOrEmpty(title.ToDate) ? "indefinido" : title.ToDate)}).");

            TempData["Success"] =
                $"Título \"{title.Title}\" de {title.Employee.FirstName} {title.Employee.LastName} actualizado exitosamente.";

            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────
        // END ASSIGNMENT — terminar vigencia del título
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Termina la vigencia de un título estableciendo ToDate = hoy.
        /// RF-06: Terminar vigencia de título/cargo activo.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EndAssignment(int empNo, string fromDate)
        {
            var title = await context.Titles
                .Include(t => t.Employee)
                .FirstOrDefaultAsync(t => t.EmpNo == empNo && t.FromDate == fromDate);

            if (title == null) return NotFound();

            title.ToDate = DateTime.Today.ToString("yyyy-MM-dd");
            await context.SaveChangesAsync();

            // RF-13: Registrar término de vigencia de título en log de actividad
            await RegistrarActividadAsync("Término de vigencia",
                $"Título \"{title.Title}\" de {title.Employee.FirstName} {title.Employee.LastName} finalizado al {title.ToDate}.");

            TempData["Warning"] =
                $"Vigencia del título \"{title.Title}\" de {title.Employee.FirstName} {title.Employee.LastName} " +
                $"finalizada al {title.ToDate}.";

            return RedirectToAction(nameof(Index));
        }
    }
}
