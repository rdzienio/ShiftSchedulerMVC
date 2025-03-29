namespace ShiftSchedulerMVC.Models
{
    public class Employee
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int WeeklyHourLimit { get; set; } = 40;
    }
}
