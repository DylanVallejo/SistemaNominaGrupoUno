using System.ComponentModel.DataAnnotations;

namespace SistemaNominaGrupoUno.Context.DTOS
{
    /// <summary>
    /// ViewModel para el formulario de alta y modificación de asignaciones
    /// empleado–departamento.
    /// RF-04: Registrar relaciones con from_date y to_date, evitando solapamientos
    ///        de vigencias para un mismo empleado.
    /// RNF-03: Separación de capas MVC con ViewModel dedicado.
    /// </summary>
    public class DeptEmpViewModel
    {
        /// <summary>
        /// Identificador del empleado a asignar.
        /// RF-04: Clave foránea hacia la tabla Employees.
        /// </summary>
        [Required(ErrorMessage = "Debe seleccionar un empleado.")]
        [Display(Name = "Empleado")]
        public int EmpNo { get; set; }

        /// <summary>
        /// Identificador del departamento de destino.
        /// RF-04: Clave foránea hacia la tabla Departments.
        /// </summary>
        [Required(ErrorMessage = "Debe seleccionar un departamento.")]
        [Display(Name = "Departamento")]
        public int DeptNo { get; set; }

        /// <summary>
        /// Fecha de inicio de la asignación (formato yyyy-MM-dd).
        /// RF-04: Inicio de vigencia. RF-11: No puede ser posterior a ToDate.
        /// </summary>
        [Required(ErrorMessage = "La fecha de inicio es requerida.")]
        [Display(Name = "Fecha Desde")]
        public string FromDate { get; set; } = string.Empty;

        /// <summary>
        /// Fecha de fin de la asignación (formato yyyy-MM-dd). Opcional = vigente.
        /// RF-04: Fin de vigencia. RF-11: Debe ser mayor o igual a FromDate si se ingresa.
        /// </summary>
        [Display(Name = "Fecha Hasta")]
        public string? ToDate { get; set; }
    }
}
