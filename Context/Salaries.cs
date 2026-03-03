namespace SistemaNominaGrupoUno.Context
{
    public class Salaries
    {
        public int EmpNo { get; set; }
        public long Salary { get; set; } // bigint
        public string FromDate { get; set; }
        public string ToDate { get; set; }

        public Employees Employee { get; set; }

    }
}
