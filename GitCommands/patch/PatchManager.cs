﻿using System;
using System.Collections.Generic;
using System.IO;
using GitCommands;
using System.Text.RegularExpressions;

namespace PatchApply
{
    public class PatchManager
    {
        public List<Patch> patches = new List<Patch>();
        public string PatchFileName { get; set; }
        public string DirToPatch { get; set; }

        public string LoadFile(string fileName)
        {
            try
            {
                StreamReader re = new StreamReader(DirToPatch + fileName, Settings.Encoding);
                // string retval = re.ReadToEnd();
                // GetMD5Hash(retval);
                string retval = "";
                string line;
                while ((line = re.ReadLine()) != null)
                {
                    retval += line + "\n"; ;
                }
                re.Close();

                if (retval.Length > 0 && retval[retval.Length - 1] == '\n')
                    retval = retval.Remove(retval.Length - 1, 1);

                return retval;
            }
            catch
            {
            }
            return "";
        }

        public void SavePatch()
        {
            foreach (Patch patch in patches)
            {
                if (!patch.Apply)
                    continue;
                string path = DirToPatch + patch.FileNameA;
                if (patch.Type == Patch.PatchType.DeleteFile)
                {
                    File.Delete(path);
                }
                else
                {                    
                    Directory.CreateDirectory(path.Substring(0, path.LastIndexOfAny(((String)"\\/").ToCharArray())));
                    TextWriter tw = new StreamWriter(DirToPatch + patch.FileNameA, false);
                    tw.Write(patch.FileTextB);
                    tw.Close();
                }
            }
        }

        public string GetMD5Hash(string input)
        {
            byte[] bs = GetUTF8EncodedBytes(input);
            var s = new System.Text.StringBuilder();
            foreach (byte b in bs)
            {
                s.Append(b.ToString("x2").ToLower());
            }
            return s.ToString();
        }

        private byte[] GetUTF8EncodedBytes(string input)
        {
            var x = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] bs = System.Text.Encoding.UTF8.GetBytes(input);
            bs = x.ComputeHash(bs);
            return bs;
        }


        public void ApplyPatch(Patch patch)
        {
            patch.FileTextB = "";
            patch.Rate = 100;

            if (patch.Type == Patch.PatchType.DeleteFile)
            {
                handleDeletePatchType(patch);
                return;
            }

            if (patch.Text == null)
                return;

            string[] patchLines = patch.Text.Split('\n');

            if (patch.Type == Patch.PatchType.NewFile)
            {
                handleNewFilePatchType(patch, patchLines);
                return;
            }

            if (patch.Type == Patch.PatchType.ChangeFile)
            {
                handleChangeFilePatchType(patch, patchLines);
                return;
            }
        }

        private void handleChangeFilePatchType(Patch patch, string[] patchLines)
        {
            List<string> fileLines = new List<string>();
            foreach (string s in LoadFile(patch.FileNameA).Split('\n'))
            {
                fileLines.Add(s);
            }

            int lineNumber = 0;
            foreach (string line in patchLines)
            {
                //Parse fist line
                //@@ -1,4 +1,4 @@
                if (line.StartsWith("@@") && line.LastIndexOf("@@") > 0)
                {
                    string pos = line.Substring(3, line.LastIndexOf("@@") - 3).Trim();
                    string[] addrem = pos.Split('+', '-');
                    string[] oldLines = addrem[1].Split(',');
                    string[] newLines = addrem[2].Split(',');

                    lineNumber = Int32.Parse(oldLines[0]) - 1;

                    //line = line.Substring(line.LastIndexOf("@@") + 3));
                    continue;
                }

                if (line.StartsWith(" "))
                {
                    //Do some extra checks
                    if (line.Length > 0)
                    {
                        if (fileLines.Count > lineNumber && fileLines[lineNumber].CompareTo(line.Substring(1)) != 0)
                            patch.Rate -= 20;
                    }
                    else
                    {
                        if (fileLines.Count > lineNumber && fileLines[lineNumber] != "")
                            patch.Rate -= 20;
                    }

                    lineNumber++;
                }
                if (line.StartsWith("-"))
                {
                    if (line.Length > 0)
                    {
                        if (fileLines.Count > lineNumber && fileLines[lineNumber].CompareTo(line.Substring(1)) != 0)
                            patch.Rate -= 20;
                    }
                    else
                    {
                        if (fileLines.Count > lineNumber && fileLines[lineNumber] != "")
                            patch.Rate -= 20;
                    }

                    patch.BookMarks.Add(lineNumber);

                    if (fileLines.Count > lineNumber)
                        fileLines.RemoveAt(lineNumber);
                    else
                        patch.Rate -= 20;

                    //lineNumber++;
                }
                if (line.StartsWith("+"))
                {
                    string insertLine = "";
                    if (line.Length > 1)
                        insertLine = line.Substring(1);

                    //Is the patch allready applied?
                    if (fileLines.Count > lineNumber && fileLines[lineNumber].CompareTo(insertLine) == 0)
                    {
                        patch.Rate -= 20;
                    }

                    fileLines.Insert(lineNumber, insertLine);
                    patch.BookMarks.Add(lineNumber);

                    lineNumber++;
                }
            }
            foreach (string patchedLine in fileLines)
            {
                patch.FileTextB += patchedLine + "\n";
            }
            if (patch.FileTextB.Length > 0 && patch.FileTextB[patch.FileTextB.Length - 1] == '\n')
                patch.FileTextB = patch.FileTextB.Remove(patch.FileTextB.Length - 1, 1);

            if (patch.Rate != 100)
                patch.Apply = false;
        }

        private void handleNewFilePatchType(Patch patch, string[] patchLines)
        {
            foreach (string line in patchLines)
            {
                if (line.Length > 0 && line.StartsWith("+"))
                {
                    if (line.Length > 4 && line.StartsWith("+ï»¿"))
                        patch.AppendText(line.Substring(4));
                    else
                        if (line.Length > 1)
                            patch.FileTextB += line.Substring(1);

                    patch.FileTextB += "\n";
                }
            }
            if (patch.FileTextB.Length > 0 && patch.FileTextB[patch.FileTextB.Length - 1] == '\n')
                patch.FileTextB = patch.FileTextB.Remove(patch.FileTextB.Length - 1, 1);
            patch.Rate = 100;

            if (File.Exists(DirToPatch + patch.FileNameB))
            {
                patch.Rate -= 40;
                patch.Apply = false;
            }
        }

        private void handleDeletePatchType(Patch patch)
        {
            patch.FileTextB = "";
            patch.Rate = 100;

            if (!File.Exists(DirToPatch + patch.FileNameA))
            {
                patch.Rate -= 40;
                patch.Apply = false;
            }
        }

        public void LoadPatch(string text, bool applyPatch)
        {
            try
            {
                StringReader stream = new StringReader(text);
                LoadPatchStream(stream, applyPatch);
            }
            catch
            {
            }

        }

        public void LoadPatchFile(bool applyPatch)
        {
            try
            {
                StreamReader re = new StreamReader(PatchFileName, Settings.Encoding);
                LoadPatchStream(re, applyPatch);
            }
            catch
            {
            }
        }

        public void LoadPatchStream(TextReader reader, bool applyPatch)
        {
            patches = new List<Patch>();
            Patch patch = null;
           
            string input = reader.ReadLine();

            processInput(reader, input, patch);

            reader.Close();

            if (!applyPatch)
                return;

            foreach (Patch patchApply in patches)
            {
                if (patchApply.Apply)
                    ApplyPatch(patchApply);
            }
        }

        private void processInput(TextReader re, string input, Patch patch)
        {
            bool gitPatch = false;
            while (input != null)
            {
                //diff --git a/FileA b/FileB
                //new patch found
                if (input.StartsWith("diff --git "))
                {
                    gitPatch = true;
                    patch = new Patch();
                    patches.Add(patch);

                    Match match = Regex.Match(input, "[ ][\\\"]{0,1}[a]/(.*)[\\\"]{0,1}[ ][\\\"]{0,1}[b]/(.*)[\\\"]{0,1}");

                    patch.FileNameA = match.Groups[1].Value;
                    patch.FileNameB = match.Groups[2].Value;
                    //patch.FileNameA = input.Substring(input.LastIndexOf(" a/") + 3, input.LastIndexOf(" b/") - (input.LastIndexOf(" a/") + 3));
                    //patch.FileNameB = input.Substring(input.LastIndexOf(" b/") + 3);

                    //The next line tells us what kind of patch
                    //new file mode xxxxxx means new file
                    //delete file mode xxxxxx means delete file
                    //index means -> no new and no delete, edit
                    if ((input = re.ReadLine()) != null)
                    {
                        //WTF! No change
                        if (input.StartsWith("diff --git "))
                        {
                            //No change? lets continue to the next line
                            continue;
                        }

                        //new file!
                        if (input.StartsWith("new file mode "))
                            patch.Type = Patch.PatchType.NewFile;
                        else
                            if (input.StartsWith("deleted file mode "))
                                patch.Type = Patch.PatchType.DeleteFile;
                            else
                                patch.Type = Patch.PatchType.ChangeFile;

                        //we need to move to the line that says 'index'
                        //because we are not sure if we are there yet because
                        //we might point at the new or delete line lines
                        if (!input.StartsWith("index "))
                            if ((input = re.ReadLine()) == null)
                                break;
                    }

                    //The next lines tells us more about the change itself
                    //Read the next
                    if ((input = re.ReadLine()) != null)
                    {
                        //Binary files a/FileA and /dev/null differ
                        //means the file is deleted but the changes are not listed explicid
                        if (input.StartsWith("Binary files a/") && input.EndsWith(" and /dev/null differ"))
                        {
                            patch.File = Patch.FileType.Binary;

                            //Check if the type was set correctly
                            if (patch.Type != Patch.PatchType.DeleteFile)
                                throw new Exception("Change not parsed correct: " + input);

                            patch = null;

                            if ((input = re.ReadLine()) == null)
                                break;

                            //Continue loop, we do not get more info about this change
                            continue;
                        }

                        //Binary files a/FileA and /dev/null differ
                        //means the file is deleted but the changes are not listed explicid
                        if (input.StartsWith("Binary files /dev/null and b/") && input.EndsWith(" differ"))
                        {
                            patch.File = Patch.FileType.Binary;

                            //Check if the type was set correctly
                            if (patch.Type != Patch.PatchType.NewFile)
                                throw new Exception("Change not parsed correct: " + input);

                            //TODO: NOT SUPPORTED!
                            patch.Apply = false;

                            patch = null;

                            if ((input = re.ReadLine()) == null)
                                break;

                            continue;
                        }

                        //GIT binary patch
                        //means the file is binairy 
                        if (input.StartsWith("GIT binary patch"))
                        {
                            patch.File = Patch.FileType.Binary;

                            //TODO: NOT SUPPORTED!
                            patch.Apply = false;

                            patch = null;

                            if ((input = re.ReadLine()) == null)
                                break;

                            continue;
                        }
                    }

                    continue;
                }

                if (!gitPatch || gitPatch && patch != null)
                {
                    //The previous check checked only if the file was binary
                    //--- /dev/null
                    //means there is no old file, so this should be a new file
                    if (input.StartsWith("--- /dev/null"))
                    {
                        if (!gitPatch)
                        {
                            patch = new Patch();
                            patches.Add(patch);
                        }

                        if (gitPatch && patch.Type != Patch.PatchType.NewFile)
                            throw new Exception("Change not parsed correct: " + input);

                        //This line is parsed, NEXT!
                        if ((input = re.ReadLine()) == null)
                            break;

                    }

                    //line starts with --- means, old file name
                    if (input.StartsWith("--- a/") && !input.StartsWith("--- /dev/null"))
                    {
                        if (!gitPatch)
                        {
                            patch = new Patch();
                            patches.Add(patch);
                        }

                        if (gitPatch && patch.FileNameA != (input.Substring(6).Trim()))
                            throw new Exception("Old filename not parsed correct: " + input);

                        patch.FileNameA = (input.Substring(6).Trim());

                        //This line is parsed, NEXT!
                        if ((input = re.ReadLine()) == null)
                            break;

                    }

                    //If there is no 'newfile', reset files
                    if (input.StartsWith("+++ /dev/null"))
                    {
                        if (gitPatch && patch.Type != Patch.PatchType.DeleteFile)
                            throw new Exception("Change not parsed correct: " + input);

                        //This line is parsed, NEXT!
                        if ((input = re.ReadLine()) == null)
                            break;
                    }


                    //line starts with +++ means, new file name
                    //we expect a new file now!
                    if (input.StartsWith("+++ ") && !input.StartsWith("+++ /dev/null"))
                    {
                        Match regexMatch = Regex.Match(input, "[+]{3}[ ][\\\"]{0,1}[b]/(.*)[\\\"]{0,1}");

                        if (gitPatch && patch.FileNameB != (regexMatch.Groups[1].Value.Trim()))
                            throw new Exception("New filename not parsed correct: " + input);

                        patch.FileNameB = (regexMatch.Groups[1].Value.Trim());

                        //This line is parsed, NEXT!
                        if ((input = re.ReadLine()) == null)
                            break;
                    }
                }

                if (patch != null)
                    patch.AppendTextLine(input);

                if ((input = re.ReadLine()) == null)
                    break;
            }
        }

        /// <summary>
        /// Counts number of characters on all lines in file up to line number specified.
        /// Currently doesn't check if line > lines.Length.
        /// Probably not be including newline characters in the count.
        /// Not set up to handle DOS (CR LF) line endings.
        /// 
        /// Assumes file is a text file and that line < lines.Length
        /// </summary>
        /// <param name="file">file we want to contain lines from</param>
        /// <param name="line">line number we want to count up to</param>
        /// <returns></returns>
        public int LineToChar(string file, int line)
        {
            string[] lines = file.Split('\n');

            int retVal = 0;

            for (int n = 0; n < line; n++)
            {
                retVal += lines[n].Length;
            }

            return retVal;
        }
    }
}