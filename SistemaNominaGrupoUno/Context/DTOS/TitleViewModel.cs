using System.ComponentModel.DataAnnotations;

namespace SistemaNominaGrupoUno.Context.DTOS
{
    /// <summary>
    /// ViewModel para el formulario de alta y modificación del historial de títulos.
    /// RF-06: Registrar títulos por empleado con histórico (from_date/to_date).
    ///        Permitir múltiples títulos en el tiempo, sin solapamiento.
    /// RNF-03: Separación de capas MVC con ViewModel dedicado.
    /// </summary>
    public class TitleViewModel
    {
        /// <summary>
        /// Identificador del empleado al que se le asigna el título.
        /// RF-06: Clave foránea hacia la tabla Employees.
        /// </summary>
        [Required(ErrorMessage = "Debe seleccionar un empleado.")]
        [Display(Name = "Empleado")]
        public int EmpNo { get; set; }

        /// <summary>
        /// Nombre del título o cargo del empleado.
        /// RF-06: Descripción del cargo. RF-11: Campo requerido, máximo 50 caracteres.
        /// </summary>
        [Required(ErrorMessage = "El título es requerido.")]
        [StringLength(50, ErrorMessage = "El título no puede superar los 50 caracteres.")]
        [Display(Name = "Título / Cargo")]
        public string TitleName { get; set; } = string.Empty;

        /// <summary>
        /// Fecha de inicio del título (formato yyyy-MM-dd). Forma parte de la PK.
        /// RF-06: Inicio de vigencia. RF-11: No puede ser posterior a ToDate.
        /// </summary>
        [Required(ErrorMessage = "La fecha de inicio es requerida.")]
        [Display(Name = "Fecha Desde")]
        public string FromDate { get; set; } = string.Empty;

        /// <summary>
        /// Fecha de fin del título (formato yyyy-MM-dd). Opcional = vigente.
        /// RF-06: Fin de vigencia. RF-11: Debe ser mayor o igual a FromDate si se ingresa.
        /// </summary>
        [Display(Name = "Fecha Hasta")]
        public string? ToDate { get; set; }

        /// <summary>
        /// Fecha original del registro cuando se edita (para identificar el registro
        /// por PK compuesta EmpNo+FromDate sin alterar la clave en el formulario).
        /// </summary>
        public string OriginalFromDate { get; set; } = string.Empty;
    }
}
