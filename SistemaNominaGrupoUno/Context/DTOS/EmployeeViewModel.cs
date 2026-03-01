using System.ComponentModel.DataAnnotations;

namespace SistemaNominaGrupoUno.Context.DTOS
{
    /// <summary>
    /// ViewModel para el formulario de alta y modificación de empleados.
    /// </summary>
    public class EmployeeViewModel
    {
        public int EmpNo { get; set; }

        /// <summary>Cédula de identidad — debe ser única en el sistema.</summary>
        [Required(ErrorMessage = "La cédula de identidad es requerida.")]
        [StringLength(50, ErrorMessage = "Máximo 50 caracteres.")]
        [Display(Name = "Cédula de Identidad")]
        public string Ci { get; set; } = string.Empty;

        /// <summary>Fecha de nacimiento en formato yyyy-MM-dd.</summary>
        [Required(ErrorMessage = "La fecha de nacimiento es requerida.")]
        [Display(Name = "Fecha de Nacimiento")]
        public string BirthDate { get; set; } = string.Empty;

        /// <summary>Nombre del empleado.</summary>
        [Required(ErrorMessage = "El nombre es requerido.")]
        [StringLength(50, ErrorMessage = "Máximo 50 caracteres.")]
        [Display(Name = "Nombre")]
        public string FirstName { get; set; } = string.Empty;

        /// <summary>Apellido del empleado.</summary>
        [Required(ErrorMessage = "El apellido es requerido.")]
        [StringLength(50, ErrorMessage = "Máximo 50 caracteres.")]
        [Display(Name = "Apellido")]
        public string LastName { get; set; } = string.Empty;

        /// <summary>Género: M (Masculino) o F (Femenino).</summary>
        [Required(ErrorMessage = "El género es requerido.")]
        [Display(Name = "Género")]
        public char Gender { get; set; }

        /// <summary>Fecha de contratación en formato yyyy-MM-dd.</summary>
        [Required(ErrorMessage = "La fecha de contratación es requerida.")]
        [Display(Name = "Fecha de Contratación")]
        public string HireDate { get; set; } = string.Empty;

        /// <summary>Correo electrónico — debe ser único en el sistema.</summary>
        [Required(ErrorMessage = "El correo electrónico es requerido.")]
        [EmailAddress(ErrorMessage = "El correo electrónico no tiene un formato válido.")]
        [StringLength(160, ErrorMessage = "Máximo 160 caracteres.")]
        [Display(Name = "Correo Electrónico")]
        public string Correo { get; set; } = string.Empty;
    }
}
