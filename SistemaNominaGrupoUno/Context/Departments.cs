
namespace SistemaNominaGrupoUno.Context
{
    public class Departments
    {

        public int DeptNo { get; set; }
        public string DeptName { get; set; }

        public ICollection<DeptManager> Managers { get; set; }
        public ICollection<DeptEmp> Employees { get; set; }
       
    }
}
