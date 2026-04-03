using FaceRecognition.Services;
using Microsoft.AspNetCore.Mvc;

namespace FaceRecognition.Controllers;

[ApiController]
[Route("api/face")]
public sealed class FaceController : ControllerBase
{
    private readonly FaceRecognitionService _svc;

    public FaceController(FaceRecognitionService svc) => _svc = svc;

    // ── DETECT ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Detect faces in an image — returns count and top confidence score.
    /// </summary>
    [HttpPost("detect")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Detect(IFormFile photo)
    {
        if (photo is null || photo.Length == 0)
            return BadRequest(new { error = "No photo provided." });

        await using var stream = photo.OpenReadStream();
        var result = await _svc.DetectAsync(stream);

        return Ok(new
        {
            faceCount = result.FaceCount,
            topConfidence = result.TopConfidence,
            facesDetected = result.FaceCount > 0
        });
    }

    // ── REGISTER ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Register or update an employee's face photo.
    /// </summary>
    [HttpPost("employees/{employeeId:guid}/register")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Register(
        Guid employeeId,
        [FromForm] string employeeName,
        IFormFile photo,
        CancellationToken ct)
    {
        if (photo is null || photo.Length == 0)
            return BadRequest(new { error = "No photo provided." });

        if (string.IsNullOrWhiteSpace(employeeName))
            return BadRequest(new { error = "Employee name is required." });

        try
        {
            await using var stream = photo.OpenReadStream();
            await _svc.RegisterAsync(employeeId, employeeName, stream, ct);
            return Ok(new { message = "Face registered successfully.", employeeId, employeeName });
        }
        catch (Exception ex) when (ex.Message.Contains("No face detected"))
        {
            return BadRequest(new { error = "No face detected in the uploaded photo." });
        }
    }

    // ── IDENTIFY ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Identify which employee appears in the photo.
    /// </summary>
    [HttpPost("identify")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Identify(IFormFile photo, CancellationToken ct)
    {
        if (photo is null || photo.Length == 0)
            return BadRequest(new { error = "No photo provided." });

        await using var stream = photo.OpenReadStream();
        var result = await _svc.IdentifyAsync(stream, ct);

        if (result is null)
            return NotFound(new { error = "No face detected or no employees registered." });

        if (!result.IsMatch)
            return Ok(new
            {
                identified = false,
                message = "Face detected but did not match any employee.",
                bestConfidence = result.Confidence
            });

        return Ok(new
        {
            identified = true,
            employeeId = result.EmployeeId,
            employeeName = result.EmployeeName,
            confidence = result.Confidence
        });
    }

    // ── LIST ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// List all registered employees.
    /// </summary>
    [HttpGet("employees")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var employees = await _svc.GetAllAsync(ct);
        return Ok(employees.Select(e => new
        {
            employeeId = e.Id,
            employeeName = e.Name,
            updatedAt = e.UpdatedAt
        }));
    }

    // ── DELETE ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Remove an employee's registered face.
    /// </summary>
    [HttpDelete("employees/{employeeId:guid}")]
    public async Task<IActionResult> Remove(Guid employeeId, CancellationToken ct)
    {
        await _svc.RemoveAsync(employeeId, ct);
        return NoContent();
    }
}