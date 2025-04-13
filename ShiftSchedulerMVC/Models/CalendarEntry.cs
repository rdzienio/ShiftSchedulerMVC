namespace ShiftSchedulerMVC.Models
{
    public class CalendarEntry
    {
        public DateTime Date { get; set; }
        public ShiftType? Shift { get; set; }
    }

}
