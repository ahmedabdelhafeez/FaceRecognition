namespace FaceRecognition.Entities;

public sealed class EmployeeFaceEmbedding
{
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; set; } = string.Empty;
    public byte[] EmbeddingBytes { get; set; } = Array.Empty<byte>();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public float[] GetEmbedding()
    {
        var floats = new float[EmbeddingBytes.Length / sizeof(float)];
        Buffer.BlockCopy(EmbeddingBytes, 0, floats, 0, EmbeddingBytes.Length);
        return floats;
    }

    public static byte[] ToBytes(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}