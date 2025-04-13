using ShiftSchedulerMVC.Models;
using System.Diagnostics;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ShiftSchedulerMVC.Services
{
    public static class GeneticScheduler
    {
        private static int workingHours = 176;
        private static int countMutate = 0;
        private static int countCrossover = 0;
        public static Chromosome Run(
        List<Employee> employees,
        List<DateTime> dates,
        Dictionary<DateTime, Dictionary<ShiftType, int>> shiftRequirements,
        int workingHoursReq,
        HashSet<(string empId, DateTime date)> vacationDays)
        {
            int generations = 100;
            int populationSize = 500;
            double crossoverRate = 0.1;
            double mutationRate = 0.09;
            workingHours = workingHoursReq;

            var rng = new Random();


            var stopwatch = Stopwatch.StartNew();
            var logPath = Logger.CreateLogFile();

            var population = SmartInitialPopulation(populationSize, dates, employees, shiftRequirements, vacationDays, rng);

            Chromosome bestChromosome = null;
            double bestFitness = double.MinValue;

            Logger.Append(logPath, $"workingHours = {workingHours};\ngenerations = {generations};\npopulationSize = {populationSize};\ncrossoverRate = {crossoverRate};\nmutationRate = {mutationRate};");

            for (int gen = 0; gen < generations; gen++)
            {
                population = population.OrderByDescending(c => EvaluateFitness(c, shiftRequirements, workingHours, vacationDays)).ToList();
                var currentBest = population[0];
                double currentFitness = EvaluateFitness(currentBest, shiftRequirements, workingHours, vacationDays);

                Logger.Append(logPath, $"Generation {gen + 1}: Best Fitness = {currentFitness:F5}");

                if (currentFitness > bestFitness)
                {
                    bestFitness = currentFitness;
                    bestChromosome = CloneChromosome(currentBest);
                }

                var nextGeneration = new List<Chromosome> { currentBest };

                while (nextGeneration.Count < populationSize)
                {
                    var parent1 = TournamentSelection(population, shiftRequirements, 5, rng, workingHours, vacationDays);
                    var parent2 = TournamentSelection(population, shiftRequirements, 5, rng, workingHours, vacationDays);

                    Chromosome child = rng.NextDouble() < crossoverRate
                        ? Crossover(parent1, parent2)
                        : CloneChromosome(parent1);

                    if (rng.NextDouble() < mutationRate)
                        Mutate(child, employees, rng);

                    Repair(child, shiftRequirements, employees, vacationDays);
                    nextGeneration.Add(child);
                }

                population = nextGeneration;
            }

            stopwatch.Stop();
            Logger.Append(logPath, $"Total runtime: {stopwatch.Elapsed.TotalSeconds:F2} seconds");

            // 📝 Dodatkowy log tylko dla najlepszego chromosomu
            var errorLog = EvaluateFitnessWithErrors(bestChromosome, shiftRequirements, workingHours, vacationDays);
            Logger.Append(logPath, "--- Final Best Chromosome Diagnostics ---");
            foreach (var line in errorLog)
                Logger.Append(logPath, line);
            Logger.Append(logPath, $"Best Chromosome: {EvaluateFitness(bestChromosome, shiftRequirements, workingHours, vacationDays)}");
            Console.WriteLine($"Best Chromosome: {EvaluateFitness(bestChromosome, shiftRequirements, workingHours, vacationDays)}");
            return bestChromosome;
        }


        private static double EvaluateFitness(
    Chromosome chrom,
    Dictionary<DateTime, Dictionary<ShiftType, int>> shiftRequirements,
    int workingHours,
    HashSet<(string empId, DateTime date)> vacationDays)
        {
            double penalty = 0;
            double reward = 0;

            var employeeHours = new Dictionary<string, int>();
            var dailyAssignments = new Dictionary<(string, DateTime), int>();
            var shiftHistory = new Dictionary<string, List<(DateTime, ShiftType)>>();

            foreach (var gene in chrom.Genes)
            {
                if (!shiftRequirements.TryGetValue(gene.Date.Date, out var dayMap) ||
                    !dayMap.TryGetValue(gene.Shift, out int required))
                    continue;

                int assigned = gene.AssignedEmployees.Count;

                if (assigned < required)
                {
                    penalty += (required - assigned) * 150;
                }

                /*if (assigned > required)
                {
                    penalty += (assigned - required) * 5;
                }*/

                foreach (var emp in gene.AssignedEmployees)
                {
                    if (!employeeHours.ContainsKey(emp.Id)) employeeHours[emp.Id] = 0;
                    employeeHours[emp.Id] += 8;

                    

                    var key = (emp.Id, gene.Date.Date);
                    if (!dailyAssignments.ContainsKey(key)) dailyAssignments[key] = 0;
                    dailyAssignments[key]++;

                    if (!shiftHistory.ContainsKey(emp.Id)) shiftHistory[emp.Id] = new();
                    shiftHistory[emp.Id].Add((gene.Date, gene.Shift));

                    if (vacationDays.Contains((emp.Id, gene.Date.Date)))
                    {
                        penalty += 500;
                    }
                }
            }

            foreach (var (empId, date) in vacationDays)
            {
                if (!employeeHours.ContainsKey(empId))
                    employeeHours[empId] = 0;

                employeeHours[empId] += 8;
            }

            foreach (var kvp in employeeHours)
            {
                if (kvp.Value == workingHours) continue;
                if (kvp.Value > workingHours)
                {
                    penalty += (kvp.Value - workingHours) * 2;
                }
                else
                {
                    double deficit = workingHours - kvp.Value;
                    penalty += deficit * 0.5;
                }
            }

            foreach (var kvp in dailyAssignments)
            {
                if (kvp.Value > 1)
                {
                    penalty += (kvp.Value - 1) * 15;
                }
            }

            foreach (var kvp in shiftHistory)
            {
                var shifts = kvp.Value.OrderBy(s => s.Item1).ToList();
                if (shifts.Count == 0) continue;

                double consecutiveHours = 0;
                DateTime? lastEnd = null;

                for (int i = 0; i < shifts.Count; i++)
                {
                    DateTime shiftStart = GetShiftStart(shifts[i].Item1, shifts[i].Item2);
                    DateTime shiftEnd = GetShiftEnd(shifts[i].Item1, shifts[i].Item2);

                    if (lastEnd == null || (shiftStart - lastEnd.Value).TotalHours <= 12)
                    {
                        consecutiveHours += (shiftEnd - shiftStart).TotalHours;
                    }
                    else
                    {
                        consecutiveHours = (shiftEnd - shiftStart).TotalHours;
                    }

                    lastEnd = shiftEnd;

                    if (consecutiveHours > 40)
                    {
                        penalty += (consecutiveHours - 40) * 10;
                    }
                }

                for (int i = 1; i < shifts.Count; i++)
                {
                    var prevEnd = GetShiftEnd(shifts[i - 1].Item1, shifts[i - 1].Item2);
                    var currStart = GetShiftStart(shifts[i].Item1, shifts[i].Item2);
                    var rest = (currStart - prevEnd).TotalHours;
                    if (rest < 12)
                    {
                        penalty += 20;
                    }
                }

                // --- Maksymalnie 5 dni roboczych pod rząd ---
                var shiftDays = kvp.Value
                    .Select(s => s.Item1.Date)
                    .Distinct()
                    .OrderBy(d => d)
                    .ToList();

                int consecutiveWorkDays = 1;

                for (int i = 1; i < shiftDays.Count; i++)
                {
                    if ((shiftDays[i] - shiftDays[i - 1]).TotalDays == 1)
                    {
                        consecutiveWorkDays++;
                        if (consecutiveWorkDays > 5)
                        {
                            penalty += 100;
                            break; // Możesz tu zrobić `break` lub kontynuować dalej
                        }
                    }
                    else
                    {
                        consecutiveWorkDays = 1;
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
                    if (weekEnd > shiftPeriods.Last().End) break;
                    var shiftsThisWeek = shiftPeriods
                        .Where(p => p.Start >= weekStart && p.Start <= weekEnd)
                        .ToList();

                    if (shiftsThisWeek.Count >= 2)
                    {
                        double maxBreak = 0;
                        for (int i = 1; i < shiftsThisWeek.Count; i++)
                        {
                            var previous = shiftsThisWeek[i - 1];
                            var current = shiftsThisWeek[i];

                            double breakHours = (current.Start - previous.End).TotalHours;
                            if (breakHours > maxBreak)
                                maxBreak = breakHours;
                        }

                        if (maxBreak < 35)
                        {
                            penalty += 100;
                        }
                    }


                    weekStart = weekStart.AddDays(7);
                    weekEnd = weekStart.AddDays(7);
                }
            }

            double fitness = (1.0 + reward) / (1.0 + penalty);
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
            countMutate++;
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
            countCrossover++;
            return child;
        }

        private static Chromosome TournamentSelection(
            List<Chromosome> population,
            Dictionary<DateTime, Dictionary<ShiftType, int>> requirements,
            int size,
            Random rng,
            int workingHours,
            HashSet<(string empId, DateTime date)> vacationDays)
        {
            var candidates = new List<Chromosome>();
            for (int i = 0; i < size; i++)
                candidates.Add(population[rng.Next(population.Count)]);

            return candidates
                .OrderByDescending(c => EvaluateFitness(c, requirements, workingHours, vacationDays))
                .First();
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

        private static void Repair(
    Chromosome chrom,
    Dictionary<DateTime, Dictionary<ShiftType, int>> shiftRequirements,
    List<Employee> employees,
    HashSet<(string empId, DateTime date)> vacationDays)
        {
            var rng = new Random();
            var employeeHours = employees.ToDictionary(e => e.Id, _ => 0);
            var dailyAssignments = new HashSet<(string EmpId, DateTime Date)>();
            var lastShiftMap = new Dictionary<string, (DateTime date, ShiftType shift)>();
            int repRequired = 0;
            int repExtra = 0;

            // Urlopy
            foreach (var (empId, date) in vacationDays)
                if (employeeHours.ContainsKey(empId))
                    employeeHours[empId] += 8;

            // Inicjalizacja stanu z chromosomu
            foreach (var gene in chrom.Genes)
            {
                foreach (var emp in gene.AssignedEmployees.ToList())
                {
                    var date = gene.Date.Date;
                    var key = (emp.Id, date);

                    //if(vacationDays.Contains(key)) employeeHours[emp.Id] += 8;
                

                    // 🧹 Sprawdzenie błędnych przypisań
                    if (dailyAssignments.Contains(key) || vacationDays.Contains(key))
                    {
                        gene.AssignedEmployees.Remove(emp);
                        continue;
                    }

                    // 🧪 Sprawdzenie zgodności z poprzednią zmianą
                    if (!IsEligibleAfterPreviousShift(emp.Id, gene.Date, gene.Shift, lastShiftMap))
                    {
                        gene.AssignedEmployees.Remove(emp);
                        continue;
                    }

                    employeeHours[emp.Id] += 8;
                    dailyAssignments.Add(key);
                    lastShiftMap[emp.Id] = (gene.Date, gene.Shift);
                }
            }

            // Uzupełnianie niedoborów
            foreach (var gene in chrom.Genes)
            {
                var date = gene.Date.Date;

                if (!shiftRequirements.TryGetValue(date, out var dayMap) ||
                    !dayMap.TryGetValue(gene.Shift, out int required))
                    continue;

                if (required == 0)
                {
                    gene.AssignedEmployees.Clear();
                    continue;
                }

                while (gene.AssignedEmployees.Count < required)
                {
                    var available = employees
                        .Where(e =>
                            !vacationDays.Contains((e.Id, date)) &&
                            !dailyAssignments.Contains((e.Id, date)) &&
                            employeeHours[e.Id] + 8 <= workingHours &&
                            IsEligibleAfterPreviousShift(e.Id, gene.Date, gene.Shift, lastShiftMap))
                        .OrderBy(_ => rng.Next())
                        .ToList();

                    if (!available.Any()) break;

                    repRequired++;

                    var emp = available.First();
                    gene.AssignedEmployees.Add(emp);
                    employeeHours[emp.Id] += 8;
                    dailyAssignments.Add((emp.Id, date));
                    lastShiftMap[emp.Id] = (gene.Date, gene.Shift);
                }

                // Opcjonalnie nadmiarowy
                if (gene.AssignedEmployees.Count == required)
                {
                    // Tylko jeśli wciąż mamy znaczną liczbę dostępnych ludzi
                    var potentialExtras = employees
                        .Where(e =>
                            !vacationDays.Contains((e.Id, date)) &&
                            !dailyAssignments.Contains((e.Id, date)) &&
                            employeeHours[e.Id] + 8 <= workingHours &&
                            IsEligibleAfterPreviousShift(e.Id, gene.Date, gene.Shift, lastShiftMap))
                        .OrderBy(_ => rng.Next())
                        .ToList();

                    if (potentialExtras.Count > 1) // np. zostaw bufor dla innych zmian
                    {
                        var extra = potentialExtras.First();
                        gene.AssignedEmployees.Add(extra);
                        employeeHours[extra.Id] += 8;
                        dailyAssignments.Add((extra.Id, date));
                        lastShiftMap[extra.Id] = (gene.Date, gene.Shift);
                    }
                }

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

        private static List<Chromosome> SmartInitialPopulation(
    int populationSize,
    List<DateTime> dates,
    List<Employee> employees,
    Dictionary<DateTime, Dictionary<ShiftType, int>> shiftRequirements,
    HashSet<(string empId, DateTime date)> vacationDays,
    Random rng)
        {
            var population = new List<Chromosome>();
            var periodEnd = dates.Max();

            for (int p = 0; p < populationSize; p++)
            {
                var chrom = new Chromosome();
                var employeeHours = employees.ToDictionary(e => e.Id, e => 0);
                var dailyAssignments = new HashSet<(string EmpId, DateTime Date)>();
                var lastShiftMap = new Dictionary<string, (DateTime date, ShiftType shift)>();

                // Zlicz godziny z zatwierdzonych urlopów
                foreach (var (empId, date) in vacationDays)
                {
                    if (employeeHours.ContainsKey(empId))
                        employeeHours[empId] += 8;
                }

                foreach (var date in dates.OrderByDescending(d => GetDayWeight(d, periodEnd)))
                {
                    foreach (ShiftType shift in Enum.GetValues(typeof(ShiftType)))
                    {
                        var gene = new Gene { Date = date, Shift = shift };
                        chrom.Genes.Add(gene);

                        if (!shiftRequirements.TryGetValue(date, out var dayReqs) ||
                            !dayReqs.TryGetValue(shift, out int required))
                            continue;

                        int attempts = 0;
                        while (gene.AssignedEmployees.Count < required && attempts < 100)
                        {
                            var emp = employees[rng.Next(employees.Count)];
                            var key = (emp.Id, date.Date);

                            if (vacationDays.Contains((emp.Id, date.Date))) { attempts++; continue; }
                            if (dailyAssignments.Contains(key)) { attempts++; continue; }
                            if (employeeHours[emp.Id] + 8 > workingHours) { attempts++; continue; }
                            if (!IsEligibleAfterPreviousShift(emp.Id, date, shift, lastShiftMap)) { attempts++; continue; }

                            gene.AssignedEmployees.Add(emp);
                            employeeHours[emp.Id] += 8;
                            dailyAssignments.Add(key);
                            lastShiftMap[emp.Id] = (date, shift);
                            attempts++;
                        }

                        // Opcjonalnie dodanie 1 nadmiarowej osoby (jeśli jest zapas godzin)
                        if (gene.AssignedEmployees.Count == required)
                        {
                            // Tylko jeśli wciąż mamy znaczną liczbę dostępnych ludzi
                            var potentialExtras = employees
                                .Where(e =>
                                    !vacationDays.Contains((e.Id, date)) &&
                                    !dailyAssignments.Contains((e.Id, date)) &&
                                    employeeHours[e.Id] + 8 <= workingHours &&
                                    IsEligibleAfterPreviousShift(e.Id, gene.Date, gene.Shift, lastShiftMap))
                                .OrderBy(_ => rng.Next())
                                .ToList();

                            if (potentialExtras.Count > 0) // np. zostaw bufor dla innych zmian
                            {
                                var extra = potentialExtras.First();
                                gene.AssignedEmployees.Add(extra);
                                employeeHours[extra.Id] += 8;
                                dailyAssignments.Add((extra.Id, date));
                                lastShiftMap[extra.Id] = (date, shift);
                            }
                        }

                    }
                }

                population.Add(chrom);
            }

            return population;
        }




        private static List<string> EvaluateFitnessWithErrors(
        Chromosome chrom,
        Dictionary<DateTime, Dictionary<ShiftType, int>> shiftRequirements,
        int workingHours,
        HashSet<(string empId, DateTime date)> vacationDays)
        {
            double penalty = 0;
            double reward = 0;

            var employeeHours = new Dictionary<string, int>();
            var dailyAssignments = new Dictionary<(string, DateTime), int>();
            var shiftHistory = new Dictionary<string, List<(DateTime, ShiftType)>>();

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
                    log.Add($"KARA: Za mało osób na zmianie {gene.Date:yyyy-MM-dd} [{gene.Shift}]");
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

                    // ❌ Sprawdzenie przypisania pracownika do zmiany w dniu urlopu
                    if (vacationDays.Contains((emp.Id, gene.Date.Date)))
                    {
                        penalty += 500;
                        log.Add($"❌ BŁĄD: Pracownik {emp.Id} został przypisany do zmiany {gene.Shift} w dzień urlopu {gene.Date:yyyy-MM-dd}");
                    }
                }
            }

            foreach (var (empId, date) in vacationDays)
            {
                if (!employeeHours.ContainsKey(empId))
                    employeeHours[empId] = 0;

                employeeHours[empId] += 8;
            }

            foreach (var kvp in employeeHours)
            {
                if(kvp.Value == workingHours) continue;
                if (kvp.Value > workingHours)
                {
                    penalty += (kvp.Value - workingHours) * 2;
                    log.Add($"Pracownik {kvp.Key} przekroczył limit godzin ({kvp.Value}h)");
                }
                else
                {
                    double deficit = workingHours - kvp.Value;
                    penalty += deficit * 0.5;
                    log.Add($"Pracownik {kvp.Key} nie spełnił limitu godzin ({kvp.Value}h)");
                }
            }

            foreach (var kvp in dailyAssignments)
            {
                if (kvp.Value > 1)
                {
                    penalty += (kvp.Value - 1) * 15;
                    log.Add($"KARA: Pracownik {kvp.Key.Item1} ma więcej niż jedną zmianę w dniu {kvp.Key.Item2:yyyy-MM-dd}");
                }
            }

            foreach (var kvp in shiftHistory)
            {
                var shifts = kvp.Value.OrderBy(s => s.Item1).ToList();
                if (!shifts.Any()) continue;

                double consecutiveHours = 0;
                DateTime? lastEnd = null;   



                for (int i = 0; i < shifts.Count; i++)
                {
                    DateTime shiftStart = GetShiftStart(shifts[i].Item1, shifts[i].Item2);
                    DateTime shiftEnd = GetShiftEnd(shifts[i].Item1, shifts[i].Item2);

                    if (lastEnd == null || (shiftStart - lastEnd.Value).TotalHours <= 12)
                    {
                        consecutiveHours += (shiftEnd - shiftStart).TotalHours;
                    }
                    else
                    {
                        consecutiveHours = (shiftEnd - shiftStart).TotalHours;
                    }

                    lastEnd = shiftEnd;

                    if (consecutiveHours > 40)
                    {
                        penalty += (consecutiveHours - 40) * 10;
                        if (log != null)
                            log.Add($"KARA: Pracownik {kvp.Key} przepracował ciągiem {consecutiveHours}h bez dnia wolnego");
                    }
                }

                for (int i = 1; i < shifts.Count; i++)
                {
                    var prevEnd = GetShiftEnd(shifts[i - 1].Item1, shifts[i - 1].Item2);
                    var currStart = GetShiftStart(shifts[i].Item1, shifts[i].Item2);
                    var rest = (currStart - prevEnd).TotalHours;
                    if (rest < 12)
                    {
                        penalty += 20;
                        log.Add($"KARA: Pracownik {kvp.Key} miał za krótką przerwę ({rest:0.##}h) między zmianami");
                    }
                }

                // --- Maksymalnie 5 dni roboczych pod rząd ---
                var shiftDays = kvp.Value
                    .Select(s => s.Item1.Date)
                    .Distinct()
                    .OrderBy(d => d)
                    .ToList();

                int consecutiveWorkDays = 1;

                for (int i = 1; i < shiftDays.Count; i++)
                {
                    if ((shiftDays[i] - shiftDays[i - 1]).TotalDays == 1)
                    {
                        consecutiveWorkDays++;
                        if (consecutiveWorkDays > 5)
                        {
                            penalty += 100;
                            log?.Add($"KARA: Pracownik {kvp.Key} pracował {consecutiveWorkDays} dni z rzędu bez dnia wolnego");
                            break; // Możesz tu zrobić `break` lub kontynuować dalej
                        }
                    }
                    else
                    {
                        consecutiveWorkDays = 1;
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
                    if (weekEnd > shiftPeriods.Last().End) break;
                    var shiftsThisWeek = shiftPeriods
                        .Where(p => p.Start >= weekStart && p.Start <= weekEnd)
                        .ToList();

                    if (shiftsThisWeek.Count >= 2)
                    {
                        double maxBreak = 0;
                        for (int i = 1; i < shiftsThisWeek.Count; i++)
                        {
                            var previous = shiftsThisWeek[i - 1];
                            var current = shiftsThisWeek[i];

                            double breakHours = (current.Start - previous.End).TotalHours;
                            if (breakHours > maxBreak)
                                maxBreak = breakHours;
                        }

                        if (maxBreak < 35)
                        {
                            penalty += 100;
                            log.Add($"KARA: Pracownik {kvp.Key} nie miał 35h odpoczynku tygodniowego (tydzień od {weekStart:yyyy-MM-dd})");
                        }
                    }

                    weekStart = weekStart.AddDays(7);
                    weekEnd = weekStart.AddDays(7);
                }
            }

            double fitness = (1.0 + reward) / (1.0 + penalty);

            log.Insert(0, $"Fitness końcowy: {fitness:0.#####}");
            log.Insert(1, "--- Diagnoza ---");

            return log;
        }




        private static double GetDayWeight(DateTime date, DateTime periodEnd)
        {
            double weight = 1.0;

            // 🟠 Weekend ma większą wagę
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                weight += 0.5;

            // 🔴 Końcówka miesiąca = +1.0
            int daysToEnd = (periodEnd - date).Days;
            if (daysToEnd <= 3)
                weight += 1.0;

            return weight;
        }

        private static bool IsEligibleAfterPreviousShift(string empId, DateTime currentDate, ShiftType currentShift, Dictionary<string, (DateTime date, ShiftType shift)> lastShiftMap)
        {
            if (!lastShiftMap.TryGetValue(empId, out var lastShift))
                return true;

            var lastDate = lastShift.date.Date;
            var today = currentDate.Date;

            var dayDiff = (today - lastDate).Days;

            // 👇 Zakładamy że rozpatrujemy kolejne dni – wszystko tego samego dnia już i tak odpada
            if (dayDiff == 1)
            {
                // ⛔ Po nocnej tylko nocna
                if (lastShift.shift == ShiftType.Night && currentShift != ShiftType.Night)
                    return false;

                // ⛔ Po popołudniowej nie można porannej
                if (lastShift.shift == ShiftType.Afternoon && currentShift == ShiftType.Morning)
                    return false;
            }

            return true;
        }

        private static bool IsMaxConsecutiveHoursValid(string empId, DateTime date, ShiftType shift, Dictionary<string, (DateTime date, ShiftType shift)> lastShiftMap)
        {
            if (!lastShiftMap.TryGetValue(empId, out var lastShift))
                return true;

            DateTime prevEnd = GetShiftEnd(lastShift.date, lastShift.shift);
            DateTime currentEnd = GetShiftEnd(date, shift);

            if ((date - lastShift.date).TotalDays > 1)
                return true; // miał dzień przerwy

            double shiftDuration = (currentEnd - GetShiftStart(date, shift)).TotalHours;
            double previousDuration = (prevEnd - GetShiftStart(lastShift.date, lastShift.shift)).TotalHours;

            return (previousDuration + shiftDuration) <= 40;
        }

        public static double EvaluateFitnessWrapper(Chromosome chrom, Dictionary<DateTime, Dictionary<ShiftType, int>> req, int hours, HashSet<(string, DateTime)> vacations)
        {
            return EvaluateFitness(chrom, req, hours, vacations);
        }

    }
}