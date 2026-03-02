using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaNominaGrupoUno.Context;
using SistemaNominaGrupoUno.Context.DTOS;

namespace SistemaNominaGrupoUno.Controllers
{
    /// <summary>
    /// Controlador para la gestión de empleados (ABM: alta, baja, modificación).
    /// RF-02: Crear, editar, consultar y desactivar empleados sin borrado físico.
    /// </summary>
    public class EmployeeController(EmployeeManagementContext context) : Controller
    {
        private const int PageSize = 10;

        // ─────────────────────────────────────────────────────────────
        // INDEX — listado paginado con búsqueda y filtro de estado
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra la lista de empleados con paginación, búsqueda por nombre/CI/correo
        /// y filtro opcional por estado activo/inactivo.
        /// </summary>
        public async Task<IActionResult> Index(string? search, string? status, int page = 1)
        {
            var query = context.Employees.AsQueryable();

            // Filtro por estado:
            //   null o "activo" → solo activos (comportamiento por defecto)
            //   "inactivo"      → solo inactivos
            //   ""              → todos (sin filtro)
            if (status == "inactivo")
                query = query.Where(e => !e.IsActive);
            else if (status != "")
                query = query.Where(e => e.IsActive);

            // Búsqueda por nombre, apellido, CI o correo
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(e =>
                    e.FirstName.ToLower().Contains(term) ||
                    e.LastName.ToLower().Contains(term)  ||
                    e.Ci.ToLower().Contains(term)        ||
                    e.Correo.ToLower().Contains(term));
            }

            int totalItems = await query.CountAsync();
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
            page = Math.Clamp(page, 1, totalPages);

            var employees = await query
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            ViewBag.Search      = search;
            ViewBag.Status      = status ?? "activo";
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages  = totalPages;
            ViewBag.TotalItems  = totalItems;

            return View(employees);
        }

        // ─────────────────────────────────────────────────────────────
        // DETAILS
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el detalle completo de un empleado.
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            var employee = await context.Employees.FindAsync(id);
            if (employee == null) return NotFound();
            return View(employee);
        }

        // ─────────────────────────────────────────────────────────────
        // CREATE
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el formulario de alta de un nuevo empleado.
        /// </summary>
        public IActionResult Create() => View(new EmployeeViewModel());

        /// <summary>
        /// Procesa la creación de un nuevo empleado validando CI y correo únicos.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            if (await context.Employees.AnyAsync(e => e.Ci == vm.Ci))
            {
                ModelState.AddModelError(nameof(vm.Ci), "Ya existe un empleado con esa cédula de identidad.");
                return View(vm);
            }

            if (await context.Employees.AnyAsync(e => e.Correo == vm.Correo))
            {
                ModelState.AddModelError(nameof(vm.Correo), "Ya existe un empleado con ese correo electrónico.");
                return View(vm);
            }

            var employee = new Employees
            {
                Ci        = vm.Ci.Trim(),
                BirthDate = vm.BirthDate,
                FirstName = vm.FirstName.Trim(),
                LastName  = vm.LastName.Trim(),
                Gender    = vm.Gender,
                HireDate  = vm.HireDate,
                Correo    = vm.Correo.Trim().ToLower(),
                IsActive  = true
            };

            context.Employees.Add(employee);
            await context.SaveChangesAsync();

            TempData["Success"] = $"Empleado {employee.FirstName} {employee.LastName} creado exitosamente (N° {employee.EmpNo}).";
            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────
        // EDIT
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el formulario de edición de un empleado existente.
        /// </summary>
        public async Task<IActionResult> Edit(int id)
        {
            var employee = await context.Employees.FindAsync(id);
            if (employee == null) return NotFound();

            var vm = new EmployeeViewModel
            {
                EmpNo     = employee.EmpNo,
                Ci        = employee.Ci,
                BirthDate = employee.BirthDate,
                FirstName = employee.FirstName,
                LastName  = employee.LastName,
                Gender    = employee.Gender,
                HireDate  = employee.HireDate,
                Correo    = employee.Correo
            };

            return View(vm);
        }

        /// <summary>
        /// Procesa la modificación de datos de un empleado, validando unicidad de CI y correo.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EmployeeViewModel vm)
        {
            if (id != vm.EmpNo) return BadRequest();
            if (!ModelState.IsValid) return View(vm);

            var employee = await context.Employees.FindAsync(id);
            if (employee == null) return NotFound();

            // Unicidad de CI excluyendo el propio registro
            if (await context.Employees.AnyAsync(e => e.Ci == vm.Ci && e.EmpNo != id))
            {
                ModelState.AddModelError(nameof(vm.Ci), "Ya existe otro empleado con esa cédula de identidad.");
                return View(vm);
            }

            // Unicidad de correo excluyendo el propio registro
            if (await context.Employees.AnyAsync(e => e.Correo == vm.Correo && e.EmpNo != id))
            {
                ModelState.AddModelError(nameof(vm.Correo), "Ya existe otro empleado con ese correo electrónico.");
                return View(vm);
            }

            employee.Ci        = vm.Ci.Trim();
            employee.BirthDate = vm.BirthDate;
            employee.FirstName = vm.FirstName.Trim();
            employee.LastName  = vm.LastName.Trim();
            employee.Gender    = vm.Gender;
            employee.HireDate  = vm.HireDate;
            employee.Correo    = vm.Correo.Trim().ToLower();

            await context.SaveChangesAsync();

            TempData["Success"] = $"Empleado {employee.FirstName} {employee.LastName} actualizado exitosamente.";
            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────
        // DEACTIVATE / ACTIVATE (baja y alta lógica)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Realiza la baja lógica de un empleado (IsActive = false). Sin borrado físico.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id)
        {
            var employee = await context.Employees.FindAsync(id);
            if (employee == null) return NotFound();

            employee.IsActive = false;
            await context.SaveChangesAsync();

            TempData["Warning"] = $"Empleado {employee.FirstName} {employee.LastName} dado de baja del sistema.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Reactiva un empleado previamente dado de baja (IsActive = true).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Activate(int id)
        {
            var employee = await context.Employees.FindAsync(id);
            if (employee == null) return NotFound();

            employee.IsActive = true;
            await context.SaveChangesAsync();

            TempData["Success"] = $"Empleado {employee.FirstName} {employee.LastName} reactivado exitosamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}
