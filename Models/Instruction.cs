

public class Instruction
{

    public string? Id { get; set; }
    public required string Name { get; set; }
    public string? Version { get; set; }
    public required string Content { get; set; }
    public string? Type { get; set; }
    public DateTime CreatedAt { get; set; }
}
