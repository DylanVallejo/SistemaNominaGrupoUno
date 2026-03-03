using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SistemaNominaGrupoUno.Context;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<EmployeeManagementContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("EmployeeManagementConnection")));


//seccion para autentificacion

//builder.Services.AddIdentity<Users, IdentityRole>()
//    .AddEntityFrameworkStores<EmployeeManagementContext>()
//    .AddDefaultTokenProviders();



var app = builder.Build();

//definicion de roles
//using (var scope = app.Services.CreateScope())
//{
//    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

//    string[] roles = { "Administrador", "RRHH" };

//    foreach (var role in roles)
//    {
//        if (!await roleManager.RoleExistsAsync(role))
//            await roleManager.CreateAsync(new IdentityRole(role));
//    }
//}

//app.UseAuthentication();
//app.UseAuthorization();


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
