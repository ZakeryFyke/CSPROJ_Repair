using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace CSPROJ_Repair
{
    class Program
    {

        public static class CSPROJ
        {
            //public static string filePath = @"C:\Users\Zakery\Documents\FundView.MVC.UI";
            //public static string[] org_doc = File.ReadAllLines(filePath + ".csproj");
            //public static StreamWriter new_doc = new System.IO.StreamWriter(filePath + "-Updated" + ".csproj");
            public static string filePath = @"C:\Users\Zakery\Documents\testing";
            public static string[] org_doc = File.ReadAllLines(filePath + ".csproj");
            public static StreamWriter new_doc = new System.IO.StreamWriter(filePath + "-Updated" + ".csproj");
        };

        static void Main(string[] args)
        {
            string line;
            long counter = 0;
            List<string> SuperTagDictionary = new List<string>(new string[] {"ItemgGroup" });
            List<string> GeneralTagDictionary = new List<string>(new string[] { "Compile", "Content", "None", "EmbeddedResource", "ProjectReference", "WCFMetadata", "Folder", "Reference" });
            //Read document line by line
            while (counter < CSPROJ.org_doc.Length)
            {
                line = CSPROJ.org_doc[counter];

                if(GeneralTagDictionary.Any(x => line.Contains("<" + x)) && !line.Contains("/>"))
                {
                    counter = GeneralStrategy(line, counter, GeneralTagDictionary);
                }else
                {
                    CSPROJ.new_doc.WriteLine(line);
                    counter++;
                }
            }
            CSPROJ.new_doc.Flush();
            CSPROJ.new_doc.Close();

        }

        //Internal tags always follow the pattern of:
        // <Tag>"text"</Tag>
        static long InternalTagStrategy(string line, long counter, List<string> InternalTagDictionary)
        {
            var tag = InternalTagDictionary.Where(x => line.Contains(x)).FirstOrDefault();
            
            var reg = Regex.Match(tag, @"(?<=\<)([^\>]*)");
            var tag_value = reg.Groups[1].Value;

            if (!line.Contains("</" + tag_value + ">"))
            {
                reg = Regex.Match(line, @"(?<=\>)([^\<]*)");
                var value = reg.Groups[1].Value;
                line = "<" + tag_value + ">" + value + "</" + tag_value + ">";
                
            }
            CSPROJ.new_doc.WriteLine(line);
            counter++;
            return counter;
        }

        // We can expect 2 different types of Compile or Context tag patterns.
        // <Compile Include ="text" />
        // <Compile Include ="text">
        //      Additional tags here
        // </Compile>
        static long GeneralStrategy(string line, long counter, List<string> GeneralTagDictionary)
        {
            string new_line;
            
            List<string> InternalTagDictionary = new List<string>(new string[] { "<AutoGen>", "<DesignTime>", "<DependentUpon>", "<SubType>", "<Generator>", "<LastGenOutput>", "<CopyToOutputDirectory>", "<Service>", "<Project>", "<Name>", "<Private>", "<HintPath>"});
            // If not one of the above tags, a closing tag is needed, so the line needs rewriting. 

            var tag = GeneralTagDictionary.Where(x => line.Contains(x)).FirstOrDefault();

            if (!InternalTagDictionary.Any(x => CSPROJ.org_doc[counter + 1].Contains(x)))
            {
                var reg = Regex.Match(line, "\"([^\"]*)\"");
                var CSFile = reg.Groups[1].Value;
                new_line = "<" + tag +" Include=" + '"' + CSFile + '"' + " />";
                CSPROJ.new_doc.WriteLine(new_line);
                counter++;
            }
            else
            {
                // Keep parsing line by line until you find a line that does not contain part of the TagDictionary
                CSPROJ.new_doc.WriteLine(CSPROJ.org_doc[counter]);
                counter++;
                while (InternalTagDictionary.Any(x => CSPROJ.org_doc[counter].Contains(x)))
                {
                    // Confirm that the inner tag is valid and add it
                    new_line = CSPROJ.org_doc[counter];
                    counter = InternalTagStrategy(new_line, counter, InternalTagDictionary);


                    //CSPROJ.new_doc.WriteLine(new_line);
                    //counter++;
                }
                if(!CSPROJ.org_doc[counter].Contains("</" + tag))
                {
                    CSPROJ.new_doc.WriteLine("</" + tag + ">");
                    counter++;
                }   
            }
            return counter;
        }
    }
}
