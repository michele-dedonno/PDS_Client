using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientTest
{
    public class SharedBool
    {
        private bool isRunning;
        
        public SharedBool(bool b)
        {
            isRunning = b;
        }

        public void setIsRunning(bool b)
        {
            isRunning = b;
        }

        public bool IsRunning()
        {
            return isRunning;
        }
    }
}
