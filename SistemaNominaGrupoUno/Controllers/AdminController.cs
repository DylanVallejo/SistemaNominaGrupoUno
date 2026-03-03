using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SistemaNominaGrupoUno.Context;
using SistemaNominaGrupoUno.Context.DTOS;

namespace SistemaNominaGrupoUno.Controllers
{
    /// <summary>
    /// Controlador de administración del sistema.
    /// RF-12: Gestión de usuarios con roles (Administrador / RRHH).
    /// RF-13: Log de actividad — accesos y operaciones críticas.
    /// RF-14: Configuración del sistema (parámetros básicos).
    /// RNF-02: Contraseñas hasheadas con BCrypt antes de persistir.
    /// RNF-03: Documentación XML en controladores.
    /// RF-01: Acceso restringido al rol Administrador.
    /// </summary>
    [Authorize(Roles = "Administrador")]
    public class AdminController(EmployeeManagementContext context) : Controller
    {
        private const int PageSize = 10;

        // ─────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Carga en ViewBag los empleados activos que aún no tienen usuario asignado,
        /// incluyendo el empleado ya seleccionado (para ediciones).
        /// RF-12: Solo empleados activos pueden tener usuario.
        /// </summary>
        private async Task LoadEmployeesAsync(int? excludeEmpNo = null)
        {
            // Empleados con usuario ya asignado (excepto el que estamos editando)
            var conUsuario = await context.Users
                .Select(u => u.EmpNo)
                .ToListAsync();

            if (excludeEmpNo.HasValue)
                conUsuario.Remove(excludeEmpNo.Value);

            var disponibles = await context.Employees
                .Where(e => e.IsActive && !conUsuario.Contains(e.EmpNo))
                .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                .Select(e => new { e.EmpNo, Display = $"#{e.EmpNo} — {e.LastName}, {e.FirstName}" })
                .ToListAsync();

            ViewBag.Employees = new SelectList(disponibles, "EmpNo", "Display");
        }

        /// <summary>
        /// Lista de roles disponibles en el sistema.
        /// RF-12: Administrador (acceso total) y RRHH (acceso operativo).
        /// </summary>
        private static List<SelectListItem> RolesDisponibles() =>
        [
            new SelectListItem("Administrador", "Administrador"),
            new SelectListItem("RRHH",          "RRHH"),
        ];

        // ─────────────────────────────────────────────────────────────
        // INDEX — listado de usuarios
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el listado de usuarios del sistema con paginación y búsqueda.
        /// RF-12: Consultar usuarios registrados y sus roles.
        /// RNF-01: Paginación de 10 registros por página.
        /// </summary>
        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            var query = context.Users
                .Include(u => u.Employee)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(u =>
                    u.Usuario.ToLower().Contains(term) ||
                    u.Rol.ToLower().Contains(term) ||
                    u.Employee.FirstName.ToLower().Contains(term) ||
                    u.Employee.LastName.ToLower().Contains(term));
            }

            int totalItems = await query.CountAsync();
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
            page = Math.Clamp(page, 1, totalPages);

            var items = await query
                .OrderBy(u => u.Usuario)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            ViewBag.Search      = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages  = totalPages;
            ViewBag.TotalItems  = totalItems;

            return View(items);
        }

        // ─────────────────────────────────────────────────────────────
        // DETAILS
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el detalle de un usuario del sistema.
        /// RF-12: Consultar datos de un usuario (sin exponer la clave).
        /// </summary>
        public async Task<IActionResult> Details(int empNo)
        {
            var user = await context.Users
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.EmpNo == empNo);

            if (user == null) return NotFound();
            return View(user);
        }

        // ─────────────────────────────────────────────────────────────
        // CREATE
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el formulario de alta de un nuevo usuario.
        /// RF-12: Crear usuario vinculado a un empleado activo.
        /// </summary>
        public async Task<IActionResult> Create()
        {
            await LoadEmployeesAsync();
            ViewBag.Roles = RolesDisponibles();
            return View(new UserViewModel());
        }

        /// <summary>
        /// Procesa el alta de un nuevo usuario hasheando la clave con BCrypt.
        /// RF-12: Alta de usuario con rol asignado.
        /// RNF-02: Contraseña hasheada con BCrypt (work factor 12).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserViewModel vm)
        {
            // Clave obligatoria en el alta
            if (string.IsNullOrWhiteSpace(vm.Clave))
                ModelState.AddModelError(nameof(vm.Clave),
                    "La contraseña es obligatoria al crear un usuario.");

            if (!ModelState.IsValid)
            {
                await LoadEmployeesAsync();
                ViewBag.Roles = RolesDisponibles();
                return View(vm);
            }

            // RF-12: Verificar que el empleado no tenga ya un usuario
            if (await context.Users.AnyAsync(u => u.EmpNo == vm.EmpNo))
            {
                ModelState.AddModelError(nameof(vm.EmpNo),
                    "El empleado seleccionado ya tiene un usuario asignado.");
                await LoadEmployeesAsync();
                ViewBag.Roles = RolesDisponibles();
                return View(vm);
            }

            // RF-12: Nombre de usuario único
            if (await context.Users.AnyAsync(u => u.Usuario == vm.Usuario))
            {
                ModelState.AddModelError(nameof(vm.Usuario),
                    "El nombre de usuario ya está en uso.");
                await LoadEmployeesAsync();
                ViewBag.Roles = RolesDisponibles();
                return View(vm);
            }

            var user = new Users
            {
                EmpNo   = vm.EmpNo,
                Usuario = vm.Usuario.Trim(),
                // RNF-02: Hash BCrypt con work factor 12
                Clave   = BCrypt.Net.BCrypt.HashPassword(vm.Clave!, workFactor: 12),
                Rol     = vm.Rol
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();

            // RF-13: Registrar con el usuario autenticado (RF-01)
            await RegistrarActividadAsync(
                User.Identity?.Name ?? "sistema", "Alta",
                "Usuarios", $"Usuario '{vm.Usuario}' creado con rol '{vm.Rol}'.");

            TempData["Success"] = $"Usuario '{vm.Usuario}' creado exitosamente.";
            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────
        // EDIT — se puede cambiar usuario y rol; clave por separado
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el formulario de edición de un usuario (usuario y rol).
        /// La clave se restablece desde la acción ResetPassword.
        /// RF-12: Modificar usuario y rol sin exponer la clave.
        /// </summary>
        public async Task<IActionResult> Edit(int empNo)
        {
            var user = await context.Users
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.EmpNo == empNo);

            if (user == null) return NotFound();

            ViewBag.EmpDisplay = $"#{user.Employee.EmpNo} — {user.Employee.LastName}, {user.Employee.FirstName}";
            ViewBag.Roles = RolesDisponibles();

            return View(new UserViewModel
            {
                EmpNo   = user.EmpNo,
                Usuario = user.Usuario,
                Rol     = user.Rol
            });
        }

        /// <summary>
        /// Procesa la edición del nombre de usuario y rol.
        /// RF-12: Modificar datos del usuario sin cambiar la clave.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int empNo, UserViewModel vm)
        {
            if (empNo != vm.EmpNo) return BadRequest();

            // En edición la clave no es obligatoria — limpiar errores de clave
            ModelState.Remove(nameof(vm.Clave));
            ModelState.Remove(nameof(vm.ConfirmClave));

            var user = await context.Users
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.EmpNo == empNo);

            if (user == null) return NotFound();

            void SetDisplay()
            {
                ViewBag.EmpDisplay = $"#{user.Employee.EmpNo} — {user.Employee.LastName}, {user.Employee.FirstName}";
                ViewBag.Roles      = RolesDisponibles();
            }

            if (!ModelState.IsValid) { SetDisplay(); return View(vm); }

            // Verificar unicidad del nombre de usuario (excluyendo el actual)
            if (await context.Users.AnyAsync(
                    u => u.Usuario == vm.Usuario.Trim() && u.EmpNo != empNo))
            {
                ModelState.AddModelError(nameof(vm.Usuario),
                    "El nombre de usuario ya está en uso por otro usuario.");
                SetDisplay();
                return View(vm);
            }

            string usuarioAnterior = user.Usuario;
            user.Usuario = vm.Usuario.Trim();
            user.Rol     = vm.Rol;
            await context.SaveChangesAsync();

            await RegistrarActividadAsync(
                User.Identity?.Name ?? "sistema", "Edición",
                "Usuarios",
                $"Usuario '{usuarioAnterior}' actualizado a '{vm.Usuario}', rol: '{vm.Rol}'.");

            TempData["Success"] = $"Usuario '{vm.Usuario}' actualizado exitosamente.";
            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────
        // RESET PASSWORD
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el formulario de restablecimiento de contraseña.
        /// RNF-02: La nueva clave se hasheará con BCrypt antes de persistir.
        /// </summary>
        public async Task<IActionResult> ResetPassword(int empNo)
        {
            var user = await context.Users
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.EmpNo == empNo);

            if (user == null) return NotFound();

            ViewBag.EmpDisplay = $"#{user.Employee.EmpNo} — {user.Employee.LastName}, {user.Employee.FirstName}";
            ViewBag.Usuario    = user.Usuario;
            return View(new UserViewModel { EmpNo = empNo, Usuario = user.Usuario, Rol = user.Rol });
        }

        /// <summary>
        /// Procesa el restablecimiento de contraseña hasheando la nueva clave con BCrypt.
        /// RNF-02: Contraseña hasheada con BCrypt (work factor 12).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int empNo, UserViewModel vm)
        {
            if (empNo != vm.EmpNo) return BadRequest();

            // Solo validar campos de clave
            ModelState.Remove(nameof(vm.Usuario));
            ModelState.Remove(nameof(vm.Rol));

            if (string.IsNullOrWhiteSpace(vm.Clave))
                ModelState.AddModelError(nameof(vm.Clave),
                    "La nueva contraseña es obligatoria.");

            var user = await context.Users
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.EmpNo == empNo);

            if (user == null) return NotFound();

            void SetDisplay()
            {
                ViewBag.EmpDisplay = $"#{user.Employee.EmpNo} — {user.Employee.LastName}, {user.Employee.FirstName}";
                ViewBag.Usuario    = user.Usuario;
            }

            if (!ModelState.IsValid) { SetDisplay(); return View(vm); }

            // RNF-02: Hash BCrypt
            user.Clave = BCrypt.Net.BCrypt.HashPassword(vm.Clave!, workFactor: 12);
            await context.SaveChangesAsync();

            await RegistrarActividadAsync(
                User.Identity?.Name ?? "sistema", "Reset Password",
                "Usuarios", $"Contraseña del usuario '{user.Usuario}' restablecida.");

            TempData["Success"] = $"Contraseña de '{user.Usuario}' restablecida exitosamente.";
            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────
        // LOG DE ACTIVIDAD — RF-13
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el log de actividad del sistema con paginación y búsqueda.
        /// RF-13: Consultar registro de accesos y operaciones críticas.
        /// RNF-01: Paginación de 10 registros por página.
        /// </summary>
        public async Task<IActionResult> LogActividadView(string? search, int page = 1)
        {
            var query = context.LogActividad.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(l =>
                    l.Usuario.ToLower().Contains(term) ||
                    l.Accion.ToLower().Contains(term) ||
                    l.Entidad.ToLower().Contains(term) ||
                    l.Detalle.ToLower().Contains(term));
            }

            int totalItems = await query.CountAsync();
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
            page = Math.Clamp(page, 1, totalPages);

            var items = await query
                .OrderByDescending(l => l.Fecha)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            ViewBag.Search      = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages  = totalPages;
            ViewBag.TotalItems  = totalItems;

            return View(items);
        }

        // ─────────────────────────────────────────────────────────────
        // CONFIGURACIÓN — RF-14
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra la página de configuración del sistema.
        /// RF-14: Parámetros básicos de internacionalización y formato.
        /// </summary>
        public IActionResult Config()
        {
            var config = new ConfigSistemaViewModel
            {
                NombreEmpresa   = "Sistema Nómina Grupo Uno",
                FormatoFecha    = "yyyy-MM-dd",
                SimboloMoneda   = "$",
                LocaleCultura   = "es-UY",
                VersionSistema  = "1.0.0",
                FechaActualizacion = DateTime.Today.ToString("yyyy-MM-dd")
            };
            return View(config);
        }

        // ─────────────────────────────────────────────────────────────
        // HELPER PRIVADO — REGISTRO DE ACTIVIDAD
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Graba un registro en Log_Actividad.
        /// RF-13: Registro automático de operaciones críticas.
        /// </summary>
        private async Task RegistrarActividadAsync(
            string usuario, string accion, string entidad, string detalle)
        {
            context.LogActividad.Add(new LogActividad
            {
                Usuario = usuario,
                Accion  = accion,
                Entidad = entidad,
                Detalle = detalle,
                Fecha   = DateTime.Now
            });
            await context.SaveChangesAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // MODELO INTERNO — CONFIGURACIÓN DEL SISTEMA
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Modelo de configuración básica del sistema.
    /// RF-14: Parámetros básicos de internacionalización.
    /// </summary>
    public class ConfigSistemaViewModel
    {
        /// <summary>Nombre de la empresa o sistema.</summary>
        public string NombreEmpresa { get; set; } = string.Empty;

        /// <summary>Formato de fecha del sistema (ej: yyyy-MM-dd).</summary>
        public string FormatoFecha { get; set; } = string.Empty;

        /// <summary>Símbolo de moneda utilizado en reportes.</summary>
        public string SimboloMoneda { get; set; } = string.Empty;

        /// <summary>Código de cultura/locale (ej: es-UY, es-ES).</summary>
        public string LocaleCultura { get; set; } = string.Empty;

        /// <summary>Versión actual del sistema.</summary>
        public string VersionSistema { get; set; } = string.Empty;

        /// <summary>Fecha de la última actualización de configuración.</summary>
        public string FechaActualizacion { get; set; } = string.Empty;
    }
}
