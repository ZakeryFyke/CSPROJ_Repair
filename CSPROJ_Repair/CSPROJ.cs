using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace CSPROJ
{
    public class CSPROJ_Repair
    {
        public string filePath;
        public string[] org_doc;
        public StreamWriter new_doc;
        public List<string> AllTags = new List<string>();
        public List<string> SuperTagDictionary = new List<string>(new string[] { "ItemGroup" });
        public List<string> GeneralTagDictionary = new List<string>(new string[] { "Compile", "Content", "None", "EmbeddedResource", "ProjectReference", "WCFMetadata", "Folder", "Reference", "Service", "ExcludeFromBuild" });
        public List<string> InternalTagDictionary = new List<string>(new string[] { "<AutoGen>", "<DesignTime>", "<DependentUpon>", "<SubType>", "<Generator>", "<LastGenOutput>", "<CopyToOutputDirectory>", "<Project>", "<Name>", "<Private>", "<HintPath>", "<SpecificVersion>", "<DebugType>", "<DefineConstants>", "<PublishDatabases>", "<ErrorReport>", "<EmbedInteropTypes>" }); //If it's ugly, but it works, give it a job.

        public CSPROJ_Repair(string path)
        {
            this.filePath = path;
            this.org_doc = File.ReadAllLines(filePath + ".csproj");
            this.new_doc = new StreamWriter(filePath + "-Updated" + ".csproj");
        }

        // First fixes any missing or damaged tags, then removes any duplicate lines.
        public void RepairCSProj()
        {
            string line;
            long counter = 0;

            //Read document line by line
            while (counter < org_doc.Length)
            {
                line = org_doc[counter];

                if (SuperTagDictionary.Any(x => line.Contains("<" + x))) //Super tags are always closed by </Tag>, never />
                {
                    counter = SuperTagStrategy(line, counter);
                }
                else if (GeneralTagDictionary.Any(x => line.Contains("<" + x)) && !line.Contains("/>"))
                {
                    counter = GeneralTagStrategy(line, counter);
                }
                else
                {
                    new_doc.WriteLine(line);
                    counter++;
                }
            }
            new_doc.Flush();
            new_doc.Close();
            DuplicateStrategy();
            GetAllTags();
        }

        // Common issue is duplicate <Compile Include= "..." /> tags.
        public void DuplicateStrategy()
        {
            List<string> temp_Document = File.ReadAllLines(filePath + "-Updated" + ".csproj").ToList();
            var compileList = new List<string>();

            // Compile tags seem to be the only duplicates which appear.
            foreach (var line in temp_Document)
            {
                if (line.Contains("<Compile Include=") && line.Contains("/>"))
                {
                    compileList.Add(line);
                }
            }

            //If there's more than one, add each duplicate to duplicateList for removal
            var duplicateList = compileList.GroupBy(x => x)
                .SelectMany(group => group.Skip(1));

            foreach (var duplicate in duplicateList)
            {
                temp_Document.Remove(duplicate);
            }
            new_doc = new StreamWriter(filePath + "-Updated" + ".csproj");
            foreach (var line in temp_Document)
            {
                new_doc.WriteLine(line);
            }

            new_doc.Flush();
            new_doc.Close();
        }

        // Internal tags always follow the pattern of:
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
        public long GeneralTagStrategy(string line, long counter)
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
        // <SuperTag>
        //  <GeneralTag>.../>
        //  <GeneralTag>
        //      <InternalTag>True<InternalTag>
        //  </GeneralTag>   
        // </SuperTag>
        public long SuperTagStrategy(string line, long counter)
        {
            var tag = SuperTagDictionary.Where(x => line.Contains(x)).FirstOrDefault();

            // Write the SuperTag line and move to the next line
            new_doc.WriteLine(org_doc[counter]);
            counter++;

            while (GeneralTagDictionary.Any(x => org_doc[counter].Contains(x)))
            {
                counter = GeneralTagStrategy(org_doc[counter], counter);
            }
            if (!org_doc[counter].Contains("</" + tag))
            {
                new_doc.WriteLine("</" + tag + ">");
                counter++;
            }

            return counter;
        }

        public void GetAllTags()
        {
            // There's probably a better way to do this than to keep opening up the updated document, but w/e

            XDocument doc = XDocument.Load(filePath + "-Updated" + ".csproj");
            var Tags = new List<string>();

            foreach (var name in doc.Root.DescendantNodes().OfType<XElement>()
                .Select(x => x.Name).Distinct())
            {
                Tags.Add(name.ToString());
            }

            // Iunno dude this is at the front of all the tags I want. 
            AllTags = Tags.Select(y => y.Replace("{http://schemas.microsoft.com/developer/msbuild/2003}","")).ToList();
        }
    }
}