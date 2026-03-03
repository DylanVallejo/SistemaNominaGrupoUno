using System.ComponentModel.DataAnnotations;

namespace SistemaNominaGrupoUno.Context.DTOS
{
    /// <summary>
    /// ViewModel para el formulario de alta y modificación de salarios de empleados.
    /// RF-07: Registrar salarios por empleado con from_date/to_date.
    ///        Solo un salario activo por empleado en una fecha dada.
    /// RF-08: Cada alta o cambio genera un registro en Log_AuditoriaSalarios.
    /// RNF-03: Separación de capas MVC con ViewModel dedicado.
    /// </summary>
    public class SalaryViewModel
    {
        /// <summary>
        /// Identificador del empleado al que se le asigna el salario.
        /// RF-07: Clave foránea hacia la tabla Employees.
        /// </summary>
        [Required(ErrorMessage = "Debe seleccionar un empleado.")]
        [Display(Name = "Empleado")]
        public int EmpNo { get; set; }

        /// <summary>
        /// Monto del salario. Debe ser mayor a cero.
        /// RF-07: Monto salarial registrado para el período.
        /// RF-11: El salario debe ser un valor positivo.
        /// </summary>
        [Required(ErrorMessage = "El salario es requerido.")]
        [Range(1, long.MaxValue, ErrorMessage = "El salario debe ser mayor a cero.")]
        [Display(Name = "Salario")]
        public long SalaryAmount { get; set; }

        /// <summary>
        /// Fecha de inicio del salario (formato yyyy-MM-dd). Parte de la PK.
        /// RF-07: Inicio de vigencia salarial. RF-11: No puede ser posterior a ToDate.
        /// </summary>
        [Required(ErrorMessage = "La fecha de inicio es requerida.")]
        [Display(Name = "Fecha Desde")]
        public string FromDate { get; set; } = string.Empty;

        /// <summary>
        /// Fecha de fin del salario (formato yyyy-MM-dd). Opcional = vigente.
        /// RF-07: Fin de vigencia salarial. RF-11: Debe ser >= FromDate si se ingresa.
        /// </summary>
        [Display(Name = "Fecha Hasta")]
        public string? ToDate { get; set; }

        /// <summary>
        /// Conserva la FromDate original al editar, dado que forma parte de la PK.
        /// Permite identificar el registro sin modificar la clave durante la edición.
        /// </summary>
        public string OriginalFromDate { get; set; } = string.Empty;
    }
}
