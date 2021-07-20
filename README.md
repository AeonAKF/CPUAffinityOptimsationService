# CPUAffinityOptimsationService
A C# .NET Framework 4.8 Servics that optimises the CPU affinity for an given application., the default being firefox. All CPUs are split between the number of instances of the given application.

Once all instances of the application in question have been accessed, the OptimiseCPUAffinity() function sets the CPU affinity of each process depending on the number of processes in relation to the number of CPUs.

1 - For 1 process = all CPUs are applied to that process
2 - For 2 processes up to (processes < CPUCount / 2) = All CPUs split equally between all processes, example: 10 CPUs - 4 processes = 2 CPUs for each process
3 - For (CPUCount / 2) processes up to Processes > CPUCount = 1 CPU for 1 process.
