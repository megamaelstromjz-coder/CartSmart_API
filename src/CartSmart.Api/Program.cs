using System.Text;
using CartSmart.Api.Auth;
using CartSmart.Api.Data;
using CartSmart.Api.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

builder.Services.AddDbContext<CartSmartDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("CartSmartDb")));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<GoogleAuthOptions>(builder.Configuration.GetSection(GoogleAuthOptions.SectionName));
builder.Services.Configure<AppleAuthOptions>(builder.Configuration.GetSection(AppleAuthOptions.SectionName));

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IPasswordHasherService, PasswordHasherService>();
builder.Services.AddScoped<IGoogleIdTokenValidator, GoogleIdTokenValidator>();
builder.Services.AddScoped<IAppleIdTokenValidator, AppleIdTokenValidator>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CartSmartDbContext>();
    await db.Database.MigrateAsync();
    await ReferenceDataSeeder.SeedAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("/api/v1/auth")
    .MapAuthEndpoints();

app.MapGroup("/api/v1/devices")
    .RequireAuthorization()
    .MapDeviceEndpoints();

app.MapGroup("/api/v1/lists")
    .RequireAuthorization()
    .MapListEndpoints();

app.MapGroup("/api/v1/sync")
    .RequireAuthorization()
    .MapSyncEndpoints();

app.MapGroup("/api/v1/account")
    .RequireAuthorization()
    .MapAccountEndpoints();

// Public: this is bundled reference/autocomplete data, not user data.
app.MapGroup("/api/v1/reference")
    .MapReferenceEndpoints();

app.Run();

public partial class Program;
