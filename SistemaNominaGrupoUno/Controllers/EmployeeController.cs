using Microsoft.AspNetCore.Mvc;
using SistemaNominaGrupoUno.Context;

namespace SistemaNominaGrupoUno.Controllers
{
    public class EmployeeController ( EmployeeManagementContext employeeManagementContext ) : Controller
    {
        public IActionResult Index()
        {
            var employees = employeeManagementContext.Employees.ToList();
            return View(employees);
        }
    }
}
