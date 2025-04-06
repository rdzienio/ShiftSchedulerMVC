using ShiftSchedulerMVC.Models;
using System.Diagnostics;

namespace ShiftSchedulerMVC.Services
{
    public static class GeneticScheduler
    {
        private static int workingHours = 176;
        public static Chromosome Run(
    List<Employee> employees,
    List<DateTime> dates,
    Dictionary<DateTime, Dictionary<ShiftType, int>> shiftRequirements,
    int workingHoursReq)
        {
            int generations = 150;
            int populationSize = 600;
            double crossoverRate = 0.9;
            double mutationRate = 0.5;
            workingHours = workingHoursReq;

            var rng = new Random();
            var stopwatch = Stopwatch.StartNew();
            var logPath = Logger.CreateLogFile();

            var population = SmartInitialPopulation(populationSize, dates, employees, shiftRequirements, rng);

            Chromosome bestChromosome = null;
            double bestFitness = double.MinValue;

            Logger.Append(logPath, $"workingHours = {workingHours};\ngenerations = {generations};\npopulationSize = {populationSize};\ncrossoverRate = {crossoverRate};\nmutationRate = {mutationRate};");

            for (int gen = 0; gen < generations; gen++)
            {
                population = population.OrderByDescending(c => EvaluateFitness(c, shiftRequirements)).ToList();
                var currentBest = population[0];
                double currentFitness = EvaluateFitness(currentBest, shiftRequirements);

                Logger.Append(logPath, $"Generation {gen + 1}: Best Fitness = {currentFitness:F5}");

                if (currentFitness > bestFitness)
                {
                    bestFitness = currentFitness;
                    bestChromosome = CloneChromosome(currentBest);
                }

                var nextGeneration = new List<Chromosome> { currentBest };

                while (nextGeneration.Count < populationSize)
                {
                    var parent1 = TournamentSelection(population, shiftRequirements, 5, rng);
                    var parent2 = TournamentSelection(population, shiftRequirements, 5, rng);

                    Chromosome child = rng.NextDouble() < crossoverRate
                        ? Crossover(parent1, parent2)
                        : CloneChromosome(parent1);

                    if (rng.NextDouble() < mutationRate)
                        Mutate(child, employees, rng);

                    Repair(child, shiftRequirements, employees);
                    nextGeneration.Add(child);
                }

                population = nextGeneration;
            }

            stopwatch.Stop();
            Logger.Append(logPath, $"Total runtime: {stopwatch.Elapsed.TotalSeconds:F2} seconds");

            // 📝 Dodatkowy log tylko dla najlepszego chromosomu
            var errorLog = EvaluateFitnessWithErrors(bestChromosome, shiftRequirements, workingHours);
            Logger.Append(logPath, "--- Final Best Chromosome Diagnostics ---");
            foreach (var line in errorLog)
                Logger.Append(logPath, line);

            return bestChromosome;
        }


        private static double EvaluateFitness(Chromosome chrom, Dictionary<DateTime, Dictionary<ShiftType, int>> shiftRequirements)
        {
            double penalty = 0;
            var employeeHours = new Dictionary<int, int>();
            var dailyAssignments = new Dictionary<(int, DateTime), int>();
            var shiftHistory = new Dictionary<int, List<(DateTime, ShiftType)>>();

            var errorLog = new List<string>();

            foreach (var gene in chrom.Genes)
            {
                if (!shiftRequirements.TryGetValue(gene.Date.Date, out var dayMap) ||
                    !dayMap.TryGetValue(gene.Shift, out int required))
                    continue;

                int assigned = gene.AssignedEmployees.Count;

                if (assigned < required)
                {
                    penalty += (required - assigned) * 150;
                    errorLog.Add($"Za mało osób na zmianie {gene.Date:yyyy-MM-dd} [{gene.Shift}]");
                }

                if (assigned > required)
                {
                    penalty += (assigned - required) * 5;
                    errorLog.Add($"Za dużo osób na zmianie {gene.Date:yyyy-MM-dd} [{gene.Shift}]");
                }

                foreach (var emp in gene.AssignedEmployees)
                {
                    if (!employeeHours.ContainsKey(emp.Id)) employeeHours[emp.Id] = 0;
                    employeeHours[emp.Id] += 8;

                    var key = (emp.Id, gene.Date.Date);
                    if (!dailyAssignments.ContainsKey(key)) dailyAssignments[key] = 0;
                    dailyAssignments[key]++;

                    if (!shiftHistory.ContainsKey(emp.Id)) shiftHistory[emp.Id] = new();
                    shiftHistory[emp.Id].Add((gene.Date, gene.Shift));
                }
            }

            foreach (var kvp in employeeHours)
            {
                if (kvp.Value > workingHours)
                {
                    penalty += (kvp.Value - workingHours) * 2;
                    //errorLog.Add($"Pracownik {kvp.Key} przekroczył limit godzin ({kvp.Value}h)");
                }
            }

            foreach (var kvp in dailyAssignments)
            {
                if (kvp.Value > 1)
                {
                    penalty += (kvp.Value - 1) * 15;
                    //errorLog.Add($"Pracownik {kvp.Key.Item1} ma więcej niż jedną zmianę w dniu {kvp.Key.Item2:yyyy-MM-dd}");
                }
            }

            foreach (var kvp in shiftHistory)
            {
                var shifts = kvp.Value.OrderBy(s => s.Item1).ToList();
                for (int i = 1; i < shifts.Count; i++)
                {
                    var prevEnd = GetShiftEnd(shifts[i - 1].Item1, shifts[i - 1].Item2);
                    var currStart = GetShiftStart(shifts[i].Item1, shifts[i].Item2);
                    var rest = (currStart - prevEnd).TotalHours;
                    if (rest < 12)
                    {
                        penalty += 20;
                        //errorLog.Add($"Pracownik {kvp.Key} miał zbyt krótką przerwę ({rest:0.##}h) między zmianami {shifts[i - 1].Item1:yyyy-MM-dd} [{shifts[i - 1].Item2}] i {shifts[i].Item1:yyyy-MM-dd} [{shifts[i].Item2}]");
                    }
                }
            }

            // Sprawdzenie tygodniowego odpoczynku (minimum 35h w tygodniu)
            foreach (var kvp in shiftHistory)
            {
                var shifts = kvp.Value.OrderBy(s => s.Item1).ToList();
                if (shifts.Count == 0) continue;

                var shiftPeriods = shifts.Select(s =>
                {
                    DateTime start = GetShiftStart(s.Item1, s.Item2);
                    DateTime end = GetShiftEnd(s.Item1, s.Item2);
                    return (Start: start, End: end);
                }).OrderBy(p => p.Start).ToList();

                DateTime weekStart = shiftPeriods.First().Start.Date;
                DateTime weekEnd = weekStart.AddDays(7);

                while (weekStart <= shiftPeriods.Last().End)
                {
                    double maxBreak = 0;
                    for (int i = 1; i < shiftPeriods.Count; i++)
                    {
                        if (shiftPeriods[i].Start < weekStart || shiftPeriods[i].Start > weekEnd) continue;

                        var previous = shiftPeriods[i - 1];
                        var current = shiftPeriods[i];

                        double breakHours = (current.Start - previous.End).TotalHours;
                        if (breakHours > maxBreak)
                            maxBreak = breakHours;
                    }

                    if (maxBreak < 35)
                    {
                        penalty += 100;
                        //errorLog.Add($"Pracownik {kvp.Key} nie miał 35h przerwy tygodniowej w tygodniu zaczynającym {weekStart:yyyy-MM-dd}");
                    }

                    weekStart = weekStart.AddDays(7);
                    weekEnd = weekStart.AddDays(7);
                }
            }

            double fitness = 1.0 / (1.0 + penalty);

            //* 🔍 Diagnoza do konsoli
            /*Console.WriteLine("\n--- Ostatnia diagnoza harmonogramu ---");
            foreach (var line in errorLog.Distinct())
                Console.WriteLine("!!!  " + line);

            Console.WriteLine($"Fitness końcowy: {fitness:0.#####}");
            Console.WriteLine();*/

            return fitness;
        }


        private static void Mutate(Chromosome chrom, List<Employee> employees, Random rng)
        {
            int geneIndex = rng.Next(chrom.Genes.Count);
            var gene = chrom.Genes[geneIndex];

            if (gene.AssignedEmployees.Count > 0)
            {
                if (rng.NextDouble() < 0.5)
                {
                    int count = gene.AssignedEmployees.Count;
                    gene.AssignedEmployees.Clear();
                    for (int i = 0; i < count; i++)
                    {
                        var newEmp = employees[rng.Next(employees.Count)];
                        gene.AssignedEmployees.Add(newEmp);
                    }
                }
                else
                {
                    int index = rng.Next(gene.AssignedEmployees.Count);
                    gene.AssignedEmployees[index] = employees[rng.Next(employees.Count)];
                }
            }
        }

        private static Chromosome Crossover(Chromosome p1, Chromosome p2)
        {
            var child = new Chromosome();
            int split = p1.Genes.Count / 2;

            for (int i = 0; i < p1.Genes.Count; i++)
            {
                var gene = i < split ? p1.Genes[i] : p2.Genes[i];
                child.Genes.Add(new Gene
                {
                    Date = gene.Date,
                    Shift = gene.Shift,
                    AssignedEmployees = new List<Employee>(gene.AssignedEmployees)
                });
            }

            return child;
        }

        private static Chromosome TournamentSelection(List<Chromosome> population, Dictionary<DateTime, Dictionary<ShiftType, int>> requirements, int size, Random rng)
        {
            var candidates = new List<Chromosome>();
            for (int i = 0; i < size; i++)
                candidates.Add(population[rng.Next(population.Count)]);

            return candidates.OrderByDescending(c => EvaluateFitness(c, requirements)).First();
        }

        private static Chromosome CloneChromosome(Chromosome source)
        {
            return new Chromosome
            {
                Genes = source.Genes.Select(g => new Gene
                {
                    Date = g.Date,
                    Shift = g.Shift,
                    AssignedEmployees = new List<Employee>(g.AssignedEmployees)
                }).ToList()
            };
        }

        private static void Repair(Chromosome chrom, Dictionary<DateTime, Dictionary<ShiftType, int>> requirements, List<Employee> employees)
        {
            foreach (var gene in chrom.Genes)
            {
                if (!requirements.TryGetValue(gene.Date.Date, out var map) ||
                    !map.TryGetValue(gene.Shift, out int required))
                    continue;

                while (gene.AssignedEmployees.Count < required)
                {
                    var emp = employees[new Random().Next(employees.Count)];
                    gene.AssignedEmployees.Add(emp);
                }

                if (gene.AssignedEmployees.Count > required)
                    gene.AssignedEmployees = gene.AssignedEmployees.Take(required).ToList();
            }
        }

        private static DateTime GetShiftStart(DateTime date, ShiftType shift)
        {
            return shift switch
            {
                ShiftType.Morning => date.Date.AddHours(6),
                ShiftType.Afternoon => date.Date.AddHours(14),
                ShiftType.Night => date.Date.AddHours(22),
                _ => date
            };
        }

        private static DateTime GetShiftEnd(DateTime date, ShiftType shift)
        {
            return shift switch
            {
                ShiftType.Morning => date.Date.AddHours(14),
                ShiftType.Afternoon => date.Date.AddHours(22),
                ShiftType.Night => date.Date.AddDays(1).AddHours(6),
                _ => date
            };
        }

        private static List<Chromosome> SmartInitialPopulation(int populationSize, List<DateTime> dates, List<Employee> employees, Dictionary<DateTime, Dictionary<ShiftType, int>> shiftRequirements, Random rng)
        {
            var population = new List<Chromosome>();

            for (int p = 0; p < populationSize; p++)
            {
                var chrom = new Chromosome();
                foreach (var date in dates)
                {
                    foreach (ShiftType shift in Enum.GetValues(typeof(ShiftType)))
                    {
                        var gene = new Gene { Date = date, Shift = shift };
                        if (shiftRequirements.TryGetValue(date, out var dayReqs) &&
                            dayReqs.TryGetValue(shift, out var requiredCount))
                        {
                            gene.AssignedEmployees = employees.OrderBy(_ => rng.Next()).Take(requiredCount).ToList();
                        }

                        chrom.Genes.Add(gene);
                    }
                }

                population.Add(chrom);
            }

            return population;
        }

        private static List<string> EvaluateFitnessWithErrors(Chromosome chrom, Dictionary<DateTime, Dictionary<ShiftType, int>> shiftRequirements, int workingHours)
        {
            double penalty = 0;
            var employeeHours = new Dictionary<int, int>();
            var dailyAssignments = new Dictionary<(int, DateTime), int>();
            var shiftHistory = new Dictionary<int, List<(DateTime, ShiftType)>>();

            var log = new List<string>();

            foreach (var gene in chrom.Genes)
            {
                if (!shiftRequirements.TryGetValue(gene.Date.Date, out var dayMap) ||
                    !dayMap.TryGetValue(gene.Shift, out int required))
                    continue;

                int assigned = gene.AssignedEmployees.Count;

                if (assigned < required)
                {
                    penalty += (required - assigned) * 150;
                    log.Add($"Za mało osób na zmianie {gene.Date:yyyy-MM-dd} [{gene.Shift}]");
                }

                if (assigned > required)
                {
                    penalty += (assigned - required) * 5;
                    log.Add($"Za dużo osób na zmianie {gene.Date:yyyy-MM-dd} [{gene.Shift}]");
                }

                foreach (var emp in gene.AssignedEmployees)
                {
                    if (!employeeHours.ContainsKey(emp.Id)) employeeHours[emp.Id] = 0;
                    employeeHours[emp.Id] += 8;

                    var key = (emp.Id, gene.Date.Date);
                    if (!dailyAssignments.ContainsKey(key)) dailyAssignments[key] = 0;
                    dailyAssignments[key]++;

                    if (!shiftHistory.ContainsKey(emp.Id)) shiftHistory[emp.Id] = new();
                    shiftHistory[emp.Id].Add((gene.Date, gene.Shift));
                }
            }

            foreach (var kvp in employeeHours)
                if (kvp.Value > workingHours)
                {
                    penalty += (kvp.Value - workingHours) * 2;
                    log.Add($"Pracownik {kvp.Key} przekroczył limit godzin ({kvp.Value}h)");
                }

            foreach (var kvp in dailyAssignments)
                if (kvp.Value > 1)
                {
                    penalty += (kvp.Value - 1) * 15;
                    log.Add($"Pracownik {kvp.Key.Item1} ma więcej niż jedną zmianę w dniu {kvp.Key.Item2:yyyy-MM-dd}");
                }

            foreach (var kvp in shiftHistory)
            {
                var shifts = kvp.Value.OrderBy(s => s.Item1).ToList();
                for (int i = 1; i < shifts.Count; i++)
                {
                    var prevEnd = GetShiftEnd(shifts[i - 1].Item1, shifts[i - 1].Item2);
                    var currStart = GetShiftStart(shifts[i].Item1, shifts[i].Item2);
                    var rest = (currStart - prevEnd).TotalHours;
                    if (rest < 12)
                    {
                        penalty += 20;
                        log.Add($"Pracownik {kvp.Key} miał za krótką przerwę ({rest:0.##}h) między zmianami");
                    }
                }

                var shiftPeriods = shifts.Select(s =>
                {
                    DateTime start = GetShiftStart(s.Item1, s.Item2);
                    DateTime end = GetShiftEnd(s.Item1, s.Item2);
                    return (Start: start, End: end);
                }).OrderBy(p => p.Start).ToList();

                DateTime weekStart = shiftPeriods.First().Start.Date;
                DateTime weekEnd = weekStart.AddDays(7);

                while (weekStart <= shiftPeriods.Last().End)
                {
                    double maxBreak = 0;
                    for (int i = 1; i < shiftPeriods.Count; i++)
                    {
                        if (shiftPeriods[i].Start < weekStart || shiftPeriods[i].Start > weekEnd) continue;

                        var previous = shiftPeriods[i - 1];
                        var current = shiftPeriods[i];

                        double breakHours = (current.Start - previous.End).TotalHours;
                        if (breakHours > maxBreak)
                            maxBreak = breakHours;
                    }

                    if (maxBreak < 35)
                    {
                        penalty += 100;
                        log.Add($"Pracownik {kvp.Key} nie miał 35h odpoczynku tygodniowego w tygodniu od {weekStart:yyyy-MM-dd}");
                    }

                    weekStart = weekStart.AddDays(7);
                    weekEnd = weekStart.AddDays(7);
                }
            }

            log.Insert(0, $"Fitness końcowy: {(1.0 / (1.0 + penalty)):0.#####}");
            log.Insert(1, "--- Diagnoza ---");

            return log;
        }

    }
}