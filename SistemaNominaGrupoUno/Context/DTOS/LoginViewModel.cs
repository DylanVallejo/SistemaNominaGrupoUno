using System.ComponentModel.DataAnnotations;

namespace SistemaNominaGrupoUno.Context.DTOS
{
    /// <summary>
    /// ViewModel para el formulario de inicio de sesión.
    /// RF-01: Credenciales de acceso al sistema (usuario y clave).
    /// RNF-02: La clave nunca se almacena; solo se verifica con BCrypt.Verify.
    /// RNF-03: Documentación XML en ViewModels.
    /// </summary>
    public class LoginViewModel
    {
        /// <summary>
        /// Nombre de usuario registrado en el sistema.
        /// RF-01: Identificador de acceso.
        /// </summary>
        [Required(ErrorMessage = "El usuario es obligatorio.")]
        [Display(Name = "Usuario")]
        public string Usuario { get; set; } = string.Empty;

        /// <summary>
        /// Contraseña en texto plano — se compara con el hash BCrypt almacenado.
        /// RNF-02: Nunca se persiste; solo se usa para verificación.
        /// </summary>
        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Clave { get; set; } = string.Empty;

        /// <summary>
        /// URL a la que redirigir tras el login exitoso.
        /// RF-01: Preservar la navegación solicitada antes del redirect al login.
        /// </summary>
        public string? ReturnUrl { get; set; }
    }
}
