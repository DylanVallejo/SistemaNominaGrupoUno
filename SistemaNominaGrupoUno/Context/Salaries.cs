namespace SistemaNominaGrupoUno.Context
{
    /// <summary>
    /// Entidad que representa el historial salarial de un empleado.
    /// RF-07: Registrar salarios por empleado con from_date/to_date.
    ///        Solo un salario activo por empleado en una fecha dada.
    /// Clave primaria compuesta: (EmpNo, FromDate).
    /// </summary>
    public class Salaries
    {
        /// <summary>FK al empleado al que corresponde el salario.</summary>
        public int EmpNo { get; set; }

        /// <summary>
        /// Monto del salario en la moneda base del sistema (bigint).
        /// RF-07: Salario registrado para el período indicado.
        /// </summary>
        public long Salary { get; set; }

        /// <summary>
        /// Fecha de inicio del salario (formato yyyy-MM-dd). Parte de la PK.
        /// RF-07: Inicio de vigencia salarial.
        /// </summary>
        public string FromDate { get; set; }

        /// <summary>
        /// Fecha de fin del salario (formato yyyy-MM-dd). Vacío = vigente.
        /// RF-07: Fin de vigencia salarial. Vacío indica salario activo.
        /// </summary>
        public string ToDate { get; set; }

        public Employees Employee { get; set; }
    }
}
