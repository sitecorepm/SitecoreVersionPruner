using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sitecore.Data.Items;
using Sitecore.Tasks;
using Sitecore.Data;
using Sitecore.Configuration;
using Sitecore.Rules;
using Sitecore.Diagnostics;
using Sitecore.SharedSource.VersionPruner.Helpers;

namespace Sitecore.SharedSource.VersionPruner.Tasks
{
    public class VersionPrunerCommand
    {
        private static object _sync = new object();
        private static bool _IsRunning = false;

        protected CommandItem CommandItem { get; set; }
        protected bool DisableIndexing { get { return this.CommandItem["Disable Indexing While Processing"] == "1"; } }
        protected bool SerializeItemsEnabled { get; set; }
        protected bool ArchiveVersionsEnabled { get; set; }
        protected string SerializeRootFolder { get; set; }
        protected string ArchiveName { get; set; }

        private Database _Database = null;
        protected Database Database
        {
            get
            {
                if (_Database == null)
                    _Database = string.IsNullOrEmpty(this.CommandItem["Database"]) ? Factory.GetDatabase("master") : Factory.GetDatabase(this.CommandItem["Database"]);
                return _Database;
            }
        }

        private Item _RootItem = null;
        protected Item RootItem
        {
            get
            {
                if (_RootItem == null)
                    _RootItem = this.Database.GetItem(this.CommandItem["Root"]);
                return _RootItem;
            }
        }

        private RuleList<RuleContext> _ItemFilterRules = null;
        protected RuleList<RuleContext> ItemFilterRules
        {
            get
            {
                if (_ItemFilterRules == null)
                {
                    _ItemFilterRules = RuleFactory.ParseRules<RuleContext>(this.Database, this.CommandItem["Item Filter"]);
                    _ItemFilterRules.Name = this.CommandItem.InnerItem.Paths.Path + " - Item Filter";
                }
                return _ItemFilterRules;
            }
        }

        private RuleList<RuleContext> _VersionFilterRules = null;
        protected RuleList<RuleContext> VersionFilterRules
        {
            get
            {
                if (_VersionFilterRules == null)
                {
                    _VersionFilterRules = RuleFactory.ParseRules<RuleContext>(this.Database, this.CommandItem["Version Filter"]);
                    _VersionFilterRules.Name = this.CommandItem.InnerItem.Paths.Path + " - Version Filter";
                }
                return _VersionFilterRules;
            }
        }

        private VersionSerializer _Serializer = null;
        protected VersionSerializer Serializer
        {
            get
            {
                if (_Serializer == null)
                    _Serializer = new VersionSerializer()
                    {
                        SerializationFolder = this.SerializeRootFolder
                    };
                return _Serializer;
            }
        }
        private VersionArchiver _Archiver = null;
        protected VersionArchiver Archiver
        {
            get
            {
                if (_Archiver == null)
                    _Archiver = new VersionArchiver()
                    {
                        DatabaseName = this.Database.Name,
                        ArchiveName = this.ArchiveName
                    };
                return _Archiver;
            }
        }


        public void Run(Item[] items, CommandItem command, ScheduleItem schedule)
        {
            lock (_sync)
            {
                if (_IsRunning)
                    return;


                _IsRunning = true;
                this.CommandItem = command;
            }

            var disableIndexing = this.DisableIndexing && Sitecore.Configuration.Settings.Indexing.Enabled;

            try
            {
                // Get the root
                var root = this.RootItem;
                Assert.ArgumentNotNull(root, "RootItem");

                if (disableIndexing)
                {
                    Log.Info("Temporarily disable indexing...", this);
                    Sitecore.Configuration.Settings.Indexing.Enabled = false;
                }
                ProcessItemTree(root);
            }
            catch (Exception ex)
            {
                Log.Error("VersionPruner exception", ex, this);
                throw;
            }
            finally
            {
                _IsRunning = false;
                if (disableIndexing)
                {
                    Sitecore.Diagnostics.Log.Info("Re-enabled indexing...", this);
                    Sitecore.Configuration.Settings.Indexing.Enabled = true;
                }
            }
        }

        protected virtual void ProcessItemTree(Item item)
        {
            if (Sitecore.Context.Job != null)
            {
                Sitecore.Context.Job.Status.Processed++;
                Sitecore.Context.Job.Status.Messages.Add("processing: " + item.Paths.Path);
            }

            // Run item against the Item Filter rule(s)
            var ruleContext = new RuleContext();
            ruleContext.Item = item;
            this.ItemFilterRules.Run(ruleContext);

            if (ruleContext.Parameters.ContainsKey("ItemValidForVersionRemoval"))
            {
                if (ruleContext.Parameters.ContainsKey("ArchiveRemovedVersions"))
                {
                    this.ArchiveVersionsEnabled = true;
                    this.ArchiveName = ruleContext.Parameters["ArchiveName"] as string;
                }

                if (ruleContext.Parameters.ContainsKey("SerializeRemovedVersions"))
                {
                    this.SerializeItemsEnabled = true;
                    this.SerializeRootFolder = ruleContext.Parameters["SerializeRootFolder"] as string;
                }

                // Rule was passed, so this item's versions should be trimmed..
                TrimItemVersions(item);
            }

            // process all descendant items..
            foreach (var child in item.Children.InnerChildren)
                ProcessItemTree(child);
        }

        protected virtual void TrimItemVersions(Item item)
        {
            var deleteMe = new List<Item>();

            // set the latest valid version for this item
            var latestValidVersion = item.Publishing.GetValidVersion(DateTime.Now, true, false);
            if (latestValidVersion == null)
            {
                Log.Warn(string.Format("Item does not have a published version. This item will NOT be pruned. [{0}]", item.Paths.Path), this);
                return;
            }

            // Get an array of all possible versions that can be removed
            var versions = item.Versions.GetVersions()
                                    .Where(x => x.Version.Number < latestValidVersion.Version.Number)
                                    .OrderBy(x => x.Version.Number)
                                    .ToArray();

            // Process each item version against the Version Filter rules
            foreach (var v in versions)
            {
                var ruleContext = new RuleContext();
                ruleContext.Parameters["currentversion"] = latestValidVersion;
                ruleContext.Item = v;
                this.VersionFilterRules.Run(ruleContext);

                if (ruleContext.Parameters.ContainsKey("MarkVersionForRemoval"))
                {
                    // Passed all rules. Add to "deleteMe" list
                    deleteMe.Add(v);
                }
            }

            if (deleteMe.Count > 0)
            {
                if (SerializeItemsEnabled)
                {
                    // Serialize versions..
                    this.Serializer.SerializeItemVersions(item, deleteMe.Select(x => x.Version.Number).ToArray());
                }

                if (ArchiveVersionsEnabled)
                {
                    // Copy the to-be-deleted item versions to the Archive database..
                    this.Archiver.ArchiveItemVersions(deleteMe.ToArray());
                }

                foreach (var v in deleteMe)
                {
                    var msg = String.Format("Remove version. [{0}][{1}][vers# {2}]",
                                      item.Language.Name,
                                      item.Paths.FullPath,
                                      v.Version.Number);
                    Log.Audit(msg, this);
                    v.Versions.RemoveVersion();
                }
            }
        }
    }
}
