
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using static System.Net.Mime.MediaTypeNames;
using CPUAffinityOptimisationService.Classes;
using System.Reflection;

namespace CPUAffinityOptimisationService
{
    public partial class srvMain : ServiceBase
    {

        #region Variables, Constants & Objects

        internal static string strApplicationName = "Firefox"; // The name of the application to be optimised
        private static string[] strArrayProcessNames = { "firefox" }; // The names of the processes to be included in the optimisation process.
        private static string ProgramFolder = Path.GetFullPath(Assembly.GetExecutingAssembly().Location.Replace("\\" + Assembly.GetExecutingAssembly().GetName().Name + ".exe", ""));
        private static int intNumberofCPUsPerBunch = 0; // Number of CPUs / processes
        private System.Threading.Timer oTimer; //Will run program at certain times
        private static Process[] AllProcesses; //Process Array to hold all Currently running processes on the computer
        private static int ProcessorNumber = Environment.ProcessorCount; //The number of Processors in the Computer
        private static bool moreProcessesThanProcessors = false; //True if there are more process active in comparison to CPU cores.
        private static bool twoProcessesSameCPU = false; //True if there are two or more processes using the same CPU.
        private static List<ApplicationProcess> ApplicationProcList = new List<ApplicationProcess>(); //The List to hold the Process objects
        private static List<Int64> CPUAffinityListA = new List<Int64>(); //The List to hold the Process objects
        private static List<Int64> CPUAffinityListB = new List<Int64>(); //The List to hold the 2nd Process objects
        private static List<string> CPUAffinityList = new List<string>(); //The List to hold the 1s location in Binary String if single CPU affinity
        private static string LastQuarterCPUAffinity = ""; //String to hold binary representation of all CPU affinity: Ex. for 8 CPUs - 11000000#
        private static List<ApplicationProcess> ApplicationProcListSorted = new List<ApplicationProcess>(); //The List to hold the Process objects Sorted
        private static string LogString = ""; //Data the will be written to the Log
        private static bool verBose = false; //Determines Logging Level
        private static string strDiags = ""; //Diagnostic String

        //Generic Variables
        private static string TempStr1 = "";
        private static string strBinary = "";
        private static string strBinary1 = "";
        private static int CPUAffinityListAPosition = 0;
        private static int CPUAffinityListBPosition = 0;
        private static int timeHrs = 0;
        private static int timeMins = 10;
        private static int timeSecs = 0;

        #endregion

        #region Functions

        #region Constructors

        public srvMain()
        {
            InitializeComponent();

            // Event handlers
            TimerCallback tmrCallBack = new TimerCallback(oTimer_TimerCallback);

            // Initial configuration
            oTimer = new System.Threading.Timer(tmrCallBack);
            // Set the Timer start in 1 second, and then fire every ten minutes
            oTimer.Change(new TimeSpan(0, 0, 1), new TimeSpan(timeHrs, timeMins, timeSecs));

        }

        #endregion

        #region Event Functions

        /// <summary>
        /// Runs when the service is started.
        /// </summary>
        /// <param name="args">Command line arguments that will be used in the function. args[0] is the full path of the service file.</param>
        protected override void OnStart(string[] args)
        {
            //Write to Log
            string xstr1 = strApplicationName + " affinity optimiser log starting: " + DateTime.Now.ToString() + Environment.NewLine +
                Environment.MachineName + " has " + ProcessorNumber.ToString() + " Processors" + Environment.NewLine +
                Environment.NewLine;

            // Write to log
            WriteToLog(xstr1);
        }

        /// <summary>
        /// Runs when the service is stopped.
        /// </summary>
        protected override void OnStop()
        {
            //Write to Log
            string xstr1 = Environment.NewLine + strApplicationName + " affinity optimiser log stopping: " + DateTime.Now.ToString() + Environment.NewLine;
            WriteToLog(xstr1);
        }

        /// <summary>
        /// Event function that occurs whenever the timer fires and starts the OptimiseCPUAffinity() function.
        /// </summary>
        /// <param name="state"></param>
        private void oTimer_TimerCallback(object state)
        {
            // Manually stop the timer...
            oTimer.Change(Timeout.Infinite, Timeout.Infinite);

            // Optimise CPU affinity
            OptimiseCPUAffinity();

            // Set the timer start in 1 minute, and then fire once every ten minutes
            oTimer.Change(new TimeSpan(timeHrs, timeMins, timeSecs), new TimeSpan(timeHrs, timeMins, timeSecs));
        }

        #endregion

        #region User Functions

        /// <summary>
        /// Updates the log file with latest log entry.
        /// </summary>
        /// <param name="LogString">The string object to be written to the log.</param>
        private static void WriteToLog(string LogString)
        {
            // If the log file already exists
            if (File.Exists(ProgramFolder + "\\" + strApplicationName + "Affinity.log") == false)
            {
                try
                {
                    // Attempt to write to the log file.
                    using (StreamWriter w = File.CreateText(ProgramFolder + "\\" + strApplicationName + "Affinity.log")) //Create Log File
                    {
                        w.Write(LogString);
                        // using statement automatically closes StreamWriter when done.
                    }
                }
                catch
                {
                    // Couldn't write to log
                    return;
                }
            }
            else // Log file does not already exist
            {
                try
                {
                    FileInfo f1 = new FileInfo(ProgramFolder + "\\" + strApplicationName + "Affinity.log");
                    // If File size is bigger than 4Mb then replace file
                    if (f1.Length > 4294967296)
                    {
                        f1.Delete();
                        using (StreamWriter w = File.CreateText(ProgramFolder + "\\" + strApplicationName + "Affinity.log")) //Create Log File
                        {
                            w.Write(LogString); //Write to Log file
                            // using statement automatically closes StreamWriter when done.
                        }
                    }
                    else
                    {
                        using (StreamWriter w = File.AppendText(ProgramFolder + "\\" + strApplicationName + "Affinity.log"))
                        {
                            w.Write(LogString); //Write to Log file
                            // using statement automatically closes StreamWriter when done.
                        }
                    }
                }
                catch
                {
                    // Couldn't write to log
                    return;
                }
            }
        }

        /// <summary>
        /// Applies a unique set of processors to each application process.
        /// 1 Processes = all CPUs, (2 processes to < CPUCount / 2) = All CPUs split between all processes,
        /// ((CPUCount / 2) processes to Processes == CPUCount = 1 CPU for 1 process.
        /// Processes > CPUs = first 3/4 CPUs applied to single process then last 1/4 CPUs applied to rest of processes. 
        /// </summary>
        private static void OptimiseCPUAffinity()
        {
            #region Parameter & Object Configuration

            AllProcesses = Process.GetProcesses(); // Get all currently running processes

            if (ProcessorNumber < 3) { goto End; } // Runs only if processors > 2. Use goto because just cannot get return to work here but works everywhere else, strange.

            // Cycle through all processes
            foreach (Process AProcess in AllProcesses)
            {
                // For any process that is the application in question
                if (strArrayProcessNames.Contains<string>(AProcess.ProcessName))
                {
                    ApplicationProcess ApplicationProcess1 = new ApplicationProcess();
                    ApplicationProcess1.ID = AProcess.Id; // Process ID number
                    ApplicationProcess1.MemorySize = AProcess.WorkingSet64; // Process physical memory usage
                    ApplicationProcess1.Proc = AProcess;
                    ApplicationProcList.Add(ApplicationProcess1); // Create entry for current application process
                    CPUAffinityListA.Add(AProcess.ProcessorAffinity.ToInt64()); // Collects data on which CPU cores each process is using
                }
            }

            if (verBose == true)
            {
                strDiags = strDiags + strApplicationName + " processes: " + ApplicationProcList.Count.ToString() + Environment.NewLine;
            }

            // No processes so exit
            if (ApplicationProcList.Count == 0) { goto End; } // Same as above about goto

            // One process so set affinity to all CPU's
            if (ApplicationProcList.Count == 1)
            {
                ApplicationProcess xProcess = ApplicationProcList[0];

                string str1 = "";
                for (int x = 0; x < ProcessorNumber; x++)
                {
                    str1 = str1 + "1";
                }

                long long1 = Convert.ToInt64(str1, 2); // Convert binary to decimal

                try
                {
                    xProcess.Proc.ProcessorAffinity = (System.IntPtr)long1; // Set processor affinity
                    LogString = LogString + strApplicationName + " process: " + xProcess.Proc.Id.ToString() + " now using CPU: " + Convert.ToString(long1, 2) +
                    Environment.NewLine;
                }
                catch (Exception ex)
                {
                    //Write Error to Log
                    LogString = LogString + "Error: " + strApplicationName + " process: " + xProcess.Proc.Id.ToString() + " unable to set affinity: " +
                   "Exception:" + ex.Message + Environment.NewLine;
                }

                goto End;
            }

            // 2nd CPUAffinityLIst for compare
            CPUAffinityListB.AddRange(CPUAffinityListA);

            // Sort list min to max by way of memory size
            ApplicationProcListSorted = ApplicationProcList.OrderBy(ApplicationProcess => ApplicationProcess.MemorySize).ToList();

            // Test to see if more processes than CPU Cores
            if (ApplicationProcList.Count > ProcessorNumber) { moreProcessesThanProcessors = true; }
            else { moreProcessesThanProcessors = false; }

            Int64 intTotal = 0;

            // Create binary representation for CPU number
            // 0001 for CPU 1, 0010 for CPU 2, 0100 for CPU 3, etc
            // For the number of unique CPU binary strings needed
            try
            {
                for (int g = 0; g < ProcessorNumber; g++)
                {

                    // Each string will have ProcessNumber characters.
                    // For the number of characters
                    string strTemp01 = string.Empty;
                    for (int x = 0; x < ProcessorNumber; x++)
                    {

                        if (x == g)
                        {
                            strTemp01 = "1" + strTemp01;
                        }
                        else
                        {
                            strTemp01 = "0" + strTemp01;
                        }
                    }

                    intTotal = intTotal + Convert.ToInt64(strTemp01, 2);

                    //Create list of CPU affinity binary 1 locations in single CPU binary string
                    CPUAffinityList.Add(intTotal.ToString());
                    strTemp01 = string.Empty;
                    intTotal = 0;

                }
            }
            catch (Exception ex)
            {
                WriteToLog(ex.Message);
            }

            // Foe every entry in the unsorted list of CPU affinity Int64s for all application processes
            foreach (Int64 xInt in CPUAffinityListA)
            {
                // Current character position in CPUAffinityListPosition
                CPUAffinityListAPosition = CPUAffinityListAPosition + 1;

                // Convert process affinity Int64 to binary string 
                strBinary = Convert.ToString(xInt, 2);

                //Convert process affinity binary string strBinary: Ex. "1" into proper binary string "0001"
                for (int x2 = 0; x2 < ProcessorNumber; x2++)
                {
                    if (strBinary.Length == ProcessorNumber) { break; }
                    strBinary = "0" + strBinary;
                }

                // Foe every entry in the unsorted list of CPU affinity Int64s for all application processes that is used for a comparison
                foreach (Int64 xInt1 in CPUAffinityListB)
                {
                    // Current character position in CPUAffinityListPosition
                    CPUAffinityListBPosition = CPUAffinityListBPosition + 1;

                    // Convert process affinity Int64 to binary string 
                    strBinary1 = Convert.ToString(xInt1, 2);

                    // Convert process affinity binary string strBinary: Ex. "1" into proper binary string "0001"
                    for (int x3 = 0; x3 < ProcessorNumber; x3++)
                    {
                        if (strBinary1.Length == ProcessorNumber) { break; }
                        strBinary1 = "0" + strBinary1;
                    }

                    // Check for locations of 1s in string
                    if (CPUAffinityListAPosition != CPUAffinityListBPosition)
                    {
                        for (int c1 = 0; c1 < strBinary.Length; c1++) // Cycle through binary representation of one of process affinity
                        {
                            if (strBinary.ElementAt(c1).ToString() == "1") // if character is a 1 and hence uses that corresponding CPU
                            {
                                if (strBinary1.ElementAt(c1).ToString() == "1") // if character is a 1 and hence uses that corresponding CPU
                                {
                                    twoProcessesSameCPU = true;
                                    break;
                                }
                                if (twoProcessesSameCPU == true) { break; }
                            }
                        }
                    }
                }
                CPUAffinityListBPosition = 0;
                if (twoProcessesSameCPU == true) { break; }
            }
            CPUAffinityListAPosition = 0;
            CPUAffinityListBPosition = 0;

            #endregion Parameter & Object Configuration

            #region If there are more application processes than CPUs

            // different types of affinity configuration dependent on whether there are more application processes than Processors
            if (moreProcessesThanProcessors == true)
            {
                int threequartersOfProcessors = Convert.ToInt32(3 * (ProcessorNumber / 4));
                int lastquarterOfProcessors = ProcessorNumber - threequartersOfProcessors;

                // Create binary string for last quarter of all processors
                for (int p = 0; p < ProcessorNumber; p++)
                {
                    if (p >= ProcessorNumber - lastquarterOfProcessors)
                    {
                        LastQuarterCPUAffinity = "1" + LastQuarterCPUAffinity;
                    }
                    else
                    {
                        LastQuarterCPUAffinity = "0" + LastQuarterCPUAffinity;
                    }
                }

                if (verBose == true)
                {
                    strDiags = strDiags + "Processor division - 3 * (Total Processors / 4 ) for single Core operation and last (Total Processors / 4 )" +
                        " for rest of " + strApplicationName + " processes shared operation: " + threequartersOfProcessors.ToString() + ":" + lastquarterOfProcessors.ToString() +
                        Environment.NewLine;
                }

                // First 3 quarters for single CPU affinity and last quarter of processors for multi CPU affinity
                for (int xb = 0; xb < ApplicationProcListSorted.Count; xb++)
                {
                    if (xb <= threequartersOfProcessors)
                    {
                        long long1 = Convert.ToInt64(CPUAffinityList[xb], 2); // Convert binary to decimal
                        try
                        {
                            ApplicationProcListSorted[xb].Proc.ProcessorAffinity = (System.IntPtr)long1; // Set processor affinity
                            LogString = LogString + strApplicationName + " Process: " + ApplicationProcListSorted[xb].ID.ToString() + " now using CPU: " + Convert.ToString(long1, 2) +
                            Environment.NewLine;
                        }
                        catch (Exception ex)
                        {
                            // Write error to log
                            LogString = LogString + "Error: " + strApplicationName + " process: " + ApplicationProcListSorted[xb].ID.ToString() + " unable to set affinity: " +
                           "Exception:" + ex.Message + Environment.NewLine;
                        }
                    }
                    else
                    {
                        long long2 = Convert.ToInt64(LastQuarterCPUAffinity, 2); // Convert binary to decimal
                        try
                        {
                            ApplicationProcListSorted[xb].Proc.ProcessorAffinity = (System.IntPtr)long2; // Set processor affinity
                            LogString = LogString + strApplicationName + " process: " + ApplicationProcListSorted[xb].ID.ToString() + " now using CPU: " + Convert.ToString(long2, 2) +
                            Environment.NewLine;
                        }
                        catch (Exception ex)
                        {
                            // Write error to log
                            LogString = LogString + "Error: " + strApplicationName + " process: " + ApplicationProcListSorted[xb].ID.ToString() + " unable to set affinity: " +
                           "Exception:" + ex.Message + Environment.NewLine;
                        }
                    }
                }
            }

            #endregion If there are more application processes than CPUs

            #region If CPUs > application processes < (CPUS / 2)

            if (moreProcessesThanProcessors == false)
            {
                if (ApplicationProcList.Count <= (ProcessorNumber / 2)) // If number of processes is less than processor count / 2
                {

                    // Rounds down if not an integer, however I have never heard of a PC with an odd number of processors.
                    int intHalfOfAllProcessors = Convert.ToInt32(Math.Floor(Convert.ToDecimal(ProcessorNumber / 2)));

                    // And Get remainder of CPUs if not an equal division between processes
                    intNumberofCPUsPerBunch = Convert.ToInt32(Math.Floor(Convert.ToDecimal(ProcessorNumber / ApplicationProcList.Count)));
                    int intRemainderOfCPUs = (ProcessorNumber % ApplicationProcList.Count);

                    // Create CPU bunches in binary string format
                    List<string> lstCPUBunches = new List<string>();
                    int intCurrentProcessor = 0;
                    string strTemp01 = string.Empty;

                    // For each application process
                    for (int z1 = 0; z1 < ApplicationProcList.Count; z1++)
                    {
                        int z4 = intCurrentProcessor + intNumberofCPUsPerBunch;

                        // Get total string for number of CPU strings needed for each process
                        for (int z2 = intCurrentProcessor; z2 < z4; z2++)
                        {

                            // Get CPU string
                            for (int z3 = 0; z3 < ProcessorNumber; z3++)
                            {
                                // If current CPU string position = intCurrentProcessor
                                if (z3 == intCurrentProcessor)
                                {
                                    strTemp01 = "1" + strTemp01;
                                }
                                else if (z3 < intCurrentProcessor) // Put 0's after 1
                                {
                                    strTemp01 = strTemp01 + "0";
                                }
                                else if (z3 > intCurrentProcessor) // Put 0's before 1
                                {
                                    strTemp01 = "0" + strTemp01;
                                }
                            }

                            intTotal = intTotal + Convert.ToInt64(strTemp01, 2);
                            strTemp01 = string.Empty;
                            intCurrentProcessor++;
                        }
                        // Add single bunch to bunch list
                        lstCPUBunches.Add(intTotal.ToString());
                        intTotal = 0;
                    }

                    for (int p2 = 0; p2 < ApplicationProcList.Count; p2++)
                    {
                        // Take the single bunch from lstCPUBunches[p2] and create a single binary string that represents 
                        // all CPUs in that bunch 
                        long long3 = Convert.ToInt64(lstCPUBunches[p2], 2);

                        try
                        {
                            ApplicationProcList[p2].Proc.ProcessorAffinity = (System.IntPtr)long3; // Set Processor affinity
                            LogString = LogString + " " + strApplicationName + " process: " + ApplicationProcList[p2].Proc.Id.ToString() + " now using CPU: " + Convert.ToString(long3, 2) +
                            Environment.NewLine;
                        }
                        catch (Exception ex)
                        {
                            // Write error to log
                            LogString = LogString + "Error: " + strApplicationName + " process: " + ApplicationProcList[p2].Proc.Id.ToString() + " unable to set affinity: " +
                           "Exception:" + ex.Message + Environment.NewLine;
                        }

                    }
                }
                else // Number of processes is greater than processor count / 2
                {

                    int p2 = 0;
                    foreach (ApplicationProcess xProcess in ApplicationProcList)
                    {
                        long long3 = Convert.ToInt64(CPUAffinityList[p2], 2); // Convert binary to decimal
                        try
                        {
                            xProcess.Proc.ProcessorAffinity = (System.IntPtr)long3; // Set processor affinity
                            LogString = LogString + " " + strApplicationName + " process: " + xProcess.Proc.Id.ToString() + " now using CPU: " + Convert.ToString(long3, 2) +
                            Environment.NewLine;

                        }
                        catch (Exception ex)
                        {
                            // Write error to log
                            LogString = LogString + "Error: " + strApplicationName + " process: " + xProcess.Proc.Id.ToString() + " unable to set affinity: " +
                           "Exception:" + ex.Message + Environment.NewLine;
                        }
                        p2++;
                    }
                }
            }

        #endregion If CPUs > application processes < (CPUS / 2)

        #region Write to log, reset variables for next run and exit function

        End:

            // Write operation data to log
            WriteToLog(Environment.NewLine + LogString + Environment.NewLine);

            // Reset variables for next run
            ResetVariables();

            #endregion Write to log, reset variables for next run and exit function

        }

        /// <summary>
        /// Reset all variables to a neutral value for the next run of the OptimiseCPUAffinity function.
        /// </summary>
        private static void ResetVariables()
        {
            if (verBose == true)
            {
                strDiags = strDiags + "Resetting variables: " + Environment.NewLine +
                    "ProcessorNumber: " + ProcessorNumber.ToString() + Environment.NewLine +
                    "moreProcessorsThanProcessors: " + moreProcessesThanProcessors.ToString() + Environment.NewLine +
                    "twoProcessesSameCPU: " + twoProcessesSameCPU.ToString() +
                     Environment.NewLine +
                     "ApplicationProcList: " + ApplicationProcList.Count.ToString() + Environment.NewLine +
                     "ApplicationAffinityListA: " + CPUAffinityListA.Count.ToString() +
                     Environment.NewLine +
                     "ApplicationAffinityListB: " + CPUAffinityListA.Count.ToString() + Environment.NewLine +
                     "CPUAffinityList: " + CPUAffinityList.Count.ToString() + Environment.NewLine +
                     "LastQuarterCPUAffinity: " + LastQuarterCPUAffinity + Environment.NewLine +
                    "ApplicationProcListSorted: " + ApplicationProcListSorted.Count.ToString() + Environment.NewLine +
                    "Tempstr1: " + TempStr1 + Environment.NewLine +
                    "strBinary: " + strBinary + Environment.NewLine +
                    "strBinary1: " + strBinary1 + Environment.NewLine +
                    "ApplicationAffinityListAPosition: " + CPUAffinityListAPosition.ToString() + Environment.NewLine +
                    "ApplicationAffinityListBPosition: " + CPUAffinityListAPosition.ToString() + Environment.NewLine +
                    Environment.NewLine;
            }

            ProcessorNumber = Environment.ProcessorCount; // Get number of processors

            //Reset Variables
            Array.Clear(AllProcesses, 0, AllProcesses.Length);
            moreProcessesThanProcessors = false;
            twoProcessesSameCPU = false;
            ApplicationProcList.Clear();
            CPUAffinityListA.Clear();
            CPUAffinityListB.Clear();
            CPUAffinityList.Clear();
            LastQuarterCPUAffinity = "";
            ApplicationProcListSorted.Clear();
            LogString = "";
            TempStr1 = "";
            strBinary = "";
            strBinary1 = "";
            CPUAffinityListAPosition = 0;
            CPUAffinityListBPosition = 0;

            //Write to Log
            string xstr1 = strApplicationName + " Affinity Optimizer Log: " + DateTime.Now.ToString() + Environment.NewLine +
                Environment.MachineName + " has " + ProcessorNumber.ToString() + " Processors" + Environment.NewLine +
                Environment.NewLine;

            // Write
            WriteToLog(xstr1);

            if (verBose == true)
            {
                strDiags = "Diagnostics: " + Environment.NewLine +
                    strDiags + Environment.NewLine +
                    "End of Run" + Environment.NewLine;
                WriteToLog(strDiags);
                strDiags = "";

            }
        }

        #endregion

        #endregion

    }
}
