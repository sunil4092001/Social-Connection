using Microsoft.AspNetCore.Http.HttpResults;

namespace IdentityService.Entities
{
    public class User
    {
        public Guid Id { get; set; }

        public string Email { get; set; }

        public string PasswordHash { get; set; }

        //public string FirstName { get; set; }

        //public string LastName { get; set; }
        //public string? PhoneNumber { get; set; }


        public bool IsEmailVerified { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }





        //        Users
        //------------------------
        //Id(Guid)
        //Email
        //PasswordHash
        //IsEmailVerified
        //IsActive
        //CreatedAt
        //UpdatedAt
    }

}
