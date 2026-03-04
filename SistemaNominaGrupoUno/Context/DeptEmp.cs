
namespace SistemaNominaGrupoUno.Context
{
    /// <summary>
    /// Entidad que representa la asignación de un empleado a un departamento con vigencia.
    /// RF-04: Registrar relaciones empleado–departamento con from_date y to_date.
    /// Clave primaria compuesta: (EmpNo, DeptNo).
    /// </summary>
    public class DeptEmp
    {
        /// <summary>FK al empleado asignado.</summary>
        public int EmpNo { get; set; }

        /// <summary>FK al departamento de destino.</summary>
        public int DeptNo { get; set; }

        /// <summary>
        /// Fecha de inicio de la asignación (formato yyyy-MM-dd).
        /// RF-04: Registro de vigencia desde.
        /// </summary>
        public string FromDate { get; set; }

        /// <summary>
        /// Fecha de fin de la asignación (formato yyyy-MM-dd). Null = vigente.
        /// RF-04: Registro de vigencia hasta. Nullable indica asignación activa.
        /// </summary>
        public string ToDate { get; set; }

        public Employees Employee { get; set; }
        public Departments Department { get; set; }
    }
}
