using BuildingBlocks.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RequirementService.Application;
using RequirementService.Domain;

namespace RequirementService.Infrastructure;

public sealed class EfRequirementRepository(RequirementDbContext db) : IRequirementRepository
{
    public void Add(Requirement requirement) => db.Requirements.Add(requirement);

    public Task<Requirement?> GetAsync(Guid id, CancellationToken ct) =>
        db.Requirements
            .Include(x => x.RequiredSkills)
            .Include(x => x.Responses)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<Requirement>> SearchAsync(Guid? customerId, string? professionalType, string? location, RequirementStatus? status, CancellationToken ct)
    {
        IQueryable<Requirement> query = db.Requirements.AsNoTracking().Include(x => x.RequiredSkills).Include(x => x.Responses).AsSplitQuery();
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(professionalType)) query = query.Where(x => x.ProfessionalType == professionalType.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(location)) query = query.Where(x => x.Location.ToLower() == location.Trim().ToLower());
        if (status.HasValue) query = query.Where(x => x.Status == status);
        return await query.OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Requirement>> SearchOpenByProfessionalTypeAsync(string professionalType, Guid excludeRespondedBy, CancellationToken ct) =>
        await db.Requirements.AsNoTracking()
            .Where(requirement => requirement.Status == RequirementStatus.OPEN
                && requirement.ProfessionalType == professionalType.Trim().ToUpperInvariant()
                && requirement.CustomerId != excludeRespondedBy
                && !requirement.Responses.Any(response => response.ProfessionalId == excludeRespondedBy))
            .Include(requirement => requirement.RequiredSkills)
            .AsSplitQuery()
            .OrderByDescending(requirement => requirement.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Requirement>> SearchByProfessionalIdAsync(Guid professionalId, CancellationToken ct) =>
        await db.Requirements.AsNoTracking()
            .Where(requirement => requirement.Responses.Any(response => response.ProfessionalId == professionalId))
            .Include(requirement => requirement.RequiredSkills)
            .Include(requirement => requirement.Responses)
            .AsSplitQuery()
            .OrderByDescending(requirement => requirement.CreatedAtUtc)
            .ToListAsync(ct);

    /// <summary>Commits the business change and any staged outbox events in one transaction.</summary>
    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("This requirement was changed by another request. Please refresh and try again.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("You have already responded to this requirement.");
        }
    }
}
