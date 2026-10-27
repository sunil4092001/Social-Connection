namespace UserService.Domain;

public sealed class Skill
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<ProfessionalSkill> ProfessionalSkills { get; set; } = new List<ProfessionalSkill>();
}
