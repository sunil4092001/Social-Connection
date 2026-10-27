> **Update:** the IdentityService → UserService handoff endpoint is now `POST /internal/users/profiles` (outside `/api`, not reachable through the gateway), and the internal key is stored in user-secrets / `.env`. See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

# BuildConnect — Phase 1: User Profiles and Skills

## Architecture and ownership

IdentityService continues to own email, password hashes, login, refresh tokens, logout, and JWTs. It accepts the registration profile fields only to forward them to UserService; none are stored in IdentityDb.

UserService owns the profile, user type, profession, experience, location, availability, verification flag, and skills. The Identity-to-User handoff now uses a dedicated internal endpoint protected by `X-Internal-Service-Key`; public callers cannot submit an arbitrary `UserId` to create a profile.

## Database changes

Migration `AddBuildConnectProfileAndSkills` adds these UserDb changes:

- `UserProfiles.UserType`, defaulting existing profiles to `CUSTOMER`
- `ProfessionalType`, `ExperienceYears`, `AvailabilityStatus`, and `IsVerified`
- `Skills` reference table
- `ProfessionalSkills` many-to-many table keyed by `(ProfessionalId, SkillId)`

It preserves the pre-existing `Bio` column and seeds 12 starter skills.

Apply the migration after setting the real connection string:

```powershell
dotnet ef database update --project UserService\UserService.csproj --startup-project UserService\UserService.csproj
```

## APIs

- `POST /api/auth/register` now accepts `userType`, and professional registration also requires `professionalType` and `experienceYears`.
- `GET /api/users/me` and `GET /api/users/{userId}` return the typed profile and its skills.
- `PUT /api/users/me` updates the current authenticated user's profile. It never accepts a user id.
- `GET /api/skills` returns the catalogue.
- `POST /api/users/{userId}/skills` and `DELETE /api/users/{userId}/skills/{skillId}` require the authenticated user to match `{userId}` and require a professional profile.

Example professional registration:

```json
{
  "email": "amit@example.com",
  "password": "SecurePass123",
  "firstName": "Amit",
  "lastName": "Kumar",
  "userType": "PROFESSIONAL",
  "professionalType": "MASON",
  "experienceYears": 8,
  "location": "Gurgaon"
}
```

Example skill assignment (with a JWT for the same user):

```json
{
  "skillId": "1fbe1c6f-93f3-4b95-9fb1-fccf12a6a001",
  "experienceYears": 8,
  "skillLevel": "EXPERT"
}
```

## Validation and security

- `CUSTOMER` and `PROFESSIONAL` are the permitted user types.
- Professionals must supply a profession and non-negative experience during registration.
- Only professional users can assign skills.
- User IDs for self-service updates and skills are taken from the JWT and checked against the route value.
