using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using SistemaNominaGrupoUno.Context;
using SistemaNominaGrupoUno.Context.DTOS;
using SistemaNominaGrupoUno.Controllers;

namespace SistemaNominaGrupoUno.Tests.Controllers
{
    /// <summary>
    /// Pruebas unitarias para EmployeeController.
    /// Se usa EF Core InMemory para aislar cada prueba de la base de datos real.
    /// Cada test obtiene una BD en memoria con nombre único para garantizar aislamiento.
    /// </summary>
    public class EmployeeControllerTests
    {
        // ────────────────────────────────────────────────────────────────────────
        // HELPERS
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>Crea un DbContext en memoria con nombre único por prueba.</summary>
        private static EmployeeManagementContext CreateContext() =>
            new(new DbContextOptionsBuilder<EmployeeManagementContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        /// <summary>Crea el controlador con TempData mockeado.</summary>
        private static EmployeeController CreateController(EmployeeManagementContext ctx)
        {
            var controller = new EmployeeController(ctx);
            controller.TempData = new TempDataDictionary(
                new DefaultHttpContext(),
                Mock.Of<ITempDataProvider>());
            return controller;
        }

        /// <summary>Fábrica de entidades Employees para los tests.</summary>
        private static Employees MakeEmployee(
            int id, string firstName, string lastName,
            string ci, string correo,
            char gender = 'M', bool isActive = true) => new()
        {
            EmpNo     = id,
            Ci        = ci,
            FirstName = firstName,
            LastName  = lastName,
            Gender    = gender,
            BirthDate = "1990-01-01",
            HireDate  = "2020-01-01",
            Correo    = correo,
            IsActive  = isActive
        };

        /// <summary>Fábrica de ViewModels para los tests de Create/Edit.</summary>
        private static EmployeeViewModel MakeViewModel(
            string ci        = "0912345678",
            string firstName = "Juan",
            string lastName  = "Perez",
            string correo    = "juan@test.com",
            char   gender    = 'M',
            string birthDate = "1990-01-15",
            string hireDate  = "2020-06-01",
            int    empNo     = 0) => new()
        {
            EmpNo     = empNo,
            Ci        = ci,
            FirstName = firstName,
            LastName  = lastName,
            Gender    = gender,
            BirthDate = birthDate,
            HireDate  = hireDate,
            Correo    = correo
        };

        // ────────────────────────────────────────────────────────────────────────
        // INDEX — filtros de estado
        // ────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Index_SinParametros_MuestraSoloEmpleadosActivos()
        {
            // Arrange
            using var ctx = CreateContext();
            ctx.Employees.AddRange(
                MakeEmployee(1, "Ana",  "Lopez",  "001", "ana@test.com",  isActive: true),
                MakeEmployee(2, "Luis", "Torres", "002", "luis@test.com", isActive: false));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);

            // Act — sin pasar status, el controlador usa "activo" por defecto
            var result = await ctrl.Index(null, null, 1);

            // Assert
            var view  = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<List<Employees>>(view.Model);
            Assert.Single(model);
            Assert.Equal("Ana", model[0].FirstName);
            Assert.Equal("activo", (string?)ctrl.ViewBag.Status);
        }

        [Fact]
        public async Task Index_FiltroActivo_RetornaSoloEmpleadosActivos()
        {
            // Arrange
            using var ctx = CreateContext();
            ctx.Employees.AddRange(
                MakeEmployee(1, "Ana",  "Lopez",  "001", "ana@test.com",  isActive: true),
                MakeEmployee(2, "Luis", "Torres", "002", "luis@test.com", isActive: false));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);

            // Act
            var result = await ctrl.Index(null, "activo", 1);

            // Assert
            var model = Assert.IsAssignableFrom<List<Employees>>(
                Assert.IsType<ViewResult>(result).Model);
            Assert.All(model, e => Assert.True(e.IsActive));
        }

        [Fact]
        public async Task Index_FiltroInactivo_RetornaSoloEmpleadosInactivos()
        {
            // Arrange
            using var ctx = CreateContext();
            ctx.Employees.AddRange(
                MakeEmployee(1, "Ana",  "Lopez",  "001", "ana@test.com",  isActive: true),
                MakeEmployee(2, "Luis", "Torres", "002", "luis@test.com", isActive: false));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);

            // Act
            var result = await ctrl.Index(null, "inactivo", 1);

            // Assert
            var model = Assert.IsAssignableFrom<List<Employees>>(
                Assert.IsType<ViewResult>(result).Model);
            Assert.Single(model);
            Assert.False(model[0].IsActive);
        }

        [Fact]
        public async Task Index_SinFiltroStatus_MuestraTodosLosEmpleados()
        {
            // Arrange
            using var ctx = CreateContext();
            ctx.Employees.AddRange(
                MakeEmployee(1, "Ana",  "Lopez",  "001", "ana@test.com",  isActive: true),
                MakeEmployee(2, "Luis", "Torres", "002", "luis@test.com", isActive: false));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);

            // Act — status vacío ("") no filtra por estado
            var result = await ctrl.Index(null, "", 1);

            // Assert
            var model = Assert.IsAssignableFrom<List<Employees>>(
                Assert.IsType<ViewResult>(result).Model);
            Assert.Equal(2, model.Count);
        }

        // ────────────────────────────────────────────────────────────────────────
        // INDEX — búsqueda por texto
        // ────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Index_BusquedaPorNombre_FiltraCorrectamente()
        {
            // Arrange
            using var ctx = CreateContext();
            ctx.Employees.AddRange(
                MakeEmployee(1, "Carlos", "Mendoza", "001", "carlos@test.com"),
                MakeEmployee(2, "Maria",  "Gomez",   "002", "maria@test.com"));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);

            // Act
            var result = await ctrl.Index("carlos", "", 1);

            // Assert
            var model = Assert.IsAssignableFrom<List<Employees>>(
                Assert.IsType<ViewResult>(result).Model);
            Assert.Single(model);
            Assert.Equal("Carlos", model[0].FirstName);
        }

        [Fact]
        public async Task Index_BusquedaPorApellido_FiltraCorrectamente()
        {
            // Arrange
            using var ctx = CreateContext();
            ctx.Employees.AddRange(
                MakeEmployee(1, "Carlos", "Mendoza", "001", "carlos@test.com"),
                MakeEmployee(2, "Maria",  "Gomez",   "002", "maria@test.com"));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);

            // Act
            var result = await ctrl.Index("GOMEZ", "", 1); // búsqueda insensible a mayúsculas

            // Assert
            var model = Assert.IsAssignableFrom<List<Employees>>(
                Assert.IsType<ViewResult>(result).Model);
            Assert.Single(model);
            Assert.Equal("Gomez", model[0].LastName);
        }

        [Fact]
        public async Task Index_BusquedaPorCi_FiltraCorrectamente()
        {
            // Arrange
            using var ctx = CreateContext();
            ctx.Employees.AddRange(
                MakeEmployee(1, "Carlos", "Mendoza", "0987654321", "carlos@test.com"),
                MakeEmployee(2, "Maria",  "Gomez",   "0912345678", "maria@test.com"));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);

            // Act
            var result = await ctrl.Index("0987", "", 1);

            // Assert
            var model = Assert.IsAssignableFrom<List<Employees>>(
                Assert.IsType<ViewResult>(result).Model);
            Assert.Single(model);
            Assert.Equal("0987654321", model[0].Ci);
        }

        [Fact]
        public async Task Index_BusquedaPorCorreo_FiltraCorrectamente()
        {
            // Arrange
            using var ctx = CreateContext();
            ctx.Employees.AddRange(
                MakeEmployee(1, "Carlos", "Mendoza", "001", "carlos@empresa.com"),
                MakeEmployee(2, "Maria",  "Gomez",   "002", "maria@otro.com"));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);

            // Act
            var result = await ctrl.Index("empresa.com", "", 1);

            // Assert
            var model = Assert.IsAssignableFrom<List<Employees>>(
                Assert.IsType<ViewResult>(result).Model);
            Assert.Single(model);
            Assert.Equal("carlos@empresa.com", model[0].Correo);
        }

        [Fact]
        public async Task Index_BusquedaSinCoincidencias_RetornaListaVacia()
        {
            // Arrange
            using var ctx = CreateContext();
            ctx.Employees.Add(MakeEmployee(1, "Ana", "Lopez", "001", "ana@test.com"));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);

            // Act
            var result = await ctrl.Index("xyzabcnotexists", "", 1);

            // Assert
            var model = Assert.IsAssignableFrom<List<Employees>>(
                Assert.IsType<ViewResult>(result).Model);
            Assert.Empty(model);
        }

        // ────────────────────────────────────────────────────────────────────────
        // INDEX — paginación
        // ────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Index_Paginacion_RetornaMaximoDiezRegistrosPorPagina()
        {
            // Arrange — 15 empleados activos
            using var ctx = CreateContext();
            for (int i = 1; i <= 15; i++)
                ctx.Employees.Add(MakeEmployee(i, $"Emp{i}", "Test", $"CI{i:D3}", $"e{i}@test.com"));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);

            // Act — página 1, sin filtro de estado (muestra todos)
            var result = await ctrl.Index(null, "", 1);

            // Assert
            var model = Assert.IsAssignableFrom<List<Employees>>(
                Assert.IsType<ViewResult>(result).Model);
            Assert.Equal(10, model.Count);
            Assert.Equal(2,  (int)ctrl.ViewBag.TotalPages);
            Assert.Equal(15, (int)ctrl.ViewBag.TotalItems);
        }

        [Fact]
        public async Task Index_Paginacion_PaginaDos_RetornaRegistrosRestantes()
        {
            // Arrange — 15 empleados
            using var ctx = CreateContext();
            for (int i = 1; i <= 15; i++)
                ctx.Employees.Add(MakeEmployee(i, $"Emp{i}", "Test", $"CI{i:D3}", $"e{i}@test.com"));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);

            // Act
            var result = await ctrl.Index(null, "", 2);

            // Assert — la segunda página tiene los 5 restantes
            var model = Assert.IsAssignableFrom<List<Employees>>(
                Assert.IsType<ViewResult>(result).Model);
            Assert.Equal(5, model.Count);
        }

        [Fact]
        public async Task Index_ViewBag_ContieneDatosDePaginacionCorrectos()
        {
            // Arrange
            using var ctx = CreateContext();
            ctx.Employees.Add(MakeEmployee(1, "Ana", "Lopez", "001", "ana@test.com"));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);

            // Act
            await ctrl.Index("ana", "activo", 1);

            // Assert
            Assert.Equal("ana",    (string?)ctrl.ViewBag.Search);
            Assert.Equal("activo", (string?)ctrl.ViewBag.Status);
            Assert.Equal(1,        (int)ctrl.ViewBag.CurrentPage);
            Assert.Equal(1,        (int)ctrl.ViewBag.TotalPages);
            Assert.Equal(1,        (int)ctrl.ViewBag.TotalItems);
        }

        // ────────────────────────────────────────────────────────────────────────
        // DETAILS
        // ────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Details_EmpleadoExistente_RetornaVistaConEmpleado()
        {
            // Arrange
            using var ctx = CreateContext();
            ctx.Employees.Add(MakeEmployee(1, "Ana", "Lopez", "001", "ana@test.com"));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);

            // Act
            var result = await ctrl.Details(1);

            // Assert
            var view  = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<Employees>(view.Model);
            Assert.Equal(1,     model.EmpNo);
            Assert.Equal("Ana", model.FirstName);
        }

        [Fact]
        public async Task Details_EmpleadoInexistente_RetornaNotFound()
        {
            // Arrange
            using var ctx = CreateContext();
            var ctrl = CreateController(ctx);

            // Act
            var result = await ctrl.Details(999);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        // ────────────────────────────────────────────────────────────────────────
        // CREATE GET
        // ────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Create_Get_RetornaVistaConViewModelVacio()
        {
            // Arrange
            using var ctx = CreateContext();
            var ctrl = CreateController(ctx);

            // Act
            var result = ctrl.Create();

            // Assert
            var view  = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<EmployeeViewModel>(view.Model);
            Assert.Equal(0,             model.EmpNo);
            Assert.Equal(string.Empty,  model.Ci);
            Assert.Equal(string.Empty,  model.Correo);
        }

        // ────────────────────────────────────────────────────────────────────────
        // CREATE POST
        // ────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Create_Post_DatosValidos_GuardaEmpleadoYRedirigeAIndex()
        {
            // Arrange
            using var ctx = CreateContext();
            var ctrl = CreateController(ctx);

            // Act
            var result = await ctrl.Create(MakeViewModel());

            // Assert
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(EmployeeController.Index), redirect.ActionName);
            Assert.Equal(1, await ctx.Employees.CountAsync());
        }

        [Fact]
        public async Task Create_Post_ModeloInvalido_RetornaVistaConModeloOriginal()
        {
            // Arrange
            using var ctx = CreateContext();
            var ctrl = CreateController(ctx);
            ctrl.ModelState.AddModelError("Ci", "La cédula es requerida.");
            var vm = MakeViewModel(ci: "");

            // Act
            var result = await ctrl.Create(vm);

            // Assert — devuelve la vista sin guardar nada
            Assert.IsType<ViewResult>(result);
            Assert.Equal(0, await ctx.Employees.CountAsync());
        }

        [Fact]
        public async Task Create_Post_CiDuplicada_AgregaErrorEnCampoYRetornaVista()
        {
            // Arrange
            using var ctx = CreateContext();
            ctx.Employees.Add(MakeEmployee(1, "Ana", "Lopez", "0912345678", "ana@test.com"));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);
            var vm = MakeViewModel(ci: "0912345678", correo: "nuevo@test.com"); // CI ya existe

            // Act
            var result = await ctrl.Create(vm);

            // Assert
            Assert.IsType<ViewResult>(result);
            Assert.True(ctrl.ModelState.ContainsKey("Ci"));
            Assert.Equal(1, await ctx.Employees.CountAsync()); // no se creó uno nuevo
        }

        [Fact]
        public async Task Create_Post_CorreoDuplicado_AgregaErrorEnCampoYRetornaVista()
        {
            // Arrange
            using var ctx = CreateContext();
            ctx.Employees.Add(MakeEmployee(1, "Ana", "Lopez", "001", "ana@test.com"));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);
            var vm = MakeViewModel(ci: "999", correo: "ana@test.com"); // correo ya existe

            // Act
            var result = await ctrl.Create(vm);

            // Assert
            Assert.IsType<ViewResult>(result);
            Assert.True(ctrl.ModelState.ContainsKey("Correo"));
        }

        [Fact]
        public async Task Create_Post_NuevoEmpleado_SeCreaConIsActiveTrue()
        {
            // Arrange
            using var ctx = CreateContext();
            var ctrl = CreateController(ctx);

            // Act
            await ctrl.Create(MakeViewModel());

            // Assert — RF-02: no borrado físico; el empleado nace activo
            var employee = await ctx.Employees.FirstOrDefaultAsync();
            Assert.NotNull(employee);
            Assert.True(employee.IsActive);
        }

        [Fact]
        public async Task Create_Post_Correo_SeAlmacenaEnMinusculas()
        {
            // Arrange
            using var ctx = CreateContext();
            var ctrl = CreateController(ctx);
            var vm = MakeViewModel(correo: "JUAN.PEREZ@TEST.COM");

            // Act
            await ctrl.Create(vm);

            // Assert
            var employee = await ctx.Employees.FirstAsync();
            Assert.Equal("juan.perez@test.com", employee.Correo);
        }

        [Fact]
        public async Task Create_Post_NombreConEspacios_SeAlmacenaTrimmed()
        {
            // Arrange
            using var ctx = CreateContext();
            var ctrl = CreateController(ctx);
            var vm = MakeViewModel(firstName: "  Juan  ", lastName: "  Perez  ");

            // Act
            await ctrl.Create(vm);

            // Assert
            var employee = await ctx.Employees.FirstAsync();
            Assert.Equal("Juan",  employee.FirstName);
            Assert.Equal("Perez", employee.LastName);
        }

        [Fact]
        public async Task Create_Post_Exitoso_AsignaMensajeEnTempData()
        {
            // Arrange
            using var ctx = CreateContext();
            var ctrl = CreateController(ctx);

            // Act
            await ctrl.Create(MakeViewModel());

            // Assert
            Assert.NotNull(ctrl.TempData["Success"]);
            Assert.Contains("creado exitosamente", ctrl.TempData["Success"]!.ToString());
        }

        // ────────────────────────────────────────────────────────────────────────
        // EDIT GET
        // ────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Edit_Get_EmpleadoExistente_RetornaVistaConViewModelRelleno()
        {
            // Arrange
            using var ctx = CreateContext();
            ctx.Employees.Add(MakeEmployee(1, "Ana", "Lopez", "001", "ana@test.com", 'F'));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);

            // Act
            var result = await ctrl.Edit(1);

            // Assert
            var view  = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<EmployeeViewModel>(view.Model);
            Assert.Equal(1,     model.EmpNo);
            Assert.Equal("Ana", model.FirstName);
            Assert.Equal("Lopez", model.LastName);
            Assert.Equal('F',   model.Gender);
            Assert.Equal("001", model.Ci);
            Assert.Equal("ana@test.com", model.Correo);
        }

        [Fact]
        public async Task Edit_Get_EmpleadoInexistente_RetornaNotFound()
        {
            // Arrange
            using var ctx = CreateContext();
            var ctrl = CreateController(ctx);

            // Act
            var result = await ctrl.Edit(999);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        // ────────────────────────────────────────────────────────────────────────
        // EDIT POST
        // ────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Edit_Post_DatosValidos_ActualizaEmpleadoYRedirigeAIndex()
        {
            // Arrange
            using var ctx = CreateContext();
            ctx.Employees.Add(MakeEmployee(1, "Ana", "Lopez", "001", "ana@test.com"));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);
            var vm = MakeViewModel(ci: "001", firstName: "Ana Editada",
                                   lastName: "Lopez", correo: "ana@test.com", empNo: 1);

            // Act
            var result = await ctrl.Edit(1, vm);

            // Assert
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(EmployeeController.Index), redirect.ActionName);
            var updated = await ctx.Employees.FindAsync(1);
            Assert.Equal("Ana Editada", updated!.FirstName);
        }

        [Fact]
        public async Task Edit_Post_IdNoCoincideConViewModel_RetornaBadRequest()
        {
            // Arrange
            using var ctx = CreateContext();
            var ctrl = CreateController(ctx);
            var vm = MakeViewModel(empNo: 5); // EmpNo = 5, pero id del route = 1

            // Act
            var result = await ctrl.Edit(1, vm);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task Edit_Post_ModeloInvalido_RetornaVistaConModelo()
        {
            // Arrange
            using var ctx = CreateContext();
            ctx.Employees.Add(MakeEmployee(1, "Ana", "Lopez", "001", "ana@test.com"));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);
            ctrl.ModelState.AddModelError("Ci", "Requerido");
            var vm = MakeViewModel(ci: "", empNo: 1);

            // Act
            var result = await ctrl.Edit(1, vm);

            // Assert
            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public async Task Edit_Post_EmpleadoInexistente_RetornaNotFound()
        {
            // Arrange
            using var ctx = CreateContext();
            var ctrl = CreateController(ctx);
            var vm = MakeViewModel(empNo: 999);

            // Act
            var result = await ctrl.Edit(999, vm);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Edit_Post_CiDuplicadaDeOtroEmpleado_AgregaErrorEnCampo()
        {
            // Arrange
            using var ctx = CreateContext();
            ctx.Employees.AddRange(
                MakeEmployee(1, "Ana",  "Lopez",  "001", "ana@test.com"),
                MakeEmployee(2, "Luis", "Torres", "002", "luis@test.com"));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);
            // Empleado 2 intenta usar la CI del empleado 1
            var vm = MakeViewModel(ci: "001", correo: "luis@test.com", empNo: 2);

            // Act
            var result = await ctrl.Edit(2, vm);

            // Assert
            Assert.IsType<ViewResult>(result);
            Assert.True(ctrl.ModelState.ContainsKey("Ci"));
        }

        [Fact]
        public async Task Edit_Post_MismaCiDelPropio_NoGeneraError()
        {
            // Arrange — el empleado actualiza sus datos pero conserva su propia CI
            using var ctx = CreateContext();
            ctx.Employees.Add(MakeEmployee(1, "Ana", "Lopez", "001", "ana@test.com"));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);
            var vm = MakeViewModel(ci: "001", correo: "ana@test.com",
                                   firstName: "Ana Actualizada", empNo: 1);

            // Act
            var result = await ctrl.Edit(1, vm);

            // Assert — no hay error, redirige correctamente
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(EmployeeController.Index), redirect.ActionName);
        }

        [Fact]
        public async Task Edit_Post_CorreoDuplicadoDeOtroEmpleado_AgregaErrorEnCampo()
        {
            // Arrange
            using var ctx = CreateContext();
            ctx.Employees.AddRange(
                MakeEmployee(1, "Ana",  "Lopez",  "001", "ana@test.com"),
                MakeEmployee(2, "Luis", "Torres", "002", "luis@test.com"));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);
            // Empleado 2 intenta usar el correo del empleado 1
            var vm = MakeViewModel(ci: "002", correo: "ana@test.com", empNo: 2);

            // Act
            var result = await ctrl.Edit(2, vm);

            // Assert
            Assert.IsType<ViewResult>(result);
            Assert.True(ctrl.ModelState.ContainsKey("Correo"));
        }

        // ────────────────────────────────────────────────────────────────────────
        // DEACTIVATE (baja lógica)
        // ────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Deactivate_EmpleadoActivo_EstableceIsActiveFalse()
        {
            // Arrange
            using var ctx = CreateContext();
            ctx.Employees.Add(MakeEmployee(1, "Ana", "Lopez", "001", "ana@test.com", isActive: true));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);

            // Act
            var result = await ctrl.Deactivate(1);

            // Assert
            Assert.IsType<RedirectToActionResult>(result);
            var employee = await ctx.Employees.FindAsync(1);
            Assert.False(employee!.IsActive);
        }

        [Fact]
        public async Task Deactivate_EmpleadoInexistente_RetornaNotFound()
        {
            // Arrange
            using var ctx = CreateContext();
            var ctrl = CreateController(ctx);

            // Act
            var result = await ctrl.Deactivate(999);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Deactivate_BajaLogica_NoEliminaFisicamenteElRegistro()
        {
            // Arrange — RF-02: sin borrado físico
            using var ctx = CreateContext();
            ctx.Employees.Add(MakeEmployee(1, "Ana", "Lopez", "001", "ana@test.com"));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);

            // Act
            await ctrl.Deactivate(1);

            // Assert — el registro sigue existiendo en la BD
            Assert.Equal(1, await ctx.Employees.CountAsync());
            var employee = await ctx.Employees.FindAsync(1);
            Assert.NotNull(employee); // no fue eliminado
        }

        [Fact]
        public async Task Deactivate_Exitoso_AsignaMensajeDeAlertaEnTempData()
        {
            // Arrange
            using var ctx = CreateContext();
            ctx.Employees.Add(MakeEmployee(1, "Ana", "Lopez", "001", "ana@test.com"));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);

            // Act
            await ctrl.Deactivate(1);

            // Assert
            Assert.NotNull(ctrl.TempData["Warning"]);
            Assert.Contains("dado de baja", ctrl.TempData["Warning"]!.ToString());
        }

        // ────────────────────────────────────────────────────────────────────────
        // ACTIVATE (reactivación)
        // ────────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task Activate_EmpleadoInactivo_EstableceIsActiveTrue()
        {
            // Arrange
            using var ctx = CreateContext();
            ctx.Employees.Add(
                MakeEmployee(1, "Ana", "Lopez", "001", "ana@test.com", isActive: false));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);

            // Act
            var result = await ctrl.Activate(1);

            // Assert
            Assert.IsType<RedirectToActionResult>(result);
            var employee = await ctx.Employees.FindAsync(1);
            Assert.True(employee!.IsActive);
        }

        [Fact]
        public async Task Activate_EmpleadoInexistente_RetornaNotFound()
        {
            // Arrange
            using var ctx = CreateContext();
            var ctrl = CreateController(ctx);

            // Act
            var result = await ctrl.Activate(999);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Activate_Exitoso_AsignaMensajeDeExitoEnTempData()
        {
            // Arrange
            using var ctx = CreateContext();
            ctx.Employees.Add(
                MakeEmployee(1, "Ana", "Lopez", "001", "ana@test.com", isActive: false));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);

            // Act
            await ctrl.Activate(1);

            // Assert
            Assert.NotNull(ctrl.TempData["Success"]);
            Assert.Contains("reactivado exitosamente", ctrl.TempData["Success"]!.ToString());
        }

        [Fact]
        public async Task Activate_DespuesDeDeactivate_EmpleadoVuelveAEstarActivo()
        {
            // Arrange — ciclo completo baja → alta
            using var ctx = CreateContext();
            ctx.Employees.Add(MakeEmployee(1, "Ana", "Lopez", "001", "ana@test.com"));
            await ctx.SaveChangesAsync();
            var ctrl = CreateController(ctx);

            // Act
            await ctrl.Deactivate(1);
            var despuesDeBaja = await ctx.Employees.FindAsync(1);
            Assert.False(despuesDeBaja!.IsActive); // validar baja

            await ctrl.Activate(1);

            // Assert
            var despuesDeAlta = await ctx.Employees.FindAsync(1);
            Assert.True(despuesDeAlta!.IsActive); // reactivado
        }
    }
}
