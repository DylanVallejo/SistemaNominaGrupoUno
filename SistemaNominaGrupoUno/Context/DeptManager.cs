namespace SistemaNominaGrupoUno.Context
{
    public class DeptManager
    {

        public int EmpNo { get; set; }
        public int DeptNo { get; set; }
        public string FromDate { get; set; }
        public string ToDate { get; set; }

        public Employees Employee { get; set; }
        public Departments Department { get; set; }


    }
}
