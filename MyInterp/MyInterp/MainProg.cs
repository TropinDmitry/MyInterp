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
            string filePath = @"D:\I am Photoshop, it is a Programmer\Visual Studio\C#\mylang\mylang\test.txt";
            if (File.Exists(filePath))
            {
                List<string> lines = new List<string>();
                lines = File.ReadAllLines(filePath).ToList();

                var res =LexicAnalyser.Analyze(lines);
                var ops = OPSGenerator.GenerateOPS(res);
            }
        }
    }
}
