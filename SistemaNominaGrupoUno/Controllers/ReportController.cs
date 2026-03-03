using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaNominaGrupoUno.Context;

namespace SistemaNominaGrupoUno.Controllers
{
    /// <summary>
    /// Controlador de reportes del sistema de nómina.
    /// RF-09: Generar reportes exportables a PDF y Excel:
    ///        a) Nómina vigente por departamento,
    ///        b) Cambios salariales en un rango de fechas,
    ///        c) Estructura organizacional (dept → manager → empleados).
    /// CU-05: El usuario selecciona parámetros; el sistema calcula y exporta.
    /// RNF-03: Documentación XML en controladores.
    /// </summary>
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class ReportController(EmployeeManagementContext context) : Controller
    {
        private readonly string _today = DateTime.Today.ToString("yyyy-MM-dd");

        // ─────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Carga el SelectList de departamentos activos en ViewBag.
        /// RF-09: Filtro por departamento en reportes de nómina y estructura.
        /// </summary>
        private async Task LoadDepartmentsAsync(int? selected = null)
        {
            var depts = await context.Departments
                .Where(d => d.IsActive)
                .OrderBy(d => d.DeptName)
                .Select(d => new { d.DeptNo, d.DeptName })
                .ToListAsync();

            ViewBag.Departments = new SelectList(depts, "DeptNo", "DeptName", selected);
        }

        // ─────────────────────────────────────────────────────────────
        // INDEX
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Página principal de reportes con acceso a los tres tipos disponibles.
        /// RF-09: Punto de entrada al módulo de reportes.
        /// </summary>
        public IActionResult Index() => View();

        // ═════════════════════════════════════════════════════════════
        // REPORTE A — NÓMINA VIGENTE POR DEPARTAMENTO
        // ═════════════════════════════════════════════════════════════

        /// <summary>
        /// Muestra el formulario y la vista previa del reporte de nómina vigente
        /// filtrado opcionalmente por departamento.
        /// RF-09a: Nómina vigente por departamento.
        /// CU-05: Usuario selecciona departamento; sistema calcula salarios activos.
        /// </summary>
        public async Task<IActionResult> NominaVigente(int? deptNo)
        {
            await LoadDepartmentsAsync(deptNo);
            var rows = await GetNominaVigenteAsync(deptNo);
            ViewBag.DeptNo     = deptNo;
            ViewBag.DeptNombre = deptNo != null
                ? (await context.Departments.FindAsync(deptNo))?.DeptName
                : "Todos los departamentos";
            return View(rows);
        }

        /// <summary>
        /// Exporta el reporte de nómina vigente a PDF.
        /// RF-09a: Exportable a PDF.
        /// </summary>
        public async Task<IActionResult> NominaVigentePdf(int? deptNo)
        {
            var rows      = await GetNominaVigenteAsync(deptNo);
            var deptNombre = deptNo != null
                ? (await context.Departments.FindAsync(deptNo))?.DeptName ?? deptNo.ToString()!
                : "Todos los departamentos";

            var pdf = GenerarNominaVigentePdf(rows, deptNombre);
            return File(pdf, "application/pdf",
                $"nomina_vigente_{DateTime.Today:yyyyMMdd}.pdf");
        }

        /// <summary>
        /// Exporta el reporte de nómina vigente a Excel.
        /// RF-09a: Exportable a Excel.
        /// </summary>
        public async Task<IActionResult> NominaVigenteExcel(int? deptNo)
        {
            var rows      = await GetNominaVigenteAsync(deptNo);
            var deptNombre = deptNo != null
                ? (await context.Departments.FindAsync(deptNo))?.DeptName ?? deptNo.ToString()!
                : "Todos los departamentos";

            var bytes = GenerarNominaVigenteExcel(rows, deptNombre);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"nomina_vigente_{DateTime.Today:yyyyMMdd}.xlsx");
        }

        // ═════════════════════════════════════════════════════════════
        // REPORTE B — CAMBIOS SALARIALES EN RANGO DE FECHAS
        // ═════════════════════════════════════════════════════════════

        /// <summary>
        /// Muestra el formulario y la vista previa del reporte de cambios salariales
        /// dentro del rango de fechas indicado.
        /// RF-09b: Cambios salariales en un rango de fechas.
        /// </summary>
        public async Task<IActionResult> CambiosSalariales(string? desde, string? hasta)
        {
            desde ??= new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
                         .ToString("yyyy-MM-dd");
            hasta ??= _today;

            ViewBag.Desde = desde;
            ViewBag.Hasta = hasta;

            var rows = await GetCambiosSalarialesAsync(desde, hasta);
            return View(rows);
        }

        /// <summary>
        /// Exporta el reporte de cambios salariales a PDF.
        /// RF-09b: Exportable a PDF.
        /// </summary>
        public async Task<IActionResult> CambiosSalarialesPdf(string desde, string hasta)
        {
            var rows = await GetCambiosSalarialesAsync(desde, hasta);
            var pdf  = GenerarCambiosSalarialesPdf(rows, desde, hasta);
            return File(pdf, "application/pdf",
                $"cambios_salariales_{desde}_{hasta}.pdf");
        }

        /// <summary>
        /// Exporta el reporte de cambios salariales a Excel.
        /// RF-09b: Exportable a Excel.
        /// </summary>
        public async Task<IActionResult> CambiosSalarialesExcel(string desde, string hasta)
        {
            var rows  = await GetCambiosSalarialesAsync(desde, hasta);
            var bytes = GenerarCambiosSalarialesExcel(rows, desde, hasta);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"cambios_salariales_{desde}_{hasta}.xlsx");
        }

        // ═════════════════════════════════════════════════════════════
        // REPORTE C — ESTRUCTURA ORGANIZACIONAL
        // ═════════════════════════════════════════════════════════════

        /// <summary>
        /// Muestra el formulario y la vista previa de la estructura organizacional:
        /// departamento → gerente vigente → empleados asignados vigentes.
        /// RF-09c: Estructura organizacional (dept → manager → empleados).
        /// </summary>
        public async Task<IActionResult> EstructuraOrganizacional(int? deptNo)
        {
            await LoadDepartmentsAsync(deptNo);
            var estructura = await GetEstructuraAsync(deptNo);
            ViewBag.DeptNo = deptNo;
            return View(estructura);
        }

        /// <summary>
        /// Exporta la estructura organizacional a PDF.
        /// RF-09c: Exportable a PDF.
        /// </summary>
        public async Task<IActionResult> EstructuraOrganizacionalPdf(int? deptNo)
        {
            var estructura = await GetEstructuraAsync(deptNo);
            var pdf        = GenerarEstructuraPdf(estructura);
            return File(pdf, "application/pdf",
                $"estructura_organizacional_{DateTime.Today:yyyyMMdd}.pdf");
        }

        /// <summary>
        /// Exporta la estructura organizacional a Excel.
        /// RF-09c: Exportable a Excel.
        /// </summary>
        public async Task<IActionResult> EstructuraOrganizacionalExcel(int? deptNo)
        {
            var estructura = await GetEstructuraAsync(deptNo);
            var bytes      = GenerarEstructuraExcel(estructura);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"estructura_organizacional_{DateTime.Today:yyyyMMdd}.xlsx");
        }

        // ─────────────────────────────────────────────────────────────
        // HELPERS DE DATOS
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Consulta empleados activos con su salario vigente, filtrados
        /// opcionalmente por departamento.
        /// RF-09a / CU-05: Calcular salarios activos por departamento.
        /// </summary>
        private async Task<List<NominaVigenteRow>> GetNominaVigenteAsync(int? deptNo)
        {
            var query = context.DeptEmps
                .Include(de => de.Employee)
                    .ThenInclude(e => e.Salaries)
                .Include(de => de.Department)
                .Where(de =>
                    (string.IsNullOrEmpty(de.ToDate) ||
                     string.Compare(de.ToDate, _today) >= 0) &&
                    de.Employee.IsActive);

            if (deptNo.HasValue)
                query = query.Where(de => de.DeptNo == deptNo.Value);

            var assignments = await query
                .OrderBy(de => de.Department.DeptName)
                .ThenBy(de => de.Employee.LastName)
                .ToListAsync();

            var rows = new List<NominaVigenteRow>();
            foreach (var de in assignments)
            {
                var salario = de.Employee.Salaries
                    .Where(s => string.IsNullOrEmpty(s.ToDate) ||
                                string.Compare(s.ToDate, _today) >= 0)
                    .OrderByDescending(s => s.FromDate)
                    .FirstOrDefault();

                rows.Add(new NominaVigenteRow
                {
                    DeptName       = de.Department.DeptName,
                    EmpNo          = de.Employee.EmpNo,
                    NombreCompleto = $"{de.Employee.LastName}, {de.Employee.FirstName}",
                    Salario        = salario?.Salary ?? 0,
                    Desde          = salario?.FromDate ?? "-"
                });
            }

            return rows;
        }

        /// <summary>
        /// Consulta el log de auditoría salarial dentro del rango [desde, hasta].
        /// RF-09b: Cambios salariales en un rango de fechas.
        /// </summary>
        private async Task<List<LogAuditoriaSalarios>> GetCambiosSalarialesAsync(
            string desde, string hasta)
        {
            var desdeDate = DateTime.Parse(desde);
            var hastaDate = DateTime.Parse(hasta).AddDays(1); // inclusivo

            return await context.LogAuditoriaSalarios
                .Include(l => l.Employee)
                .Where(l => l.FechaActualizacion >= desdeDate &&
                            l.FechaActualizacion < hastaDate)
                .OrderByDescending(l => l.FechaActualizacion)
                .ToListAsync();
        }

        /// <summary>
        /// Construye la estructura organizacional departamento → gerente → empleados.
        /// RF-09c: Estructura organizacional vigente.
        /// </summary>
        private async Task<List<DeptEstructura>> GetEstructuraAsync(int? deptNo)
        {
            var deptQuery = context.Departments.Where(d => d.IsActive);
            if (deptNo.HasValue)
                deptQuery = deptQuery.Where(d => d.DeptNo == deptNo.Value);

            var departamentos = await deptQuery
                .OrderBy(d => d.DeptName)
                .ToListAsync();

            var estructura = new List<DeptEstructura>();

            foreach (var dept in departamentos)
            {
                var manager = await context.DeptManagers
                    .Include(dm => dm.Employee)
                    .Where(dm => dm.DeptNo == dept.DeptNo &&
                                 (string.IsNullOrEmpty(dm.ToDate) ||
                                  string.Compare(dm.ToDate, _today) >= 0))
                    .OrderByDescending(dm => dm.FromDate)
                    .FirstOrDefaultAsync();

                var empleados = await context.DeptEmps
                    .Include(de => de.Employee)
                    .Where(de => de.DeptNo == dept.DeptNo &&
                                 de.Employee.IsActive &&
                                 (string.IsNullOrEmpty(de.ToDate) ||
                                  string.Compare(de.ToDate, _today) >= 0))
                    .OrderBy(de => de.Employee.LastName)
                    .Select(de => $"{de.Employee.LastName}, {de.Employee.FirstName}")
                    .ToListAsync();

                estructura.Add(new DeptEstructura
                {
                    DeptNo        = dept.DeptNo,
                    DeptName      = dept.DeptName,
                    ManagerNombre = manager != null
                        ? $"{manager.Employee.LastName}, {manager.Employee.FirstName}"
                        : "Sin gerente asignado",
                    Empleados     = empleados
                });
            }

            return estructura;
        }

        // ─────────────────────────────────────────────────────────────
        // GENERACIÓN PDF — QuestPDF
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Genera el PDF del reporte de nómina vigente.
        /// RF-09a: Exportable a PDF con filtros aplicados.
        /// </summary>
        private static byte[] GenerarNominaVigentePdf(
            List<NominaVigenteRow> rows, string titulo)
        {
            return Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(t => t.FontSize(9));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Sistema de Nómina — Grupo Uno")
                            .FontSize(14).Bold().AlignCenter();
                        col.Item().Text($"Nómina Vigente — {titulo}")
                            .FontSize(11).AlignCenter();
                        col.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}")
                            .FontSize(8).AlignCenter().FontColor(Colors.Grey.Medium);
                        col.Item().PaddingTop(4).LineHorizontal(0.5f);
                    });

                    page.Content().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3);
                            cols.ConstantColumn(40);
                            cols.RelativeColumn(4);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                        });

                        static IContainer HeaderCell(IContainer c) =>
                            c.Background(Colors.Grey.Darken3).Padding(4);

                        table.Header(h =>
                        {
                            h.Cell().Element(HeaderCell).Text("Departamento")
                                .FontColor(Colors.White).Bold();
                            h.Cell().Element(HeaderCell).Text("#")
                                .FontColor(Colors.White).Bold();
                            h.Cell().Element(HeaderCell).Text("Empleado")
                                .FontColor(Colors.White).Bold();
                            h.Cell().Element(HeaderCell).AlignRight().Text("Salario")
                                .FontColor(Colors.White).Bold();
                            h.Cell().Element(HeaderCell).Text("Vigente Desde")
                                .FontColor(Colors.White).Bold();
                        });

                        static IContainer RowCell(IContainer c, bool alt) =>
                            c.Background(alt ? Colors.Grey.Lighten4 : Colors.White).Padding(4);

                        for (int i = 0; i < rows.Count; i++)
                        {
                            var r = rows[i];
                            bool alt = i % 2 == 1;
                            table.Cell().Element(c => RowCell(c, alt)).Text(r.DeptName);
                            table.Cell().Element(c => RowCell(c, alt)).Text($"#{r.EmpNo}");
                            table.Cell().Element(c => RowCell(c, alt)).Text(r.NombreCompleto);
                            table.Cell().Element(c => RowCell(c, alt)).AlignRight()
                                        .Text($"${r.Salario:N0}");
                            table.Cell().Element(c => RowCell(c, alt)).Text(r.Desde);
                        }
                    });

                    page.Footer().AlignCenter()
                        .Text(t =>
                        {
                            t.Span("Página ").FontSize(8);
                            t.CurrentPageNumber().FontSize(8);
                            t.Span(" de ").FontSize(8);
                            t.TotalPages().FontSize(8);
                        });
                });
            }).GeneratePdf();
        }

        /// <summary>
        /// Genera el PDF del reporte de cambios salariales.
        /// RF-09b: Exportable a PDF con filtros de rango de fechas.
        /// </summary>
        private static byte[] GenerarCambiosSalarialesPdf(
            List<LogAuditoriaSalarios> rows, string desde, string hasta)
        {
            return Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(t => t.FontSize(8));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Sistema de Nómina — Grupo Uno")
                            .FontSize(14).Bold().AlignCenter();
                        col.Item().Text($"Cambios Salariales del {desde} al {hasta}")
                            .FontSize(11).AlignCenter();
                        col.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}")
                            .FontSize(8).AlignCenter().FontColor(Colors.Grey.Medium);
                        col.Item().PaddingTop(4).LineHorizontal(0.5f);
                    });

                    page.Content().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(30);
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(5);
                            cols.RelativeColumn(2);
                        });

                        static IContainer H(IContainer c) =>
                            c.Background(Colors.Grey.Darken3).Padding(3);

                        table.Header(h =>
                        {
                            h.Cell().Element(H).Text("#").FontColor(Colors.White).Bold();
                            h.Cell().Element(H).Text("Empleado").FontColor(Colors.White).Bold();
                            h.Cell().Element(H).AlignRight().Text("Salario")
                                .FontColor(Colors.White).Bold();
                            h.Cell().Element(H).Text("Usuario").FontColor(Colors.White).Bold();
                            h.Cell().Element(H).Text("Detalle del Cambio")
                                .FontColor(Colors.White).Bold();
                            h.Cell().Element(H).Text("Fecha / Hora").FontColor(Colors.White).Bold();
                        });

                        static IContainer R(IContainer c, bool alt) =>
                            c.Background(alt ? Colors.Grey.Lighten4 : Colors.White).Padding(3);

                        for (int i = 0; i < rows.Count; i++)
                        {
                            var r = rows[i];
                            bool alt = i % 2 == 1;
                            table.Cell().Element(c => R(c, alt)).Text($"{r.Id}");
                            table.Cell().Element(c => R(c, alt))
                                        .Text($"#{r.EmpNo} {r.Employee.LastName}, {r.Employee.FirstName}");
                            table.Cell().Element(c => R(c, alt)).AlignRight()
                                        .Text($"${r.Salario:N0}");
                            table.Cell().Element(c => R(c, alt)).Text(r.Usuario);
                            table.Cell().Element(c => R(c, alt)).Text(r.DetalleCambio);
                            table.Cell().Element(c => R(c, alt))
                                        .Text(r.FechaActualizacion.ToString("dd/MM/yyyy HH:mm"));
                        }
                    });

                    page.Footer().AlignCenter()
                        .Text(t =>
                        {
                            t.Span("Página ").FontSize(8);
                            t.CurrentPageNumber().FontSize(8);
                            t.Span(" de ").FontSize(8);
                            t.TotalPages().FontSize(8);
                        });
                });
            }).GeneratePdf();
        }

        /// <summary>
        /// Genera el PDF de la estructura organizacional.
        /// RF-09c: Exportable a PDF.
        /// </summary>
        private static byte[] GenerarEstructuraPdf(List<DeptEstructura> estructura)
        {
            return Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(t => t.FontSize(9));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("Sistema de Nómina — Grupo Uno")
                            .FontSize(14).Bold().AlignCenter();
                        col.Item().Text("Estructura Organizacional Vigente")
                            .FontSize(11).AlignCenter();
                        col.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}")
                            .FontSize(8).AlignCenter().FontColor(Colors.Grey.Medium);
                        col.Item().PaddingTop(4).LineHorizontal(0.5f);
                    });

                    page.Content().PaddingTop(10).Column(main =>
                    {
                        foreach (var dept in estructura)
                        {
                            main.Item().PaddingBottom(8).Column(col =>
                            {
                                col.Item()
                                    .Background(Colors.Grey.Darken3)
                                    .Padding(6)
                                    .Text($"[{dept.DeptNo}]  {dept.DeptName}")
                                    .FontColor(Colors.White).Bold().FontSize(10);

                                col.Item()
                                    .Background(Colors.Blue.Lighten4)
                                    .PaddingLeft(16).Padding(4)
                                    .Text(t =>
                                    {
                                        t.Span("Gerente: ").Bold();
                                        t.Span(dept.ManagerNombre);
                                    });

                                col.Item()
                                    .Background(Colors.Grey.Lighten4)
                                    .PaddingLeft(32).PaddingTop(4).PaddingBottom(4)
                                    .Column(emp =>
                                    {
                                        if (dept.Empleados.Count == 0)
                                        {
                                            emp.Item().Text("Sin empleados asignados")
                                                .Italic().FontColor(Colors.Grey.Medium);
                                        }
                                        else
                                        {
                                            foreach (var e in dept.Empleados)
                                                emp.Item().Text($"• {e}");
                                        }
                                    });
                            });
                        }
                    });

                    page.Footer().AlignCenter()
                        .Text(t =>
                        {
                            t.Span("Página ").FontSize(8);
                            t.CurrentPageNumber().FontSize(8);
                            t.Span(" de ").FontSize(8);
                            t.TotalPages().FontSize(8);
                        });
                });
            }).GeneratePdf();
        }

        // ─────────────────────────────────────────────────────────────
        // GENERACIÓN EXCEL — ClosedXML
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Genera el Excel del reporte de nómina vigente.
        /// RF-09a: Exportable a Excel con filtros aplicados.
        /// </summary>
        private static byte[] GenerarNominaVigenteExcel(
            List<NominaVigenteRow> rows, string titulo)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Nómina Vigente");

            ws.Cell(1, 1).Value = "Sistema de Nómina — Grupo Uno";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Range(1, 1, 1, 5).Merge();

            ws.Cell(2, 1).Value = $"Nómina Vigente — {titulo}";
            ws.Cell(2, 1).Style.Font.FontSize = 11;
            ws.Range(2, 1, 2, 5).Merge();

            ws.Cell(3, 1).Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";
            ws.Cell(3, 1).Style.Font.FontSize = 8;
            ws.Cell(3, 1).Style.Font.FontColor = XLColor.Gray;
            ws.Range(3, 1, 3, 5).Merge();

            string[] headers = ["Departamento", "#", "Empleado", "Salario", "Vigente Desde"];
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(5, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#374151");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                int row = i + 6;
                ws.Cell(row, 1).Value = r.DeptName;
                ws.Cell(row, 2).Value = r.EmpNo;
                ws.Cell(row, 3).Value = r.NombreCompleto;
                ws.Cell(row, 4).Value = r.Salario;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 5).Value = r.Desde;

                if (i % 2 == 1)
                    ws.Range(row, 1, row, 5).Style
                        .Fill.BackgroundColor = XLColor.FromHtml("#F3F4F6");
            }

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Genera el Excel del reporte de cambios salariales.
        /// RF-09b: Exportable a Excel con rango de fechas.
        /// </summary>
        private static byte[] GenerarCambiosSalarialesExcel(
            List<LogAuditoriaSalarios> rows, string desde, string hasta)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Cambios Salariales");

            ws.Cell(1, 1).Value = "Sistema de Nómina — Grupo Uno";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Range(1, 1, 1, 6).Merge();

            ws.Cell(2, 1).Value = $"Cambios Salariales del {desde} al {hasta}";
            ws.Cell(2, 1).Style.Font.FontSize = 11;
            ws.Range(2, 1, 2, 6).Merge();

            ws.Cell(3, 1).Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";
            ws.Cell(3, 1).Style.Font.FontSize = 8;
            ws.Cell(3, 1).Style.Font.FontColor = XLColor.Gray;
            ws.Range(3, 1, 3, 6).Merge();

            string[] headers = ["#", "Empleado", "Salario", "Usuario", "Detalle del Cambio", "Fecha / Hora"];
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(5, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#374151");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                int row = i + 6;
                ws.Cell(row, 1).Value = r.Id;
                ws.Cell(row, 2).Value = $"#{r.EmpNo} {r.Employee.LastName}, {r.Employee.FirstName}";
                ws.Cell(row, 3).Value = r.Salario;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 4).Value = r.Usuario;
                ws.Cell(row, 5).Value = r.DetalleCambio;
                ws.Cell(row, 6).Value = r.FechaActualizacion.ToString("dd/MM/yyyy HH:mm");

                if (i % 2 == 1)
                    ws.Range(row, 1, row, 6).Style
                        .Fill.BackgroundColor = XLColor.FromHtml("#F3F4F6");
            }

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Genera el Excel de la estructura organizacional.
        /// RF-09c: Exportable a Excel.
        /// </summary>
        private static byte[] GenerarEstructuraExcel(List<DeptEstructura> estructura)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Estructura Organizacional");

            ws.Cell(1, 1).Value = "Sistema de Nómina — Grupo Uno";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Range(1, 1, 1, 4).Merge();

            ws.Cell(2, 1).Value = "Estructura Organizacional Vigente";
            ws.Cell(2, 1).Style.Font.FontSize = 11;
            ws.Range(2, 1, 2, 4).Merge();

            ws.Cell(3, 1).Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";
            ws.Cell(3, 1).Style.Font.FontSize = 8;
            ws.Cell(3, 1).Style.Font.FontColor = XLColor.Gray;
            ws.Range(3, 1, 3, 4).Merge();

            string[] headers = ["Departamento", "Cód.", "Gerente", "Empleados Asignados"];
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(5, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#374151");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            int row = 6;
            foreach (var dept in estructura)
            {
                ws.Cell(row, 1).Value = dept.DeptName;
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1F2937");
                ws.Cell(row, 1).Style.Font.FontColor = XLColor.White;

                ws.Cell(row, 2).Value = dept.DeptNo;
                ws.Cell(row, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#1F2937");
                ws.Cell(row, 2).Style.Font.FontColor = XLColor.White;

                ws.Cell(row, 3).Value = dept.ManagerNombre;
                ws.Cell(row, 3).Style.Fill.BackgroundColor = XLColor.FromHtml("#DBEAFE");

                ws.Cell(row, 4).Value = dept.Empleados.Count == 0
                    ? "Sin empleados asignados"
                    : string.Join(", ", dept.Empleados);
                ws.Cell(row, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#EFF6FF");

                row++;
            }

            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // MODELOS INTERNOS DE REPORTE
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Fila del reporte de nómina vigente por departamento.
    /// RF-09a: Campos requeridos para el reporte de nómina.
    /// </summary>
    public class NominaVigenteRow
    {
        /// <summary>Nombre del departamento.</summary>
        public string DeptName { get; set; } = string.Empty;

        /// <summary>Número de empleado.</summary>
        public int EmpNo { get; set; }

        /// <summary>Apellido y nombre del empleado.</summary>
        public string NombreCompleto { get; set; } = string.Empty;

        /// <summary>Salario activo del empleado.</summary>
        public long Salario { get; set; }

        /// <summary>Fecha desde la que está vigente el salario.</summary>
        public string Desde { get; set; } = string.Empty;
    }

    /// <summary>
    /// Nodo de la estructura organizacional de un departamento.
    /// RF-09c: dept → manager → empleados.
    /// </summary>
    public class DeptEstructura
    {
        /// <summary>Código del departamento.</summary>
        public int DeptNo { get; set; }

        /// <summary>Nombre del departamento.</summary>
        public string DeptName { get; set; } = string.Empty;

        /// <summary>Nombre del gerente vigente.</summary>
        public string ManagerNombre { get; set; } = string.Empty;

        /// <summary>Lista de nombres de empleados vigentemente asignados.</summary>
        public List<string> Empleados { get; set; } = [];
    }
}
