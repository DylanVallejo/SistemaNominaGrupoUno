
using Microsoft.AspNetCore.Identity;

namespace SistemaNominaGrupoUno.Context
{
    public class Users : IdentityUser
    {
        public int EmpNo { get; set; }
        //public int EmpNo { get; set; }
        public string Usuario { get; set; }
        public string Clave { get; set; }

        public Employees Employee { get; set; }

    }
}
