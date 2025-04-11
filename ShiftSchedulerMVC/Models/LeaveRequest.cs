namespace ShiftSchedulerMVC.Models
{
    public class LeaveRequest
    {
        public int Id { get; set; }
        public string EmployeeId { get; set; }
        public ApplicationUser Employee { get; set; }

        public DateTime Date { get; set; }
        public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

        public DateTime SubmittedAt { get; set; } = DateTime.Now;

        public string? Reason { get; set; }
    }

    public enum LeaveStatus
    {
        Pending,
        Approved,
        Rejected
    }

}
