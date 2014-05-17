using System;
using System.Collections.Generic;
using System.IO;

namespace InteractiveFiction
{
    class Program
    {
        static void Main(string[] args)
        {
            new World("world.txt").play();
        }
    }
}
