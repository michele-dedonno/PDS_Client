using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MahApps.Metro.Controls.Dialogs;
using System.Windows;

namespace ClientTest
{
    class WaitDialog : CustomDialog
    {
        public WaitDialog()
        {
            this.Title = "Attendere...";
            this.AddText("Terminazione dei processi in background in corso...");
        }
    }
}
