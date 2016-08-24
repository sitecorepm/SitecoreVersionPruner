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
                        SerializationFolder = this.CommandItem["Serialization Root Folder"]
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
                        Database = this.Database
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

                Log.Info(string.Format("Start prune search from root item: {0}", root.Paths.Path), this);
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
                // Rule was passed, so this item's versions should be trimmed..
                TrimItemVersions(item);
            }

            // process all descendant items..
            foreach (var child in item.Children.InnerChildren)
                ProcessItemTree(child);
        }

        protected virtual void TrimItemVersions(Item item)
        {
            var pruneMe = new List<PruneAction>();

            // Get the latest valid version for this item
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

            Log.Debug(string.Format("[{0}][latest published version #: {1}][# of pruning candidates: {2}]", item.Paths.Path, latestValidVersion.Version.Number, versions.Length), this);


            // Process each item version against the Version Filter rules
            foreach (var v in versions)
            {
                var ruleContext = new RuleContext();
                ruleContext.Parameters["currentversion"] = latestValidVersion;
                ruleContext.Item = v;
                this.VersionFilterRules.Run(ruleContext);


                var a = new PruneAction
                {
                    ItemVersion = v
                };

                a.Archive = ruleContext.Parameters.ContainsKey("ArchiveThisVersion");
                a.Serialize = ruleContext.Parameters.ContainsKey("SerializeThisVersion");

                if (a.Archive || a.Serialize)
                {
                    // Passed all rules. Add to "deleteMe" list
                    pruneMe.Add(a);
                }
            }

            if (pruneMe.Count > 0)
            {
                if (pruneMe.Any(x => x.Serialize))
                {
                    // Serialize versions..
                    var serializeMe = pruneMe.Where(x => x.Serialize);
                    this.Serializer.SerializeItemVersions(item, serializeMe.Select(x => x.ItemVersion.Version.Number).ToArray());
                }

                if (pruneMe.Any(x => x.Archive))
                {
                    // Copy the to-be-deleted item versions to the Archive database..
                    this.Archiver.ArchiveItemVersions(pruneMe.Where(x => x.Archive).Select(x => x.ItemVersion).ToArray());
                }

                if (pruneMe.Any(x => !x.Archive))
                {
                    foreach (var v in pruneMe.Where(x => !x.Archive))
                    {
                        var msg = String.Format("Delete version: [{0}][{1}][vers# {2}]",
                                          item.Language.Name,
                                          item.Paths.FullPath,
                                          v.ItemVersion.Version.Number);
                        Log.Audit(msg, this);
                        v.ItemVersion.Versions.RemoveVersion();
                    }
                }
            }
        }
    }

    class PruneAction
    {
        public Item ItemVersion { get; set; }
        public bool Archive { get; set; }
        public bool Serialize { get; set; }
    }
}
