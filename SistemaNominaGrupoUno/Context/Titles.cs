namespace SistemaNominaGrupoUno.Context
{
    /// <summary>
    /// Entidad que representa el historial de títulos/cargos de un empleado.
    /// RF-06: Registrar títulos por empleado con histórico (from_date/to_date).
    ///        Permitir múltiples títulos en el tiempo, sin solapamiento.
    /// Clave primaria compuesta: (EmpNo, FromDate).
    /// </summary>
    public class Titles
    {
        /// <summary>FK al empleado al que pertenece el título.</summary>
        public int EmpNo { get; set; }

        /// <summary>
        /// Nombre del título o cargo del empleado.
        /// RF-06: Descripción del cargo en el período indicado.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Fecha de inicio del título (formato yyyy-MM-dd). Parte de la PK.
        /// RF-06: Inicio de vigencia del cargo.
        /// </summary>
        public string FromDate { get; set; }

        /// <summary>
        /// Fecha de fin del título (formato yyyy-MM-dd). Vacío = vigente.
        /// RF-06: Fin de vigencia. Vacío indica que el cargo sigue activo.
        /// </summary>
        public string ToDate { get; set; }

        public Employees Employee { get; set; }
    }
}
