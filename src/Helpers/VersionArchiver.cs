using System;
using System.Linq;
using Sitecore.Configuration;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Data.Items;
using Sitecore.Data.Serialization;
using System.Text;
using Sitecore.Data.DataProviders.Sql;
using Sitecore.Data.SqlServer;
using Sitecore.Data;
using Sitecore.Data.Archiving;
using Sitecore.Tasks;

namespace Sitecore.SharedSource.VersionPruner.Helpers
{

    public class VersionArchiver
    {
        public Database Database { get; set; }

        private int[] _SitecoreVersion = null;
        private int[] SitecoreVersion
        {
            get
            {
                if (_SitecoreVersion == null)
                    _SitecoreVersion = Sitecore.Configuration.About.GetVersionNumber(false).Split('.').Select(x =>
                    {
                        int i = 0;
                        if (!int.TryParse(x, out i))
                            i = 0;
                        return i;
                    }).ToArray();
                return _SitecoreVersion;
            }
        }


        public void ArchiveItemVersions(Item[] versions)
        {
            if (versions.Length > 0)
            {
                var major = SitecoreVersion[0];
                var minor = SitecoreVersion[1];

                if (major <= 6 && minor < 6)
                {
                    throw new Exception("Saving pruned item versions to the archive was a BETA feature (for Sitecore pre-6.6) that has been removed. If you need it back, use 'Version Pruner v1.2'");
                }
                else
                {
                    foreach (var v in versions)
                    {
                        var task = new ArchiveVersion(DateTime.Now)
                        {
                            ItemID = v.ID,
                            DatabaseName = v.Database.Name,
                            //By = "VersionPruner",
                            Language = v.Language.Name,
                            Version = v.Version.Number,
                            ArchiveName = "archive"
                        };
                        task.Execute();
                    }
                }
            }
        }

    }
}