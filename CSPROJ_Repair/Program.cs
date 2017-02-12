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
            public static string filePath = @"C:\Users\Zakery\Documents\FundView.MVC.UI";
            public static string[] org_doc = File.ReadAllLines(filePath + ".csproj");
            public static StreamWriter new_doc = new System.IO.StreamWriter(filePath + "-Updated" + ".csproj");
            //public static string filePath = @"C:\Users\Zakery\Documents\testing";
            //public static string[] org_doc = File.ReadAllLines(filePath + ".csproj");
            //public static StreamWriter new_doc = new System.IO.StreamWriter(filePath + "-Updated" + ".csproj");
        };

        static void Main(string[] args)
        {
            string line;
            long counter = 0;
            List<string> SuperTagDictionary = new List<string>(new string[] {"ItemGroup" });
            List<string> GeneralTagDictionary = new List<string>(new string[] { "Compile", "Content", "None", "EmbeddedResource", "ProjectReference", "WCFMetadata", "Folder", "Reference", "Service", "ExcludeFromBuild" });
            List<string> InternalTagDictionary = new List<string>(new string[] { "<AutoGen>", "<DesignTime>", "<DependentUpon>", "<SubType>", "<Generator>", "<LastGenOutput>", "<CopyToOutputDirectory>", "<Project>", "<Name>", "<Private>", "<HintPath>" , "<SpecificVersion>", "<DebugType>", "<DefineConstants>", "<PublishDatabases>", "<ErrorReport>", "<EmbedInteropTypes>"}); //If it's ugly, but it works, give it a job.

            //Read document line by line
            while (counter < CSPROJ.org_doc.Length)
            {
                line = CSPROJ.org_doc[counter];

                if (SuperTagDictionary.Any(x => line.Contains("<" + x))) //Super tags are always closed by </Tag>, never />
                {
                    counter = SuperTagStrategy(line, counter, SuperTagDictionary, GeneralTagDictionary, InternalTagDictionary);
                }
                else if(GeneralTagDictionary.Any(x => line.Contains("<" + x)) && !line.Contains("/>"))
                {
                    counter = GeneralStrategy(line, counter, GeneralTagDictionary, InternalTagDictionary);
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

        // We can expect 2 different types of General tag patterns.
        // <Compile Include ="text" />
        // <Compile Include ="text">
        //      Additional tags here
        // </Compile>
        static long GeneralStrategy(string line, long counter, List<string> GeneralTagDictionary, List<string> InternalTagDictionary)
        {
            string new_line;

            

            var tag = GeneralTagDictionary.Where(x => line.Contains(x)).FirstOrDefault();
            // If it doesn't contain an InternalTag, it's missing a /> tag and simply needs rewriting. 
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
                }
                if(!CSPROJ.org_doc[counter].Contains("</" + tag))
                {
                    CSPROJ.new_doc.WriteLine("</" + tag + ">");
                    counter++;
                }   
            }
            return counter;
        }

        // Super Tag patterns always follow something similar to that found below. They never directly contain an InternalTag unless it's inside a GeneralTag.
        //<SuperTag>
        //  <GeneralTag>.../>
        //  <GeneralTag>
        //      <InternalTag>True<InternalTag>
        //  </GeneralTag>   
        //</SuperTag>
        static long SuperTagStrategy(string line, long counter, List<string> SuperTagDictionary, List<string> GeneralTagDictionary, List<string> InternalTagDictionary)
        {
            string new_line;

            var tag = SuperTagDictionary.Where(x => line.Contains(x)).FirstOrDefault();
            //Since SuperTags always contain GeneralTags, we can skip the check.
            CSPROJ.new_doc.WriteLine(CSPROJ.org_doc[counter]);
            counter++;
                                                                                         // To stop things like "</Reference>", which should end the loop, from continuing it
            while (GeneralTagDictionary.Any(x => CSPROJ.org_doc[counter].Contains(x)) && !CSPROJ.org_doc[counter].Trim().StartsWith("</")) 
            {

                //Confirm that the General Tag is valid and add it.
                new_line = CSPROJ.org_doc[counter];
                var tag2 = GeneralTagDictionary.Where(x => new_line.Contains(x)).FirstOrDefault();
                if (new_line.Contains("<ItemGroup>"))
                {

                }

                counter = GeneralStrategy(new_line, counter, GeneralTagDictionary, InternalTagDictionary);
            }
            if (!CSPROJ.org_doc[counter].Contains("</" + tag))
            {
                CSPROJ.new_doc.WriteLine("</" + tag + ">");
                counter++;
            }

            return counter;
        }
    }
}
