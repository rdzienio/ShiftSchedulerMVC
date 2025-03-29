using System;
using System.Collections.Generic;

namespace ShiftSchedulerMVC.Models
{
    public class Gene
    {
        public DateTime Date { get; set; }
        public ShiftType Shift { get; set; }
        public List<Employee> AssignedEmployees { get; set; } = new();
    }
}
