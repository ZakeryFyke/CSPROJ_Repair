using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSPROJ_Repair
{
    class Testing
    {
        static void Main(string[] args)
        {
            var repaired_document = new CSPROJ.CSPROJ_Repair(@"C:\Users\Zakery\Documents\FundView.MVC.UI");
            repaired_document.RepairCSProj();

        }
    }
}
