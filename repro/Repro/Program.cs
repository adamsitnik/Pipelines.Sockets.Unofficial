using Benchmarks;
using System;

namespace Repro
{
    class Program
    {
        static void Main(string[] args)
        {
            var tests = new BasicTests();

            // fails with
            // Unhandled Exception: System.IO.FileLoadException: Could not load file or assembly 'System.Buffers, Version=4.0.2.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51' or one of its dependencies. 
            // The located assembly's manifest definition does not match the assembly reference. (Exception from HRESULT: 0x80131040)
            tests.BasicPipelines().GetAwaiter().GetResult(); 
        }
    }
}
