using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ViewAppxPackage
{
    /// <summary>
    /// EventArgs that has a Handled property
    /// </summary>
    internal class HandleEventArgs : EventArgs
    {
        public bool Handled { get; set; } = false;
    }
}
