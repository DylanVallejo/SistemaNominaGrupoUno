using System.ComponentModel.DataAnnotations;

namespace SistemaNominaGrupoUno.Context.DTOS
{
    /// <summary>
    /// ViewModel para la gestión de usuarios del sistema.
    /// RF-01: Credenciales para autenticación.
    /// RF-12: Rol del usuario (Administrador / RRHH).
    /// RNF-02: La clave se hashea antes de persistir; nunca se expone.
    /// RNF-03: Documentación XML en ViewModels.
    /// </summary>
    public class UserViewModel
    {
        /// <summary>
        /// FK al empleado propietario del usuario (PK de la tabla Users).
        /// RF-01: Cada usuario corresponde a un empleado activo.
        /// </summary>
        [Required(ErrorMessage = "Debe seleccionar un empleado.")]
        [Display(Name = "Empleado")]
        public int EmpNo { get; set; }

        /// <summary>
        /// Nombre de usuario para el inicio de sesión. Debe ser único.
        /// RF-01: Identificador de acceso al sistema.
        /// </summary>
        [Required(ErrorMessage = "El nombre de usuario es obligatorio.")]
        [StringLength(50, ErrorMessage = "El usuario no puede superar los 50 caracteres.")]
        [Display(Name = "Usuario")]
        public string Usuario { get; set; } = string.Empty;

        /// <summary>
        /// Contraseña en texto plano (solo en formularios de alta/reset).
        /// RNF-02: Se hashea con BCrypt antes de persistir; nunca se almacena en claro.
        /// </summary>
        [StringLength(100, MinimumLength = 6,
            ErrorMessage = "La clave debe tener entre 6 y 100 caracteres.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string? Clave { get; set; }

        /// <summary>
        /// Confirmación de contraseña para validación en formularios.
        /// RNF-02: Evita errores de tipeo en la clave.
        /// </summary>
        [DataType(DataType.Password)]
        [Compare(nameof(Clave), ErrorMessage = "Las contraseñas no coinciden.")]
        [Display(Name = "Confirmar Contraseña")]
        public string? ConfirmClave { get; set; }

        /// <summary>
        /// Rol del usuario en el sistema.
        /// RF-12: "Administrador" — acceso total; "RRHH" — acceso operativo.
        /// </summary>
        [Required(ErrorMessage = "Debe seleccionar un rol.")]
        [Display(Name = "Rol")]
        public string Rol { get; set; } = "RRHH";
    }
}
