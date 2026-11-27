using System.Text;
using Microsoft.AspNetCore.Authentication;
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
builder.Services.AddScoped<ActivityService>();
builder.Services.AddSingleton(TimeProvider.System); // injectable clock, so tests can pin "today"
builder.Services.AddHttpClient<GitHubService>((sp, client) =>
    GitHubService.Configure(client, sp.GetRequiredService<IConfiguration>()));

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

        // Without these, a user clicking "Cancel" on GitHub's consent screen (or any
        // OAuth failure) surfaces as an unhandled exception page. Send them back to
        // the frontend with a readable error code instead.
        var frontendHome = builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173";
        options.Events.OnAccessDenied = context =>
        {
            context.Response.Redirect($"{frontendHome}/?error=access_denied");
            context.HandleResponse();
            return Task.CompletedTask;
        };
        options.Events.OnRemoteFailure = context =>
        {
            context.Response.Redirect($"{frontendHome}/?error=login_failed");
            context.HandleResponse();
            return Task.CompletedTask;
        };

        // The correlation cookie defaults to SameSite=None + Secure, which
        // requires HTTPS — SameSite=None without Secure is an invalid
        // combination that browsers silently refuse to store at all.
        // Locally the API runs over plain HTTP, so drop to Lax (still sent
        // on the top-level redirect back from GitHub) and allow non-Secure.
        // Only in Development; production is expected to run behind HTTPS.
        if (builder.Environment.IsDevelopment())
        {
            options.CorrelationCookie.SameSite = SameSiteMode.Lax;
            options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        }
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers(options => options.Filters.Add<GitHubApiExceptionFilter>());
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
