﻿using System;
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

    public class ChangeLog
    {
        public long duplicates_removed = 0;
        public long extra_text_removed = 0;
        public long internal_tags_repaired = 0;
        public long general_tags_repaired = 0;
        public long super_tags_repaired = 0;
        public StreamWriter doc;
        //public StreamWriter doc
        //{ set { CreateChangeLog(); } }

        public ChangeLog(string filePath)
        {
            var index = filePath.LastIndexOf("/");
            var path = filePath.Substring(0, filePath.LastIndexOf(@"\") + 1) + "changelog.txt";

            if (!File.Exists(path))
            {
                var file = File.CreateText(path);
                file.Close(); // Iunno, for reasons.
            }
            else
            {
                var file = new StreamWriter(path, true);
                file.Close();// Don't worry about it
            }
            doc = new StreamWriter(path, true);
        }

        public void UpdateChangeLog()
        {
            doc.WriteLine("-----------------");
            doc.WriteLine("Change Log for " + DateTime.Now + ":");
            doc.WriteLine("Duplicates Removed: " + duplicates_removed);
            doc.WriteLine("Extra Text Removed: " + extra_text_removed);
            doc.WriteLine("Internal Tags Repaired: " + internal_tags_repaired);
            doc.WriteLine("General Tags Repaired: " + general_tags_repaired);
            doc.WriteLine("Super Tags Repaired: " + super_tags_repaired);
            //var lines = new List<string>();
            //lines.Add("Change Log for " + DateTime.Now + ":");
            //lines.Add("Duplicates Removed: " + duplicates_removed);
            //lines.Add("Extra Text Removed: " + extra_text_removed);
            //lines.Add("Internal Tags Removed: " + internal_tags_repaired);
            //lines.Add("General Tags Removed: " + general_tags_repaired);
            //lines.Add("Super Tags Removed: " + super_tags_repaired);

            //doc.WriteLine(lines);

            doc.Flush();
            doc.Close();
        }
    }

    public class CSPROJ_Repair
    {
        public string filePath;
        public string[] org_doc;
        public StreamWriter temp_doc;
        public StreamWriter new_doc;
        public ChangeLog log;
        //public List<string> AllTags = new List<string>();
        public List<string> SuperTagDictionary = new List<string>(new string[] { "ItemGroup" });
        public List<string> GeneralTagDictionary = new List<string>(new string[] { "Compile", "Content", "None", "EmbeddedResource", "ProjectReference", "WCFMetadata", "Folder", "Reference", "Service", "ExcludeFromBuild" });
        public List<string> InternalTagDictionary = new List<string>(new string[] { "<AutoGen>", "<DesignTime>", "<DependentUpon>", "<SubType>", "<Generator>", "<LastGenOutput>", "<CopyToOutputDirectory>", "<Project>", "<Name>", "<Private>", "<HintPath>", "<SpecificVersion>", "<DebugType>", "<DefineConstants>", "<PublishDatabases>", "<ErrorReport>", "<EmbedInteropTypes>" }); //If it's ugly, but it works, give it a job.

        public CSPROJ_Repair(string path)
        {
            filePath = path;
            org_doc = File.ReadAllLines(filePath + ".csproj");
            temp_doc = new StreamWriter(filePath + "-Temp" + ".csproj");
            new_doc = new StreamWriter(filePath + "-Updated" + ".csproj");
            log = new ChangeLog(filePath);
        }

        // First fixes any missing or damaged tags, then removes any duplicate lines.
        public void RepairCSProj()
        {
            string line;
            long counter = 0;

            // First, remove any extra text outside of tags that might make this an invalid document
            RemoveExtraText();

            var tmp_doc = File.ReadAllLines(filePath + "-Temp" + ".csproj");
            //File.Delete(filePath + "-Temp" + ".csproj");
            
            // There's probably a less ugly way to do this
            temp_doc = new StreamWriter(filePath + "-Temp" + ".csproj");

            //Read the temporary document line by line
            while (counter < tmp_doc.Length)
            {
                line = tmp_doc[counter];

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
                    temp_doc.WriteLine(line);
                    counter++;
                }
            }

            // When we reach this point, all tags have been repaired and are sitting in the temp_document. All that's left is to remove duplicates. 

            temp_doc.Flush();
            temp_doc.Close();
            DuplicateStrategy();
            //GetAllTags();
            log.UpdateChangeLog();
        }

        // Occasionally, extraneous text is inserted into the document and must be removed. 
        public void RemoveExtraText()
        {
            string line;
            long counter = 1;
            var tmp_doc = File.ReadAllLines(filePath + ".csproj");
            temp_doc.WriteLine(tmp_doc[0]); // This line contains the xml version and I don't wanna deal with it. :) 
            while(counter < tmp_doc.Length)
            {
                line = tmp_doc[counter];
                if ((line.Contains("<") || line.Contains(">")))
                {
                    temp_doc.WriteLine(line);
                }else
                {
                    if(line == ""){
                        Console.WriteLine("Disgarding a blank line.");
                    }else{
                        Console.WriteLine("Disregarding " + line.TrimStart());
                    }
                    log.extra_text_removed++;
                }
                counter++;
            }

            temp_doc.Flush();
            temp_doc.Close();   
        }

        // Common issue is duplicate <Compile Include= "..." /> tags.
        public void DuplicateStrategy()
        {
            List<string> tmp_doc = File.ReadAllLines(filePath + "-Temp" + ".csproj").ToList();
            var compileList = new List<string>();

            // Compile tags seem to be the only duplicates which appear.
            foreach (var line in tmp_doc)
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
                tmp_doc.Remove(duplicate);
                Console.WriteLine("Removing duplicate line \"" + duplicate.TrimStart() + "\"");
                log.duplicates_removed++;
            }

            //temp_doc = new StreamWriter(filePath + "-Temp" + ".csproj");

            foreach (var line in tmp_doc)
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
                log.internal_tags_repaired++;
            }

            temp_doc.WriteLine(line);
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
                temp_doc.WriteLine(new_line);
                counter++;
                log.general_tags_repaired++;
                Console.WriteLine(counter);
            }
            else
            {
                // Keep parsing line by line until you find a line that does not contain part of the TagDictionary
                temp_doc.WriteLine(org_doc[counter]);
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
                    temp_doc.WriteLine("</" + tag + ">");
                    counter++;
                    log.general_tags_repaired++;
                }
                else
                {
                    temp_doc.WriteLine(org_doc[counter]);
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
            temp_doc.WriteLine(org_doc[counter]);
            counter++;

            while (GeneralTagDictionary.Any(x => org_doc[counter].Contains(x)))
            {
                counter = GeneralTagStrategy(org_doc[counter], counter);
            }
            if (!org_doc[counter].Contains("</" + tag))
            {
                temp_doc.WriteLine("</" + tag + ">");
                counter++;
                log.super_tags_repaired++;
            }

            return counter;
        }

        //public void GetAllTags()
        //{
        //    // There's probably a better way to do this than to keep opening up the updated document, but w/e

        //    XDocument doc = XDocument.Load(filePath + "-Temp" + ".csproj");
        //    var Tags = new List<string>();

        //    foreach (var name in doc.Root.DescendantNodes().OfType<XElement>()
        //        .Select(x => x.Name).Distinct())
        //    {
        //        Tags.Add(name.ToString());
        //    }

        //    // Iunno dude this is at the front of all the tags I want. 
        //    AllTags = Tags.Select(y => y.Replace("{http://schemas.microsoft.com/developer/msbuild/2003}","")).OrderBy(x => x).ToList();
        //}
    }
}