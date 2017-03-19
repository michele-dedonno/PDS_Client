using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientTest
{
    class SessionAuthenticationException:Exception
    {
        public SessionAuthenticationException(string message) : base(message)
        {
        }
    }
}
