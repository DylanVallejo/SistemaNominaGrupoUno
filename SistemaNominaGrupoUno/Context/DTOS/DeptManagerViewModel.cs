using System.ComponentModel.DataAnnotations;

namespace SistemaNominaGrupoUno.Context.DTOS
{
    /// <summary>
    /// ViewModel para el formulario de alta y modificación de gerentes de departamento.
    /// RF-05: Registrar el manager (emp_no) con from_date y to_date.
    ///        Validar un solo manager activo por departamento en una fecha dada.
    /// RNF-03: Separación de capas MVC con ViewModel dedicado.
    /// </summary>
    public class DeptManagerViewModel
    {
        /// <summary>
        /// Identificador del empleado que actuará como gerente.
        /// RF-05: Clave foránea hacia la tabla Employees.
        /// </summary>
        [Required(ErrorMessage = "Debe seleccionar un empleado.")]
        [Display(Name = "Gerente (Empleado)")]
        public int EmpNo { get; set; }

        /// <summary>
        /// Identificador del departamento a gestionar.
        /// RF-05: Clave foránea hacia la tabla Departments.
        /// </summary>
        [Required(ErrorMessage = "Debe seleccionar un departamento.")]
        [Display(Name = "Departamento")]
        public int DeptNo { get; set; }

        /// <summary>
        /// Fecha de inicio del rol gerencial (formato yyyy-MM-dd).
        /// RF-05: Inicio de vigencia. RF-11: No puede ser posterior a ToDate.
        /// </summary>
        [Required(ErrorMessage = "La fecha de inicio es requerida.")]
        [Display(Name = "Fecha Desde")]
        public string FromDate { get; set; } = string.Empty;

        /// <summary>
        /// Fecha de fin del rol gerencial (formato yyyy-MM-dd). Opcional = vigente.
        /// RF-05: Fin de vigencia. RF-11: Debe ser mayor o igual a FromDate si se ingresa.
        /// </summary>
        [Display(Name = "Fecha Hasta")]
        public string? ToDate { get; set; }
    }
}
