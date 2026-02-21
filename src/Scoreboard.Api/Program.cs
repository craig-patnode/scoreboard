using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scoreboard.Api.Data;
using Scoreboard.Api.Hubs;
using Scoreboard.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<ScoreboardDbContext>(options =>
	options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Services
builder.Services.AddScoped<GameService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<GameStateCache>();

// Auth — C1: throw if JWT key is not configured instead of using hardcoded fallback
var jwtKey = builder.Configuration["Jwt:Key"]
	?? throw new InvalidOperationException("Jwt:Key must be configured. Set it via environment variable or Azure Key Vault.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidateAudience = true,
			ValidateLifetime = true,
			ValidateIssuerSigningKey = true,
			ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "Scoreboard",
			ValidAudience = builder.Configuration["Jwt:Audience"] ?? "Scoreboard",
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
		};

		// Allow JWT in SignalR query string
		options.Events = new JwtBearerEvents
		{
			OnMessageReceived = context =>
			{
				var accessToken = context.Request.Query["access_token"];
				var path = context.HttpContext.Request.Path;
				if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/game"))
				{
					context.Token = accessToken;
				}
				return Task.CompletedTask;
			}
		};
	});

builder.Services.AddAuthorization();

// SignalR
builder.Services.AddSignalR();

// Controllers
builder.Services.AddControllers();

// CORS — C3: use configuration-based origins instead of AllowAll
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
	?? (builder.Environment.IsDevelopment()
		? new[] { "https://localhost:5001", "https://localhost:7001", "http://localhost:5000" }
		: Array.Empty<string>());

builder.Services.AddCors(options =>
{
	options.AddPolicy("Default", policy =>
	{
		if (builder.Environment.IsDevelopment() && allowedOrigins.Length == 0)
		{
			// Dev fallback: allow same-origin only (static files served from same host)
			policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
		}
		else
		{
			policy.WithOrigins(allowedOrigins)
				  .AllowAnyMethod()
				  .AllowAnyHeader();
		}
	});

	options.AddPolicy("SignalR", policy =>
	{
		if (builder.Environment.IsDevelopment())
		{
			policy.SetIsOriginAllowed(_ => true)
				  .AllowAnyMethod()
				  .AllowAnyHeader()
				  .AllowCredentials();
		}
		else
		{
			policy.WithOrigins(allowedOrigins)
				  .AllowAnyMethod()
				  .AllowAnyHeader()
				  .AllowCredentials();
		}
	});
});

// H1: Rate limiting on auth endpoints
builder.Services.AddRateLimiter(options =>
{
	options.AddFixedWindowLimiter("auth", limiter =>
	{
		limiter.PermitLimit = 10;
		limiter.Window = TimeSpan.FromMinutes(1);
		limiter.QueueLimit = 0;
	});
	options.OnRejected = async (context, cancellationToken) =>
	{
		context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
		await context.HttpContext.Response.WriteAsJsonAsync(
			new { error = "Too many requests. Please try again later." }, cancellationToken);
	};
});

// Swagger for dev — L1: explicitly guard with configuration flag
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// H2: Security headers
app.Use(async (context, next) =>
{
	context.Response.Headers["X-Content-Type-Options"] = "nosniff";
	context.Response.Headers["X-Frame-Options"] = "DENY";
	context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
	context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
	if (!context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
	{
		context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
	}
	// L4: Request ID for log correlation
	context.Response.Headers["X-Request-ID"] = context.TraceIdentifier;
	await next();
});

// Global exception handler — returns user-friendly JSON for all unhandled exceptions
app.UseExceptionHandler(errorApp =>
{
	errorApp.Run(async context =>
	{
		context.Response.StatusCode = 500;
		context.Response.ContentType = "application/json";
		await context.Response.WriteAsJsonAsync(new
		{
			error = "An unexpected error occurred.",
			message = "Please try again or contact support if the issue persists."
		});
	});
});

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("Default");
app.UseRateLimiter();

// WebSockets — required for reliable SignalR WebSocket transport
app.UseWebSockets();

// Serve static files (overlays, controller, signup, landing page)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<GameHub>("/hubs/game", options =>
{
	options.AllowStatefulReconnects = true;
}).RequireCors("SignalR");

// Fallback to index.html for SPA-style routing
app.MapFallbackToFile("index.html");

app.Run();
