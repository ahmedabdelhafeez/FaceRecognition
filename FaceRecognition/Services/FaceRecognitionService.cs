using FaceAiSharp;
using FaceAiSharp.Extensions;
using FaceRecognition.Data;
using FaceRecognition.Entities;
using FaceRecognition.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FaceRecognition.Services;

// ── Result records ────────────────────────────────────────────────────────────

public sealed record IdentifyResult(
    Guid EmployeeId,
    string EmployeeName,
    float Confidence,
    bool IsMatch
);

public sealed record DetectResult(
    int FaceCount,
    float TopConfidence
);

// ── Service ───────────────────────────────────────────────────────────────────

public sealed class FaceRecognitionService
{
    private const float Threshold = 0.42f;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FaceRecognitionService> _logger;
    private readonly string _scrfdPath;
    private readonly string _arcFacePath;

    // Lazy: models load on first request, not at startup
    private readonly Lazy<IFaceDetectorWithLandmarks> _detectorLazy;
    private readonly Lazy<IFaceEmbeddingsGenerator> _generatorLazy;

    private IFaceDetectorWithLandmarks _detector => _detectorLazy.Value;
    private IFaceEmbeddingsGenerator _generator => _generatorLazy.Value;

    public FaceRecognitionService(
        IServiceScopeFactory scopeFactory,
        ILogger<FaceRecognitionService> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        // ✅ ALWAYS use AppContext.BaseDirectory as the root.
        // Never use a relative path — the working directory at runtime is unpredictable.
        // AppContext.BaseDirectory = folder where your .dll lives, e.g.:
        //   bin\Debug\net9.0\win-x64\
        var baseDir = AppContext.BaseDirectory;

        // If OnnxDirectory is configured, treat it as relative to BaseDirectory
        // unless it is already an absolute path.
        var onnxConfig = config["FaceRecognition:OnnxDirectory"];
        var onnxDir = string.IsNullOrWhiteSpace(onnxConfig)
            ? Path.Combine(baseDir, "onnx")
            : Path.IsPathRooted(onnxConfig)
                ? onnxConfig                              // absolute path → use as-is
                : Path.Combine(baseDir, onnxConfig);      // relative → combine with BaseDirectory

        // Make sure the path is fully resolved (no ".." or "." segments)
        onnxDir = Path.GetFullPath(onnxDir);

        _scrfdPath = Path.Combine(onnxDir, "scrfd_2.5g_kps.onnx");
        _arcFacePath = Path.Combine(onnxDir, "arcface_lresnet100e_opset7_int8.onnx");

        // Log at construction time so you can see exact paths in the console immediately
        _logger.LogInformation("=== FaceRecognitionService init ===");
        _logger.LogInformation("BaseDirectory : {Base}", baseDir);
        _logger.LogInformation("ONNX dir      : {Dir}", onnxDir);
        _logger.LogInformation("SCRFD         : {Path} | exists={E}", _scrfdPath, File.Exists(_scrfdPath));
        _logger.LogInformation("ArcFace       : {Path} | exists={E}", _arcFacePath, File.Exists(_arcFacePath));

        // ✅ Capture paths into locals for the lambdas (avoid closure over 'this' fields)
        var scrfdPath = _scrfdPath;
        var arcFacePath = _arcFacePath;

        _detectorLazy = new Lazy<IFaceDetectorWithLandmarks>(
            () => FaceAiSharpBundleFactory.CreateFaceDetectorWithLandmarks());

        _generatorLazy = new Lazy<IFaceEmbeddingsGenerator>(
            () => FaceAiSharpBundleFactory.CreateFaceEmbeddingsGenerator());
    }

    // ── PUBLIC API ────────────────────────────────────────────────────────────

    public Task<DetectResult> DetectAsync(Stream imageStream)
        => Task.Run(() =>
        {
            using var img = Image.Load<Rgb24>(imageStream);
            var faces = _detector.DetectFaces(img);
            var top = faces.OrderByDescending(f => f.Confidence ?? 0).FirstOrDefault();
            return new DetectResult(faces.Count(), top.Confidence ?? 0f);
        });

    public async Task RegisterAsync(
        Guid employeeId,
        string employeeName,
        Stream imageStream,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Registering face for employee {Id} – {Name}", employeeId, employeeName);

        var embedding = await ExtractEmbeddingAsync(imageStream);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FaceDbContext>();

        var existing = await db.FaceEmbeddings.FindAsync([employeeId], ct);
        if (existing is null)
        {
            db.FaceEmbeddings.Add(new EmployeeFaceEmbedding
            {
                EmployeeId = employeeId,
                EmployeeName = employeeName,
                EmbeddingBytes = EmployeeFaceEmbedding.ToBytes(embedding),
            });
        }
        else
        {
            existing.EmployeeName = employeeName;
            existing.EmbeddingBytes = EmployeeFaceEmbedding.ToBytes(embedding);
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Embedding saved for {Name}", employeeName);
    }

    public async Task<IdentifyResult?> IdentifyAsync(Stream imageStream, CancellationToken ct = default)
    {
        float[] query;
        try
        {
            query = await ExtractEmbeddingAsync(imageStream);
        }
        catch (FaceDetectionException ex)
        {
            _logger.LogWarning("No face detected: {Msg}", ex.Message);
            return null;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FaceDbContext>();

        var allEmbeddings = await db.FaceEmbeddings.AsNoTracking().ToListAsync(ct);

        if (allEmbeddings.Count == 0)
        {
            _logger.LogWarning("No employees registered yet");
            return null;
        }

        var (best, score) = FindBestMatch(query, allEmbeddings);

        _logger.LogInformation(
            "Best match: {Name} ({Id}) — similarity {Score:F4}",
            best.EmployeeName, best.EmployeeId, score);

        return new IdentifyResult(best.EmployeeId, best.EmployeeName, score, score >= Threshold);
    }

    public async Task RemoveAsync(Guid employeeId, CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FaceDbContext>();

        var row = await db.FaceEmbeddings.FindAsync([employeeId], ct);
        if (row is not null)
        {
            db.FaceEmbeddings.Remove(row);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<List<(Guid Id, string Name, DateTimeOffset UpdatedAt)>> GetAllAsync(
        CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FaceDbContext>();

        return await db.FaceEmbeddings
            .AsNoTracking()
            .Select(e => new ValueTuple<Guid, string, DateTimeOffset>(
                e.EmployeeId, e.EmployeeName, e.UpdatedAt))
            .ToListAsync(ct);
    }

    // ── PRIVATE HELPERS ───────────────────────────────────────────────────────

    private Task<float[]> ExtractEmbeddingAsync(Stream imageStream)
        => Task.Run(() =>
        {
            using var image = Image.Load<Rgb24>(imageStream);
            var faces = _detector.DetectFaces(image);

            if (!faces.Any())
                throw new FaceDetectionException("No face detected in the image.");

            var best = faces.OrderByDescending(f => f.Box.Width * f.Box.Height).First();

            if (best.Landmarks is null)
                throw new FaceDetectionException("Could not compute facial landmarks.");

            using var aligned = image.Clone();
            _generator.AlignFaceUsingLandmarks(aligned, best.Landmarks);

            return _generator.GenerateEmbedding(aligned);
        });

    private static (EmployeeFaceEmbedding best, float score) FindBestMatch(
        float[] query, List<EmployeeFaceEmbedding> candidates)
    {
        EmployeeFaceEmbedding? best = null;
        float bestScore = float.MinValue;

        foreach (var candidate in candidates)
        {
            float score = GeometryExtensions.Dot(query, candidate.GetEmbedding());
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return (best!, bestScore);
    }
}