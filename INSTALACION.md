# Sistema de Nómina — Guía de Instalación

Sistema de gestión de nómina desarrollado en **ASP.NET Core 10 MVC** con **Entity Framework Core** y **SQL Server Express**.

---

## Requisitos previos

| Herramienta | Versión mínima | Enlace |
|---|---|---|
| Visual Studio 2022 | 17.10 o superior | https://visualstudio.microsoft.com/ |
| .NET SDK | 10.0 | https://dotnet.microsoft.com/download |
| SQL Server Express | 2019 o superior | https://www.microsoft.com/sql-server/sql-server-downloads |
| Git | Cualquier versión reciente | https://git-scm.com/ |

> **Carga de trabajo requerida en Visual Studio:** `ASP.NET y desarrollo web`
>
> Al instalar o modificar Visual Studio, asegurarse de tener habilitada esta carga de trabajo desde el Visual Studio Installer.

---

## 1. Clonar el repositorio

Abrir una terminal (CMD, PowerShell o Git Bash) y ejecutar:

```bash
git clone https://github.com/DylanVallejo/SistemaNominaGrupoUno.git
cd SistemaNominaGrupoUno
```

---

## 2. Abrir la solución en Visual Studio

Abrir el archivo `SistemaNominaGrupoUno.slnx` desde Visual Studio 2022:

- **Opción A:** Doble clic sobre el archivo `.slnx` en el Explorador de archivos.
- **Opción B:** Desde Visual Studio → `Archivo` → `Abrir` → `Proyecto o Solución`.

---

## 3. Configurar la cadena de conexión

Abrir el archivo `SistemaNominaGrupoUno/appsettings.json` y verificar que el nombre del servidor coincida con la instancia local de SQL Server:

```json
"ConnectionStrings": {
  "EmployeeManagementConnection": "Server=localhost\\SQLEXPRESS;Database=EmployeeManagementDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;"
}
```

> Si la instancia de SQL Server tiene un nombre distinto (por ejemplo `.\SQLSERVER` o `localhost`), ajustar el valor de `Server` en consecuencia.

---

## 4. Restaurar paquetes NuGet

Visual Studio restaura los paquetes automáticamente al abrir la solución. Si no lo hace, ejecutar desde la **Consola del Administrador de Paquetes** (`Herramientas` → `Administrador de paquetes NuGet` → `Consola`):

```powershell
dotnet restore
```

### Paquetes incluidos

| Paquete | Versión |
|---|---|
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.3 |
| Microsoft.EntityFrameworkCore.Tools | 10.0.3 |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.3 |
| BCrypt.Net-Next | 4.0.3 |
| QuestPDF | 2024.12.4 |
| ClosedXML | 0.104.1 |
| System.IO.Packaging | 9.0.0 |

---

## 5. Aplicar las migraciones a la base de datos

Desde la **Consola del Administrador de Paquetes** en Visual Studio, asegurarse de que el proyecto seleccionado sea `SistemaNominaGrupoUno` y ejecutar:

```powershell
Update-Database
```

Esto creará automáticamente la base de datos `EmployeeManagementDB` y todas sus tablas aplicando las siguientes migraciones en orden:

1. `migracionInicial` — estructura base de todas las entidades
2. `AddEmployeeIsActive` — baja lógica de empleados
3. `AddDepartmentIsActive` — baja lógica de departamentos
4. `AddAdminModule` — usuarios, roles y log de actividad

> **Alternativa desde terminal:** Instalar la herramienta EF Core CLI globalmente si no está disponible:
> ```bash
> dotnet tool install --global dotnet-ef
> ```
> Luego, desde la carpeta `SistemaNominaGrupoUno/`:
> ```bash
> dotnet ef database update
> ```

---

## 6. Ejecutar el proyecto

Presionar **F5** en Visual Studio (o `Depurar` → `Iniciar depuración`) para compilar y ejecutar el proyecto.

La aplicación se abrirá en el navegador en una dirección similar a:

```
https://localhost:5001
```

> El puerto exacto puede variar según la configuración local. Revisar la consola de salida de Visual Studio para confirmar la URL.

---

## 7. Crear el primer usuario administrador

La base de datos inicia sin usuarios. Es necesario insertar el usuario administrador **una sola vez** antes de poder iniciar sesión.

Abrir **SQL Server Management Studio (SSMS)**, conectarse a la instancia `localhost\SQLEXPRESS`, seleccionar la base de datos `EmployeeManagementDB` y ejecutar el siguiente script completo de una sola vez:

```sql
USE EmployeeManagementDB;

-- Paso 1: insertar el empleado administrador
INSERT INTO Employees (Ci, BirthDate, FirstName, LastName, Gender, HireDate, Correo, IsActive)
VALUES ('00000000', '1990-01-01', 'Administrador', 'Administrador', 'M', '2020-01-01', 'admin@gmail.com', 1);

-- Paso 2: capturar el EmpNo generado automaticamente
DECLARE @EmpNo INT = SCOPE_IDENTITY();

-- Paso 3: insertar el usuario administrador
-- Contrasena inicial: admin  (hash BCrypt work factor 12)
INSERT INTO Users (EmpNo, Usuario, Clave, Rol)
VALUES (@EmpNo, 'admin',
  '$2a$12$9EvckHlTeY33IHUcK7Rx/OMAxnYlSnxfWj0ck6M2UU3xvIBY5vvFq',
  'Administrador');
```

> **Importante:**
> - Ejecutar los tres bloques juntos en la misma ventana de query para que `SCOPE_IDENTITY()` capture el `EmpNo` correctamente.
> - La columna `EmpNo` es `IDENTITY`: no especificar un valor explícito.
> - El hash corresponde a la contraseña `admin` con BCrypt work factor 12. Se recomienda cambiarla desde el módulo de Administración una vez iniciada la primera sesión.

---

## 8. Iniciar sesión

Una vez ejecutado el script, acceder a la aplicación e iniciar sesión con:

| Campo | Valor |
|---|---|
| Usuario | `admin` |
| Contraseña | `admin` |

> Desde el módulo **Administración** se pueden crear usuarios adicionales asociados a empleados existentes, y cambiar la contraseña del administrador mediante la opción "Restablecer contraseña".

---

## 9. Ejecutar las pruebas unitarias (opcional)

Las pruebas se encuentran en el proyecto `SistemaNominaGrupoUno.Tests`. Para ejecutarlas:

**Desde Visual Studio:**
`Prueba` → `Ejecutar todas las pruebas`

**Desde terminal:**
```bash
cd SistemaNominaGrupoUno.Tests
dotnet test
```

---

## Estructura de la solución

```
SistemaNominaGrupoUno/
├── Context/                  # Entidades EF Core y DbContext
│   └── DTOS/                 # ViewModels y DTOs
├── Controllers/              # Controladores MVC
├── Migrations/               # Migraciones EF Core
├── Views/                    # Vistas Razor por modulo
│   ├── Auth/
│   ├── Admin/
│   ├── Employee/
│   ├── Department/
│   ├── DeptEmp/
│   ├── DeptManager/
│   ├── Title/
│   ├── Salary/
│   ├── Report/
│   ├── Home/
│   └── Shared/
├── wwwroot/                  # Archivos estaticos
├── appsettings.json          # Configuracion y cadena de conexion
└── Program.cs                # Configuracion de la aplicacion

SistemaNominaGrupoUno.Tests/
└── Controllers/              # Pruebas unitarias (xUnit + EF InMemory + Moq)
```

---

## Modulos del sistema

| Modulo | Descripcion |
|---|---|
| Autenticacion | Login/Logout con cookies; roles Administrador y RRHH |
| Empleados | CRUD con baja logica, busqueda y paginacion |
| Departamentos | CRUD con baja logica y validacion de nombre unico |
| Asignaciones | Asignacion de empleados a departamentos con vigencias |
| Gerentes | Asignacion de gerentes por departamento con exclusividad |
| Titulos / Cargos | Historial de cargos por empleado con vigencias |
| Salarios | Registro de salarios con auditoria automatica |
| Reportes | Exportacion a PDF y Excel (nomina, cambios salariales, estructura) |
| Administracion | Gestion de usuarios, log de actividad y configuracion del sistema |
| Dashboard | Metricas generales y alertas de vigencias proximas a vencer |

---

## Stack tecnico

- **Backend:** ASP.NET Core 10.0 MVC, C#
- **ORM:** Entity Framework Core 10.0.3
- **Base de datos:** SQL Server Express
- **Frontend:** Bootstrap 5 + Bootstrap Icons (CDN)
- **Seguridad:** Autenticacion por cookie, BCrypt work factor 12
- **Reportes:** QuestPDF (PDF) + ClosedXML (Excel)
- **Pruebas:** xUnit + EF Core InMemory + Moq
