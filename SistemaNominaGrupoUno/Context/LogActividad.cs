namespace SistemaNominaGrupoUno.Context
{
    /// <summary>
    /// Registro de actividad del sistema: accesos y operaciones críticas.
    /// RF-13: Log básico de acceso y operaciones de alta, edición y baja lógica.
    /// Clave primaria: Id (autoincremental).
    /// </summary>
    public class LogActividad
    {
        /// <summary>Identificador autoincremental del registro.</summary>
        public int Id { get; set; }

        /// <summary>
        /// Nombre de usuario que realizó la acción.
        /// RF-13: Trazabilidad del actor del evento.
        /// </summary>
        public string Usuario { get; set; } = string.Empty;

        /// <summary>
        /// Tipo de acción realizada (Alta, Edición, Baja, Login, Logout, etc.).
        /// RF-13: Clasificación del evento registrado.
        /// </summary>
        public string Accion { get; set; } = string.Empty;

        /// <summary>
        /// Nombre de la entidad o módulo afectado (Empleados, Departamentos, etc.).
        /// RF-13: Contexto del evento registrado.
        /// </summary>
        public string Entidad { get; set; } = string.Empty;

        /// <summary>
        /// Descripción detallada del evento.
        /// RF-13: Detalle de la operación realizada.
        /// </summary>
        public string Detalle { get; set; } = string.Empty;

        /// <summary>
        /// Fecha y hora del evento.
        /// RF-13: Marca temporal para auditoría cronológica.
        /// </summary>
        public DateTime Fecha { get; set; }
    }
}
