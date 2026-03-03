namespace SistemaNominaGrupoUno.Context.DTOS
{
    // ─────────────────────────────────────────────────────────────
    // Dashboard — Sección 7 PDF (Pantalla requerida)
    // RF-02: Resumen de empleados (activos / inactivos)
    // RF-03: Resumen de departamentos (activos / inactivos)
    // RF-07: Total de salarios vigentes
    // RF-13: Acceso rápido a módulos desde pantalla principal
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// ViewModel para el Dashboard principal del sistema.
    /// Pantalla requerida (Sección 7 PDF): resumen de empleados, departamentos,
    /// salarios vigentes y alertas de vigencias próximas a vencer.
    /// Accesos rápidos a todos los módulos del sistema.
    /// </summary>
    public class DashboardViewModel
    {
        // ── Empleados (RF-02) ─────────────────────────────────────

        /// <summary>Total de empleados activos en el sistema. RF-02.</summary>
        public int TotalEmpleadosActivos   { get; set; }

        /// <summary>Total de empleados inactivos (baja lógica). RF-02.</summary>
        public int TotalEmpleadosInactivos { get; set; }

        // ── Departamentos (RF-03) ─────────────────────────────────

        /// <summary>Total de departamentos activos. RF-03.</summary>
        public int TotalDeptosActivos      { get; set; }

        /// <summary>Total de departamentos inactivos (baja lógica). RF-03.</summary>
        public int TotalDeptosInactivos    { get; set; }

        // ── Salarios vigentes (RF-07) ─────────────────────────────

        /// <summary>
        /// Total de salarios cuya vigencia está activa hoy (ToDate vacío o >= hoy).
        /// RF-07: Solo un salario activo por empleado en una fecha dada.
        /// </summary>
        public int TotalSalariosVigentes   { get; set; }

        // ── Alertas de vigencias próximas a vencer ────────────────

        /// <summary>
        /// Vigencias (asignaciones, gerencias, títulos, salarios) que vencen
        /// en los próximos 30 días. Permite tomar acción oportuna.
        /// Sección 7 PDF: "alertas de vigencias por vencer".
        /// </summary>
        public List<AlertaVigencia> AlertasVigencias { get; set; } = [];
    }

    /// <summary>
    /// Representa un ítem de alerta para vigencias próximas a vencer.
    /// Sección 7 PDF: Dashboard — alertas de vigencias por vencer.
    /// </summary>
    public class AlertaVigencia
    {
        /// <summary>Tipo de vigencia: "Asignación", "Gerencia", "Título" o "Salario".</summary>
        public string Tipo            { get; set; } = string.Empty;

        /// <summary>Descripción legible del registro (nombre del empleado y entidad).</summary>
        public string Descripcion     { get; set; } = string.Empty;

        /// <summary>Fecha de vencimiento de la vigencia (yyyy-MM-dd).</summary>
        public string FechaVencimiento { get; set; } = string.Empty;

        /// <summary>Cantidad de días restantes hasta el vencimiento (0 = hoy).</summary>
        public int DiasRestantes      { get; set; }

        /// <summary>
        /// Clase CSS de Bootstrap según urgencia:
        /// 0–7 días → danger, 8–15 días → warning, 16–30 días → info.
        /// </summary>
        public string BadgeClass =>
            DiasRestantes <= 7  ? "danger"  :
            DiasRestantes <= 15 ? "warning" : "info";
    }
}
