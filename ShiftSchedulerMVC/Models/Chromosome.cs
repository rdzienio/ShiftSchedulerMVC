using System;
using System.Collections.Generic;

namespace ShiftSchedulerMVC.Models
{
    public class Chromosome
    {
        public List<Gene> Genes { get; set; } = new();

        // 🚀 Cache fitness: liczenie fitness jest kosztowne i w trakcie jednej generacji
        // ten sam chromosom jest oceniany wielokrotnie (sortowanie + selekcja turniejowa).
        // Wartość jest liczona raz i przechowywana tutaj; null = "trzeba przeliczyć"
        // (po każdej zmianie genów przez Mutate/Crossover/Repair). Nie jest serializowane,
        // bo zależy od kontekstu (wymagania/godziny/urlopy) danego uruchomienia GA.
        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public double? CachedFitness { get; set; }
    }
}
