using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace CSPROJ
{
    public class CSPROJ_Repair
    {
        //public static string filePath = @"C:\Users\Zakery\Documents\FundView.MVC.UI";
        //public static string[] org_doc = File.ReadAllLines(filePath + ".csproj");
        //public static StreamWriter new_doc = new System.IO.StreamWriter(filePath + "-Updated" + ".csproj");
        //public static string filePath = @"C:\Users\Zakery\Documents\testing";
        //public static string[] org_doc = File.ReadAllLines(filePath + ".csproj");
        //public static StreamWriter new_doc = new System.IO.StreamWriter(filePath + "-Updated" + ".csproj");
        //public string filePath { get; set; }
        //public string[] org_doc { get; set; }
        //public StreamWriter new_doc { get; set; }
        public string[] org_doc;
        public CSPROJ_Repair(string path)
        {
            this.filePath = path;
            this.org_doc = File.ReadAllLines(filePath + ".csproj");
            this.new_doc = new StreamWriter(filePath + "-Updated" + ".csproj");
        }

        public void RepairCSProj()
        {
            string line;
            long counter = 0;
            List<string> SuperTagDictionary = new List<string>(new string[] { "ItemGroup" });
            List<string> GeneralTagDictionary = new List<string>(new string[] { "Compile", "Content", "None", "EmbeddedResource", "ProjectReference", "WCFMetadata", "Folder", "Reference", "Service", "ExcludeFromBuild" });
            List<string> InternalTagDictionary = new List<string>(new string[] { "<AutoGen>", "<DesignTime>", "<DependentUpon>", "<SubType>", "<Generator>", "<LastGenOutput>", "<CopyToOutputDirectory>", "<Project>", "<Name>", "<Private>", "<HintPath>", "<SpecificVersion>", "<DebugType>", "<DefineConstants>", "<PublishDatabases>", "<ErrorReport>", "<EmbedInteropTypes>" }); //If it's ugly, but it works, give it a job.

            //Read document line by line
            while (counter < org_doc.Length)
            {
                line = org_doc[counter];

                if (SuperTagDictionary.Any(x => line.Contains("<" + x))) //Super tags are always closed by </Tag>, never />
                {
                    counter = SuperTagStrategy(line, counter, SuperTagDictionary, GeneralTagDictionary, InternalTagDictionary);
                }
                else if (GeneralTagDictionary.Any(x => line.Contains("<" + x)) && !line.Contains("/>"))
                {
                    counter = GeneralTagStrategy(line, counter, GeneralTagDictionary, InternalTagDictionary);
                }
                else
                {
                    new_doc.WriteLine(line);
                    counter++;
                }
            }
            new_doc.Flush();
            new_doc.Close();
        }


        //Internal tags always follow the pattern of:
        // <Tag>"text"</Tag>
        public long InternalTagStrategy(string line, long counter, List<string> InternalTagDictionary)
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
            new_doc.WriteLine(line);
            counter++;
            return counter;
        }

        // We can expect 2 different types of General tag patterns.
        // <Compile Include ="text" />
        // <Compile Include ="text">
        //      Additional tags here
        // </Compile>
        public long GeneralTagStrategy(string line, long counter, List<string> GeneralTagDictionary, List<string> InternalTagDictionary)
        {
            string new_line;

            var tag = GeneralTagDictionary.Where(x => line.Contains(x)).FirstOrDefault();
            // If it doesn't contain an InternalTag, it's missing a /> tag and simply needs rewriting. 
            if (!InternalTagDictionary.Any(x => org_doc[counter + 1].Contains(x)))
            {
                var reg = Regex.Match(line, "\"([^\"]*)\"");
                var CSFile = reg.Groups[1].Value;
                new_line = "<" + tag + " Include=" + '"' + CSFile + '"' + " />";
                new_doc.WriteLine(new_line);
                counter++;
            }
            else
            {
                // Keep parsing line by line until you find a line that does not contain part of the TagDictionary
                new_doc.WriteLine(org_doc[counter]);
                counter++;
                while (InternalTagDictionary.Any(x => org_doc[counter].Contains(x)))
                {
                    // Confirm that the inner tag is valid and add it
                    new_line = org_doc[counter];
                    counter = InternalTagStrategy(new_line, counter, InternalTagDictionary);
                }
                //If the closing tag is missing, add the proper tag, otherwise just write the line. 
                if (!org_doc[counter].Contains("</" + tag))
                {
                    new_doc.WriteLine("</" + tag + ">");
                    counter++;
                }
                else
                {
                    new_doc.WriteLine(org_doc[counter]);
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
        public long SuperTagStrategy(string line, long counter, List<string> SuperTagDictionary, List<string> GeneralTagDictionary, List<string> InternalTagDictionary)
        {
            var tag = SuperTagDictionary.Where(x => line.Contains(x)).FirstOrDefault();

            // Write the SuperTag line and move to the next line
            new_doc.WriteLine(org_doc[counter]);
            counter++;

            // The next line must be a general tag. While you see general tags, check if they're valid and write them and their subtags. This is handled by GeneralTagStrategy.
            while (GeneralTagDictionary.Any(x => org_doc[counter].Contains(x))) // While the line contains a tag in the GeneralDictionary...
            {
                counter = GeneralTagStrategy(org_doc[counter], counter, GeneralTagDictionary, InternalTagDictionary);
            }
            // When the above loop breaks, it's because we've hit a </GeneralTag> or a </SuperTag>
            if (!org_doc[counter].Contains("</" + tag))
            {
                new_doc.WriteLine("</" + tag + ">");
                counter++;
            }

            return counter;
        }
    }
}
