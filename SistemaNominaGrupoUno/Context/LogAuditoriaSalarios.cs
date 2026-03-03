namespace SistemaNominaGrupoUno.Context
{
    /// <summary>
    /// Entidad de auditoría que registra cada alta o cambio salarial de un empleado.
    /// RF-08: Registrar en Log_AuditoriaSalarios cada alta o cambio de salario
    ///        con usuario, fecha/hora, detalle y monto.
    /// </summary>
    public class LogAuditoriaSalarios
    {
        /// <summary>Clave primaria autoincremental del registro de auditoría.</summary>
        public int Id { get; set; }

        /// <summary>
        /// Usuario del sistema que realizó el cambio salarial.
        /// RF-08: Identificación del responsable del cambio.
        /// </summary>
        public string Usuario { get; set; }

        /// <summary>
        /// Fecha y hora exacta en que se realizó el cambio salarial.
        /// RF-08: Marca temporal del evento de auditoría.
        /// </summary>
        public DateTime FechaActualizacion { get; set; }

        /// <summary>
        /// Descripción del cambio realizado (ej. "Alta de salario", "Modificación de salario").
        /// RF-08: Detalle del evento registrado en la auditoría.
        /// </summary>
        public string DetalleCambio { get; set; }

        /// <summary>
        /// Monto del salario registrado en el evento de auditoría.
        /// RF-08: Monto asociado al cambio salarial auditado.
        /// </summary>
        public long Salario { get; set; }

        /// <summary>FK al empleado cuyo salario fue modificado.</summary>
        public int EmpNo { get; set; }

        public Employees Employee { get; set; }
    }
}
