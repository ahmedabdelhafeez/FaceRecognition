using FaceRecognition.Data;
using FaceRecognition.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Face Recognition API", Version = "v1" });
});

// SQLite — zero config, single file, perfect for this standalone service
// For SQL Server swap with: builder.Services.AddDbContext<FaceDbContext>(o => o.UseSqlServer(...))
builder.Services.AddDbContext<FaceDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("FaceDb")
        ?? "Data Source=faces.db"));

// FaceRecognitionService is SINGLETON — ONNX models load once and stay in memory.
// It uses IServiceScopeFactory internally to access the scoped DbContext safely.
builder.Services.AddSingleton<FaceRecognitionService>();

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// ── App ───────────────────────────────────────────────────────────────────────

var app = builder.Build();

// Auto-create the SQLite DB on startup (no migrations needed for this standalone project)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FaceDbContext>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.MapControllers();

// ── Health check endpoint ─────────────────────────────────────────────────────
app.MapGet("/health", () => new
{
    status = "healthy",
    time = DateTimeOffset.UtcNow,
    onnxDir = Path.Combine(AppContext.BaseDirectory, "onnx"),
    scrfd = File.Exists(Path.Combine(AppContext.BaseDirectory, "onnx", "scrfd_2.5g_kps.onnx")),
    arcface = File.Exists(Path.Combine(AppContext.BaseDirectory, "onnx", "arcface_lresnet100e_opset7.onnx"))
});

app.Run();