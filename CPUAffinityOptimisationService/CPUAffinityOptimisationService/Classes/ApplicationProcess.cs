
#region Using Statements

using System;
using System.Diagnostics;

#endregion

namespace CPUAffinityOptimisationService.Classes
{
    class ApplicationProcess
    {

        #region Properties

        /// <summary>
        /// The windows process object that holds the current application process.
        /// </summary>
        public Process Proc { get; set; }
        /// <summary>
        /// The current application process ID.
        /// </summary>
        public Int64 ID { get; set; }
        /// <summary>
        /// The amount of memory in bytes that the current application process uses at present.
        /// </summary>
        public Int64 MemorySize { get; set; }
        /// <summary>
        /// The name of the current application process.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// The processor affinity that the current process uses.
        /// </summary>
        public IntPtr Affinity
        {
            get { return Proc.ProcessorAffinity; }
            set { Proc.ProcessorAffinity = value; }
        }

        #endregion

    }
}
