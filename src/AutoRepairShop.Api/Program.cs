using System.Security.Claims;
using System.Text;
using AutoRepairShop.Api.Middlewares;
using AutoRepairShop.Application.Interfaces;
using AutoRepairShop.Application.Interfaces.Services;
using AutoRepairShop.Application.Mapping;
using AutoRepairShop.Application.Security;
using AutoRepairShop.Application.Services;
using AutoRepairShop.Application.Settings;
using AutoRepairShop.Domain.Interfaces.Repositories;
using AutoRepairShop.Infrastructure.Data;
using AutoRepairShop.Infrastructure.Repositories;
using AutoRepairShop.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition(
        "Bearer",
        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Insira: Bearer {seu_token}",
        }
    );

    c.AddSecurityRequirement(
        new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer",
                    },
                },
                Array.Empty<string>()
            },
        }
    );
});

//JWT Settings

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();


// DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        o => o.EnableRetryOnFailure()
    )
);

//var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
//var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "1433";
//var db = Environment.GetEnvironmentVariable("DB_NAME") ?? "AutoRepairShopDb";
//var pass = Environment.GetEnvironmentVariable("SA_PASSWORD") ?? "YourStrong@Passw0rd";

//var conn = $"Server={host},{port};Database={db};User Id=sa;Password={pass};TrustServerCertificate=True";

//builder.Services.AddDbContext<AppDbContext>(options =>
//    options.UseSqlServer(conn));

// AutoMapper
builder.Services.AddAutoMapper(typeof(MapperProfile).Assembly);

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(EmailSettings.SectionName));

// Dependency Injection
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();

builder.Services.AddScoped<IVehicleService, VehicleService>();
builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();

builder.Services.AddScoped<IServiceService, ServiceService>();
builder.Services.AddScoped<IServiceRepository, ServiceRepository>();

builder.Services.AddScoped<ISupplyService, SupplyService>();
builder.Services.AddScoped<ISupplyRepository, SupplyRepository>();

builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddScoped<IServiceOrderRepository, ServiceOrderRepository>();
builder.Services.AddScoped<IServiceOrderService, ServiceOrderService>();

builder.Services.AddScoped<IAdminRepository, AdminRepository>();

builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

//Auth
var jwtKey =
    builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");

builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],

            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.NameIdentifier,
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var retry = 10;
    while (retry > 0)
    {
        try
        {
            database.Database.Migrate();
            break;
        }
        catch (Exception)
        {
            retry--;
            Console.WriteLine("Erro ao aplicar migrations. Tentando novamente...");
            Thread.Sleep(3000);
        }
    }
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
