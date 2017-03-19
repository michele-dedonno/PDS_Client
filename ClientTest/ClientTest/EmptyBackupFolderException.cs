using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientTest
{
    class EmptyBackupFolderException : Exception
    {
        public EmptyBackupFolderException(string message) : base(message)
        {
        }
    }
}
