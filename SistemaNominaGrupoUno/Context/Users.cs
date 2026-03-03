using Microsoft.AspNetCore.Identity;

namespace SistemaNominaGrupoUno.Context
{
    /// <summary>
    /// Entidad de usuario del sistema con credenciales y rol de acceso.
    /// RF-01: Autenticación con usuario y clave (hash).
    /// RF-12: Roles de acceso — Administrador y RRHH.
    /// RNF-02: Contraseña almacenada como hash BCrypt.
    /// Clave primaria: EmpNo (FK a Employees — un empleado, un usuario).
    /// </summary>
    public class Users : IdentityUser
    {
        /// <summary>
        /// FK al empleado propietario del usuario. También es la PK.
        /// RF-01: Vínculo directo entre empleado y credenciales.
        /// </summary>
        public int EmpNo { get; set; }

        /// <summary>
        /// Nombre de usuario para el inicio de sesión. Debe ser único.
        /// RF-01: Credencial de acceso al sistema.
        /// </summary>
        public string Usuario { get; set; }

        /// <summary>
        /// Contraseña almacenada como hash BCrypt.
        /// RNF-02: Seguridad — nunca se almacena texto plano.
        /// </summary>
        public string Clave { get; set; }

        /// <summary>
        /// Rol del usuario en el sistema.
        /// RF-12: "Administrador" — acceso total; "RRHH" — acceso operativo.
        /// </summary>
        public string Rol { get; set; } = "RRHH";

        public Employees Employee { get; set; }
    }
}
