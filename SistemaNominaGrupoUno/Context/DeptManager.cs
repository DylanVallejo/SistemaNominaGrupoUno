namespace SistemaNominaGrupoUno.Context
{
    /// <summary>
    /// Entidad que representa la asignación de un empleado como gerente de un departamento.
    /// RF-05: Registrar el manager (emp_no) con from_date y to_date.
    ///        Validar un solo manager activo por departamento en una fecha dada.
    /// Clave primaria compuesta: (EmpNo, DeptNo).
    /// </summary>
    public class DeptManager
    {
        /// <summary>FK al empleado que actúa como gerente.</summary>
        public int EmpNo { get; set; }

        /// <summary>FK al departamento que es gestionado.</summary>
        public int DeptNo { get; set; }

        /// <summary>
        /// Fecha de inicio del rol de gerente (formato yyyy-MM-dd).
        /// RF-05: Inicio de vigencia del cargo gerencial.
        /// </summary>
        public string FromDate { get; set; }

        /// <summary>
        /// Fecha de fin del rol de gerente (formato yyyy-MM-dd). Vacío = vigente.
        /// RF-05: Fin de vigencia. Vacío indica que el gerente sigue activo.
        /// </summary>
        public string ToDate { get; set; }

        public Employees Employee { get; set; }
        public Departments Department { get; set; }
    }
}
