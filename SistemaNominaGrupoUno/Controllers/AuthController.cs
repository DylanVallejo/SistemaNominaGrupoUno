using System.Security.Claims;
using BCrypt.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaNominaGrupoUno.Context;
using SistemaNominaGrupoUno.Context.DTOS;

namespace SistemaNominaGrupoUno.Controllers
{
    /// <summary>
    /// Controlador de autenticación del sistema.
    /// RF-01: Inicio y cierre de sesión con validación de credenciales.
    ///        Bloqueo de acceso a credenciales incorrectas.
    /// RNF-02: La contraseña nunca se expone; se verifica con BCrypt.Verify.
    /// RNF-03: Documentación XML en controladores.
    /// </summary>
    public class AuthController(EmployeeManagementContext context) : Controller
    {
        // ─────────────────────────────────────────────────────────────
        // LOGIN
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el formulario de inicio de sesión.
        /// RF-01: Punto de entrada para usuarios no autenticados.
        /// </summary>
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            // Si ya está autenticado redirigir al Home
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        /// <summary>
        /// Procesa el inicio de sesión: valida credenciales con BCrypt y,
        /// si son correctas, emite la cookie de autenticación con los claims
        /// de usuario, rol y nombre completo del empleado.
        /// RF-01: Validar credenciales y generar sesión autenticada.
        /// RNF-02: BCrypt.Verify compara la clave enviada con el hash almacenado.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            // RF-01: Buscar usuario por nombre de usuario
            var user = await context.Users
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.Usuario == vm.Usuario);

            // RNF-02: Verificar contraseña con BCrypt
            if (user == null || !BCrypt.Net.BCrypt.Verify(vm.Clave, user.Clave))
            {
                ModelState.AddModelError(string.Empty,
                    "Usuario o contraseña incorrectos. Verifique sus credenciales.");
                return View(vm);
            }

            // RF-01: Construir los claims de la sesión
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name,   user.Usuario),
                new(ClaimTypes.Role,   user.Rol),
                new("EmpNo",           user.EmpNo.ToString()),
                new("NombreCompleto",  $"{user.Employee.FirstName} {user.Employee.LastName}")
            };

            var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = false });

            // RF-13: Registrar login en log de actividad
            await RegistrarActividadAsync(user.Usuario, "Login",
                "Autenticación", $"Inicio de sesión exitoso del usuario '{user.Usuario}'.");

            // RF-01: Redirigir a la URL original o al Home
            if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
                return Redirect(vm.ReturnUrl);

            return RedirectToAction("Index", "Home");
        }

        // ─────────────────────────────────────────────────────────────
        // LOGOUT
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Cierra la sesión del usuario actual, elimina la cookie y
        /// registra el evento en el log de actividad.
        /// RF-01: Cierre de sesión seguro.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var usuario = User.Identity?.Name ?? "desconocido";

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // RF-13: Registrar logout en log de actividad
            await RegistrarActividadAsync(usuario, "Logout",
                "Autenticación", $"Cierre de sesión del usuario '{usuario}'.");

            return RedirectToAction(nameof(Login));
        }

        // ─────────────────────────────────────────────────────────────
        // ACCESO DENEGADO
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra la página de acceso denegado cuando el usuario no
        /// tiene el rol requerido para una acción.
        /// RF-01: Gestión de acceso denegado por rol.
        /// </summary>
        [HttpGet]
        public IActionResult AccesoDenegado() => View();

        // ─────────────────────────────────────────────────────────────
        // HELPER
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Graba un evento en Log_Actividad (si la tabla existe).
        /// RF-13: Registro de accesos y cierres de sesión.
        /// </summary>
        private async Task RegistrarActividadAsync(
            string usuario, string accion, string entidad, string detalle)
        {
            try
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
            catch
            {
                // No interrumpir el flujo de autenticación si el log falla
            }
        }
    }
}
