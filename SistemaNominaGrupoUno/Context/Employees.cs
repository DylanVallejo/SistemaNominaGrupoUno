

namespace SistemaNominaGrupoUno.Context
{
    public class Employees
    {
        public int EmpNo { get; set; }
        public string Ci { get; set; }
        public string BirthDate { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public char Gender { get; set; }
        public string HireDate { get; set; }
        public string Correo { get; set; }

        public ICollection<Salaries> Salaries { get; set; }
        public ICollection<Titles> Titles { get; set; }
        public ICollection<DeptEmp> Departments { get; set; }
        public ICollection<DeptManager> ManagedDepartments { get; set; }
        public Users User { get; set; }

    }
}
