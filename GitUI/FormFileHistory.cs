﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using GitCommands;
using PatchApply;

namespace GitUI
{
    public partial class FormFileHistory : GitExtensionsForm
    {
        public FormFileHistory(string fileName, GitRevision revision)
        {
            InitializeComponent();
            FileChanges.SetInitialRevision(revision);
            Translate();

            if (string.IsNullOrEmpty(fileName))
                return;

            LoadFileHistory(fileName);

            Diff.ExtraDiffArgumentsChanged += DiffExtraDiffArgumentsChanged;

            FileChanges.SelectionChanged += FileChangesSelectionChanged;
            FileChanges.DisableContextMenu();
        }

        public FormFileHistory(string fileName) : this(fileName, null)
        {
        }

        public string FileName { get; set; }

        private void LoadFileHistory(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return;

            //The section below contains native windows (kernel32) calls
            //and breaks on Linux. Only use it on Windows. Casing is only
            //a Windows problem anyway.
            if (Settings.RunningOnWindows())
            {
                // we will need this later to look up proper casing for the file
                string fullFilePath = fileName;

                if (!fileName.StartsWith(Settings.WorkingDir, StringComparison.InvariantCultureIgnoreCase))
                    fullFilePath = Path.Combine(Settings.WorkingDir, fileName);

                if (File.Exists(fullFilePath))
                {
                    // grab the 8.3 file path
                    StringBuilder shortPath = new StringBuilder(4096);
                    NativeMethods.GetShortPathName(fullFilePath, shortPath, shortPath.Capacity);

                    // use 8.3 file path to get properly cased full file path
                    StringBuilder longPath = new StringBuilder(4096);
                    NativeMethods.GetLongPathName(shortPath.ToString(), longPath, longPath.Capacity);

                    // remove the working dir and now we have a properly cased file name.
                    fileName = longPath.ToString().Substring(Settings.WorkingDir.Length);
                }
            }

            if (fileName.StartsWith(Settings.WorkingDir, StringComparison.InvariantCultureIgnoreCase))
                fileName = fileName.Substring(Settings.WorkingDir.Length);

            FileName = fileName;

            if (Settings.FollowRenamesInFileHistory)
                FileChanges.Filter = " --name-only --follow -- \"" + fileName + "\"";
            else
            {
                // --parents doesn't work with --follow enabled, but needed to graph a filtered log
                FileChanges.Filter = " --parents -- \"" + fileName + "\"";
                FileChanges.AllowGraphWithFilter = true;
            }
        }
 
        private void DiffExtraDiffArgumentsChanged(object sender, EventArgs e)
        {
            UpdateSelectedFileViewers();
        }

        private void FormFileHistoryFormClosing(object sender, FormClosingEventArgs e)
        {
            SavePosition("file-history");
        }

        private void FormFileHistoryLoad(object sender, EventArgs e)
        {
            RestorePosition("file-history");
            Text = string.Format("File History ({0})", FileName);
        }

        private void FileChangesSelectionChanged(object sender, EventArgs e)
        {
            View.SaveCurrentScrollPos();
            Diff.SaveCurrentScrollPos();
            UpdateSelectedFileViewers();
        }

        private void UpdateSelectedFileViewers()
        {
            var selectedRows = FileChanges.GetRevisions();

            if (selectedRows.Count == 0) return;

            IGitItem revision = selectedRows[0];

            var fileName = revision.Name;

            if (string.IsNullOrEmpty(fileName))
                fileName = FileName;

            Text = string.Format("File History ({0})", fileName);

            if (tabControl1.SelectedTab == Blame)
                blameControl1.LoadBlame(revision.Guid, fileName);
            if (tabControl1.SelectedTab == ViewTab)
            {
                var scrollpos = View.ScrollPos;

                View.ViewGitItemRevision(fileName, revision.Guid);
                View.ScrollPos = scrollpos;
            }

            switch (selectedRows.Count)
            {
                case 1:
                    {
                        IGitItem revision1 = selectedRows[0];

                        if (tabControl1.SelectedTab == DiffTab)
                        {
                            Diff.ViewPatch(
                                () =>
                                {
                                    Patch diff = GitCommands.GitCommands.GetSingleDiff(revision1.Guid, revision1.Guid + "^", fileName,
                                                                          Diff.GetExtraDiffArguments());
                                    if (diff == null)
                                        return string.Empty;
                                    return diff.Text;
                                }
                                );
                        }
                    }
                    break;
                case 2:
                    {
                        IGitItem revision1 = selectedRows[0];
                        IGitItem revision2 = selectedRows[1];

                        if (tabControl1.SelectedTab == DiffTab)
                        {
                            Diff.ViewPatch(
                                () =>
                                GitCommands.GitCommands.GetSingleDiff(revision1.Guid, revision2.Guid, fileName,
                                                                      Diff.GetExtraDiffArguments()).Text);
                        }
                    }
                    break;
                default:
                    Diff.ViewPatch("You need to select 2 files to view diff.");
                    break;
            }
        }


        private void TabControl1SelectedIndexChanged(object sender, EventArgs e)
        {
            FileChangesSelectionChanged(sender, e);
        }

        private void FileChangesDoubleClick(object sender, EventArgs e)
        {
            if (FileChanges.GetRevisions().Count == 0)
            {
                GitUICommands.Instance.StartCompareRevisionsDialog();
                return;
            }

            IGitItem revision = FileChanges.GetRevisions()[0];

            var form = new FormDiffSmall();
            form.SetRevision(revision.Guid);
            form.ShowDialog();
        }

        private void OpenWithDifftoolToolStripMenuItemClick(object sender, EventArgs e)
        {
            var selectedRows = FileChanges.GetRevisions();
            string rev1;
            string rev2;
            switch (selectedRows.Count)
            {
                case 1:
                    {
                        rev1 = selectedRows[0].Guid;
                        var parentGuids = selectedRows[0].ParentGuids;
                        if (parentGuids != null && parentGuids.Length > 0)
                        {
                            rev2 = parentGuids[0];
                        }
                        else
                        {
                            rev2 = rev1;
                        }
                    }
                    break;
                case 0:
                    return;
                default:
                    rev1 = selectedRows[0].Guid;
                    rev2 = selectedRows[1].Guid;
                    break;
            }

            var output = GitCommands.GitCommands.OpenWithDifftool(FileName, rev1, rev2);
            if (!string.IsNullOrEmpty(output))
                MessageBox.Show(output);
        }
    }
}