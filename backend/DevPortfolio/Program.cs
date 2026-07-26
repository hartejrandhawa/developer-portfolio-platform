using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using DevPortfolio.Data;
using DevPortfolio.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Database ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Services ---
builder.Services.AddScoped<JwtService>();

// --- CORS ---
var frontendOrigin = builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173";
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(frontendOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// --- Authentication ---
// Two schemes:
//  - Cookie: temporary, used only during the GitHub OAuth handshake
//  - JwtBearer: used for all actual API calls after login
//  - GitHub: the external OAuth provider itself
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSecret = builder.Configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
        };
    })
    .AddGitHub("GitHub", options =>
    {
        options.ClientId = builder.Configuration["GitHub:ClientId"]
            ?? throw new InvalidOperationException("GitHub:ClientId is not configured");
        options.ClientSecret = builder.Configuration["GitHub:ClientSecret"]
            ?? throw new InvalidOperationException("GitHub:ClientSecret is not configured");
        options.CallbackPath = "/api/auth/github/callback";
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.Scope.Add("read:user");

        // Explicitly map GitHub's API response fields to claims, using GitHub's
        // documented user-object field names directly, rather than relying on
        // this package's default claim mapping (safer since it's verifiable
        // against GitHub's own API docs).
        options.ClaimActions.MapJsonKey("urn:github:id", "id");
        options.ClaimActions.MapJsonKey("urn:github:login", "login");
        options.ClaimActions.MapJsonKey("urn:github:avatarurl", "avatar_url");
        options.ClaimActions.MapJsonKey(System.Security.Claims.ClaimTypes.Name, "name");
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
