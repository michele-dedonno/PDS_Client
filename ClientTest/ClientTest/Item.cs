using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace ClientTest
{
    public class Item : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected string itemName;
        public string ItemName
        {
            get
            {
                return itemName;
            }

            set
            {
                
                if (itemName != value)
                {
                    itemName = value;
                    NotifyChange(new PropertyChangedEventArgs("ItemName"));
                }
                   
            }
        }

        protected string itemPath;
        public string ItemPath {
            get
            {
                return itemPath;
            }

            set
            {
                
                if (itemPath != value)
                {
                    itemPath = value;
                    NotifyChange(new PropertyChangedEventArgs("ItemPath"));
                }
                    
            }
        }

        protected bool itemChecked;
        public bool ItemChecked
        {
            get
            {
                return itemChecked;
            }

            set
            {
                if(itemChecked != value)
                {
                    itemChecked = value;
                    NotifyChange(new PropertyChangedEventArgs("ItemChecked"));
                }
                    
            }
        }
        
        protected void NotifyChange(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }
    }


}
