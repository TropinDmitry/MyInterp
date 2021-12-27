using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace MyInterp
{
    class MainProg
    {
        static void Main(string[] args)
        {
            string filePath = @"";
            if (File.Exists(filePath))
            {
                List<string> lines = new List<string>();
                lines = File.ReadAllLines(filePath).ToList();

                
            }
        }
    }
}
