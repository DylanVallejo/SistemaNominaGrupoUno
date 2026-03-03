namespace SistemaNominaGrupoUno.Context
{
    public class Titles
    {
        public int EmpNo { get; set; }
        public string Title { get; set; } // campo correcto
        public string FromDate { get; set; }
        public string ToDate { get; set; }

        public Employees Employee { get; set; }

    }
}
