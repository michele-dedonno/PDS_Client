using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace ClientTest
{
    public class DirectoryItem : Item
    {
        public List<Item> Items { get; set; }

        public new bool ItemChecked
        {
            get
            {
                return this.itemChecked;
            }

            set
            {
                //if(itemChecked != value)
                //{
                    this.itemChecked = value;

                    //Console.WriteLine(ItemName + " : " + itemChecked);
                    foreach (var i in this.Items)
                    {
                        if (typeof(DirectoryItem) == i.GetType())
                            ((DirectoryItem)i).ItemChecked = this.itemChecked;
                        else
                            i.ItemChecked = this.itemChecked;

                        //Console.WriteLine("\t" + i.ItemName + " : " + i.itemChecked);
                    }

                    NotifyChange(new PropertyChangedEventArgs("ItemChecked"));
                //}
            }
        }
        

        public DirectoryItem()
        {
            Items = new List<Item>();
        }

        private bool CanSave()
        {
            return true;
        }

        private void OnChecked()
        {
            //Console.WriteLine(this.ItemChecked);
            //check/uncheck all nested folders/files
            foreach (Item i in this.Items)
                i.ItemChecked = this.ItemChecked;
        }



    }
}
