using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestXTemplate
{
    class Start
    {
        public static string[] Args;
        [STAThread]
        public static void Main(string[] args)
        {
            Args = args;
            App app = new App();
            app.InitializeComponent();
            app.Run();
        }
    }
}
