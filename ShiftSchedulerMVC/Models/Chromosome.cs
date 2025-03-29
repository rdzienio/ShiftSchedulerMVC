using System;
using System.Collections.Generic;

namespace ShiftSchedulerMVC.Models
{
    public class Chromosome
    {
        public List<Gene> Genes { get; set; } = new();
    }
}
