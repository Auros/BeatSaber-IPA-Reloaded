﻿using IPA.Logging;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;

namespace IPA.Updating.Backup
{
    /// <summary>
    /// A unit for backup. WIP.
    /// </summary>
    internal class BackupUnit
    {
        public string Name { get; private set; }

        private DirectoryInfo _BackupPath;
        private List<string> _Files = new List<string>();
        private FileInfo _ManifestFile;
        private static string _ManifestFileName = "$manifest$.txt";

        public BackupUnit(string path) : this(path, DateTime.Now.ToString("yyyy-MM-dd_h-mm-ss"))
        {
        }

        internal BackupUnit(string path, string name)
        {
            Name = name;
            _BackupPath = new DirectoryInfo(Path.Combine(path, Name));
            _ManifestFile = new FileInfo(Path.Combine(_BackupPath.FullName, _ManifestFileName));
        }

        public static BackupUnit FromDirectory(DirectoryInfo directory, string backupPath)
        {
            var unit = new BackupUnit(backupPath, directory.Name);

            // Read Manifest
            if (unit._ManifestFile.Exists)
            {
                string manifest = File.ReadAllText(unit._ManifestFile.FullName);
                foreach (var line in manifest.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
                    unit._Files.Add(line);
            }
            else
            {
                foreach (var file in directory.GetFiles("*", SearchOption.AllDirectories))
                {
                    if (file.Name == _ManifestFileName) continue;
                    var relativePath = file.FullName.Substring(directory.FullName.Length + 1);
                    unit._Files.Add(relativePath);
                }
            }

            return unit;
        }

        public void Add(string file)
        {
            Add(new FileInfo(file));
        }

        internal void Delete()
        {
            if (_BackupPath.Exists)
                _BackupPath.Delete(true);
        }

        /// <summary>
        /// Adds a file to the list of changed files and backups it.
        /// </summary>
        /// <param name="file"></param>
        public void Add(FileInfo file)
        {
            var relativePath = LoneFunctions.GetRelativePath(file.FullName, Environment.CurrentDirectory);
            var backupPath = new FileInfo(Path.Combine(_BackupPath.FullName, relativePath));

            Logger.updater.Debug($"rp={relativePath}, bp={backupPath}");

            if (_Files.Contains(relativePath))
            {
                Logger.updater.Debug($"Skipping backup of {relativePath}");
                return;
            }

            // Copy over
            backupPath.Directory.Create();
            if (file.Exists)
            {
                file.CopyTo(backupPath.FullName);
            }
            else
            {
                // Make empty file
                backupPath.Create().Close();
            }

            if (!File.Exists(_ManifestFile.FullName))
                _ManifestFile.Create().Close();
            var stream = _ManifestFile.AppendText();
            stream.WriteLine(relativePath);
            stream.Close();

            // Add to list
            _Files.Add(relativePath);
        }

        /// <summary>
        /// Reverts the changes made in this unit.
        /// </summary>
        public void Restore()
        {
            foreach (var relativePath in _Files)
            {
                Logger.updater.Debug($"Restoring {relativePath}");
                // Original version
                var backupFile = new FileInfo(Path.Combine(_BackupPath.FullName, relativePath));
                var target = new FileInfo(Path.Combine(Environment.CurrentDirectory, relativePath));

                if (backupFile.Exists)
                {
                    if (backupFile.Length > 0)
                    {
                        Logger.updater.Debug($"  {backupFile.FullName} => {target.FullName}");
                        target.Directory.Create();
                        backupFile.CopyTo(target.FullName, true);
                    }
                    else
                    {
                        Logger.updater.Debug($"  x {target.FullName}");
                        if (target.Exists)
                        {
                            target.Delete();
                        }
                    }
                }
                else
                {
                    Logger.updater.Error("Backup not found!");
                }
            }
        }


    }
}
