
namespace SistemaNominaGrupoUno.Context
{
    /// <summary>
    /// Entidad que representa un departamento de la organización.
    /// RF-03: Gestión de departamentos (alta, baja lógica, modificación, consulta).
    /// </summary>
    public class Departments
    {
        /// <summary>Clave primaria autoincremental del departamento.</summary>
        public int DeptNo { get; set; }

        /// <summary>Nombre del departamento — debe ser único en el sistema.</summary>
        public string DeptName { get; set; }

        /// <summary>
        /// Indica si el departamento está activo en el sistema.
        /// RF-03: Baja lógica — no se elimina físicamente el registro.
        /// </summary>
        public bool IsActive { get; set; } = true;

        public ICollection<DeptManager> Managers { get; set; }
        public ICollection<DeptEmp> Employees { get; set; }
    }
}
