using System.ComponentModel.DataAnnotations;

namespace SistemaNominaGrupoUno.Context.DTOS
{
    /// <summary>
    /// ViewModel para el formulario de alta y modificación de departamentos.
    /// RF-03: Gestión de departamentos — campos mínimos: dept_no, dept_name.
    /// RNF-03: Separación de capas MVC con ViewModel dedicado.
    /// </summary>
    public class DepartmentViewModel
    {
        /// <summary>
        /// Identificador del departamento (PK). Se usa en el formulario de edición.
        /// RF-03: Campo mínimo dept_no.
        /// </summary>
        public int DeptNo { get; set; }

        /// <summary>
        /// Nombre del departamento — debe ser único en el sistema.
        /// RF-03: Campo mínimo dept_name.
        /// RF-11: Validaciones de negocio — nombre requerido y máximo 50 caracteres.
        /// </summary>
        [Required(ErrorMessage = "El nombre del departamento es requerido.")]
        [StringLength(50, ErrorMessage = "El nombre no puede superar los 50 caracteres.")]
        [Display(Name = "Nombre del Departamento")]
        public string DeptName { get; set; } = string.Empty;
    }
}
