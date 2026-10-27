namespace UserService.Domain;

public sealed class ProfessionalSkill
{
    public Guid ProfessionalId { get; set; }
    public Guid SkillId { get; set; }
    public int ExperienceYears { get; set; }
    public SkillLevel SkillLevel { get; set; }
    public UserProfile Professional { get; set; } = null!;
    public Skill Skill { get; set; } = null!;
}
