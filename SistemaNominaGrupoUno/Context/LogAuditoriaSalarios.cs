namespace SistemaNominaGrupoUno.Context
{
    public class LogAuditoriaSalarios
    {
        public int Id { get; set; }
        public string Usuario { get; set; }
        public DateTime FechaActualizacion { get; set; }
        public string DetalleCambio { get; set; }
        public long Salario { get; set; } // bigint
        public int EmpNo { get; set; }

        public Employees Employee { get; set; }

    }
}
