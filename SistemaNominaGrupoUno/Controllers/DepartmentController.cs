using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaNominaGrupoUno.Context;
using SistemaNominaGrupoUno.Context.DTOS;

namespace SistemaNominaGrupoUno.Controllers
{
    /// <summary>
    /// Controlador para la gestión de departamentos (ABM: alta, baja lógica, modificación, consulta).
    /// RF-03: Crear, editar, consultar y desactivar departamentos sin borrado físico.
    /// RF-10: Todas las pantallas de consulta incluyen búsqueda por texto y filtros por campos clave.
    /// RNF-01: Listados paginados (10 filas por página).
    /// RNF-03: Documentación XML en controladores y modelos.
    /// RF-01: Requiere autenticación para acceder.
    /// </summary>
    [Authorize]
    public class DepartmentController(EmployeeManagementContext context) : Controller
    {
        private const int PageSize = 10;

        // ─────────────────────────────────────────────────────────────
        // HELPER — Log de actividad
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Graba un evento en Log_Actividad para operaciones críticas del módulo.
        /// RF-13: Registro de actividad — crear/editar/baja lógica de departamentos.
        /// </summary>
        private async Task RegistrarActividadAsync(string accion, string detalle)
        {
            try
            {
                context.LogActividad.Add(new LogActividad
                {
                    Usuario = User.Identity?.Name ?? "sistema",
                    Accion  = accion,
                    Entidad = "Departamentos",
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
        // INDEX — listado paginado con búsqueda y filtro de estado
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra la lista de departamentos con paginación, búsqueda por número/nombre
        /// y filtro opcional por estado activo/inactivo.
        /// RF-03: Consultar departamentos.
        /// RF-10: Búsqueda por texto y filtro por estado.
        /// RNF-01: Paginación de 10 registros por página.
        /// </summary>
        public async Task<IActionResult> Index(string? search, string? status, int page = 1)
        {
            var query = context.Departments.AsQueryable();

            // Filtro por estado:
            //   null o "activo" → solo activos (comportamiento por defecto)
            //   "inactivo"      → solo inactivos
            //   ""              → todos (sin filtro)
            if (status == "inactivo")
                query = query.Where(d => !d.IsActive);
            else if (status != "")
                query = query.Where(d => d.IsActive);

            // RF-10: Búsqueda por número de departamento o nombre
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(d =>
                    d.DeptName.ToLower().Contains(term) ||
                    d.DeptNo.ToString().Contains(term));
            }

            int totalItems = await query.CountAsync();
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
            page = Math.Clamp(page, 1, totalPages);

            var departments = await query
                .OrderBy(d => d.DeptName)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            ViewBag.Search      = search;
            ViewBag.Status      = status ?? "activo";
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages  = totalPages;
            ViewBag.TotalItems  = totalItems;

            return View(departments);
        }

        // ─────────────────────────────────────────────────────────────
        // DETAILS
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el detalle completo de un departamento.
        /// RF-03: Consultar departamento.
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            var department = await context.Departments.FindAsync(id);
            if (department == null) return NotFound();
            return View(department);
        }

        // ─────────────────────────────────────────────────────────────
        // CREATE
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el formulario de alta de un nuevo departamento.
        /// RF-03: Alta de departamento.
        /// </summary>
        public IActionResult Create() => View(new DepartmentViewModel());

        /// <summary>
        /// Procesa la creación de un nuevo departamento validando que el nombre sea único.
        /// RF-03: Alta de departamento.
        /// RF-11: Validaciones de negocio — nombre único en el sistema.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DepartmentViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            // RF-11: Validar que el nombre del departamento sea único
            if (await context.Departments.AnyAsync(d => d.DeptName.ToLower() == vm.DeptName.Trim().ToLower()))
            {
                ModelState.AddModelError(nameof(vm.DeptName), "Ya existe un departamento con ese nombre.");
                return View(vm);
            }

            var department = new Departments
            {
                DeptName = vm.DeptName.Trim(),
                IsActive  = true
            };

            context.Departments.Add(department);
            await context.SaveChangesAsync();

            // RF-13: Registrar alta de departamento en log de actividad
            await RegistrarActividadAsync("Alta",
                $"Departamento #{department.DeptNo} — \"{department.DeptName}\" dado de alta.");

            TempData["Success"] = $"Departamento \"{department.DeptName}\" creado exitosamente (N° {department.DeptNo}).";
            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────
        // EDIT
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el formulario de edición de un departamento existente.
        /// RF-03: Modificación de departamento.
        /// </summary>
        public async Task<IActionResult> Edit(int id)
        {
            var department = await context.Departments.FindAsync(id);
            if (department == null) return NotFound();

            var vm = new DepartmentViewModel
            {
                DeptNo   = department.DeptNo,
                DeptName = department.DeptName
            };

            return View(vm);
        }

        /// <summary>
        /// Procesa la modificación de un departamento, validando unicidad de nombre
        /// excluyendo el propio registro.
        /// RF-03: Modificación de departamento.
        /// RF-11: Validaciones de negocio — nombre único excluyendo el registro actual.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DepartmentViewModel vm)
        {
            if (id != vm.DeptNo) return BadRequest();
            if (!ModelState.IsValid) return View(vm);

            var department = await context.Departments.FindAsync(id);
            if (department == null) return NotFound();

            // RF-11: Unicidad del nombre excluyendo el propio registro
            if (await context.Departments.AnyAsync(d =>
                    d.DeptName.ToLower() == vm.DeptName.Trim().ToLower() &&
                    d.DeptNo != id))
            {
                ModelState.AddModelError(nameof(vm.DeptName), "Ya existe otro departamento con ese nombre.");
                return View(vm);
            }

            department.DeptName = vm.DeptName.Trim();
            await context.SaveChangesAsync();

            // RF-13: Registrar modificación de departamento en log de actividad
            await RegistrarActividadAsync("Edición",
                $"Departamento #{department.DeptNo} — \"{department.DeptName}\" modificado.");

            TempData["Success"] = $"Departamento \"{department.DeptName}\" actualizado exitosamente.";
            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────
        // DEACTIVATE / ACTIVATE (baja y alta lógica)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Realiza la baja lógica de un departamento (IsActive = false). Sin borrado físico.
        /// RF-03: Desactivar departamento sin borrado físico.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id)
        {
            var department = await context.Departments.FindAsync(id);
            if (department == null) return NotFound();

            department.IsActive = false;
            await context.SaveChangesAsync();

            // RF-13: Registrar baja lógica de departamento en log de actividad
            await RegistrarActividadAsync("Baja lógica",
                $"Departamento #{department.DeptNo} — \"{department.DeptName}\" dado de baja.");

            TempData["Warning"] = $"Departamento \"{department.DeptName}\" dado de baja del sistema.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Reactiva un departamento previamente dado de baja (IsActive = true).
        /// RF-03: Reactivación de departamento.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Activate(int id)
        {
            var department = await context.Departments.FindAsync(id);
            if (department == null) return NotFound();

            department.IsActive = true;
            await context.SaveChangesAsync();

            // RF-13: Registrar reactivación de departamento en log de actividad
            await RegistrarActividadAsync("Reactivación",
                $"Departamento #{department.DeptNo} — \"{department.DeptName}\" reactivado.");

            TempData["Success"] = $"Departamento \"{department.DeptName}\" reactivado exitosamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}
