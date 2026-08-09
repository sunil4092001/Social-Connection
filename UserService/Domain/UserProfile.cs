namespace UserService.Domain;

public sealed class UserProfile
{

    public UserProfile()
    {
        Bio = "Welcome to my profile.";
        Headline = "Open to opportunities";
        Location = "Not specified";
        Headline = "Headline";
        About = "About you";
        Company = "Company";
        ProfileImageUrl = "Profile image";
        CoverImageUrl = "Cover Image";
        Website = "Website";
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Headline { get; set; }
    public string? About { get; set; }
    public string? Location { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    // new
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string? PhoneNumber { get; set; }

    //
    public string Bio { get; set; }
    public string Company { get; set; } 
    public string ProfileImageUrl { get; set; }
    public string CoverImageUrl { get; set; }
    public string Website { get; set; }
    public DateTime UpdatedAt {  get; set; }
}



//UserProfiles
//------------------------
//UserId(Guid)
//FirstName
//LastName
//Headline
//Bio
//Location
//Company
//ProfileImageUrl
//CoverImageUrl
//Website
//CreatedAt
//UpdatedAt




//UserProfiles

//Education

//Experience

//Skills

//Certificates