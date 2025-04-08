namespace ShiftSchedulerMVC.Models
{
    public class UserViewModel
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string FullName => $"{FirstName} {LastName}";
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Position { get; set; }
        public List<string> Roles { get; set; }
    }

}
