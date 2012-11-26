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

namespace Sitecore.SharedSource.VersionPruner.Helpers
{
    /// <summary>
    /// Started with code from these sources: 
    ///     http://www.sitecore.net/Community/Technical-Blogs/John-West-Sitecore-Blog/Posts/2011/06/Remove-Old-Versions-of-Items-in-the-Sitecore-ASPNET-CMS.aspx
    ///     http://trac.sitecore.net/VersionManager
    /// </summary>
    public class VersionSerializer
    {
        public string SerializationFolder { get; set; }

        public VersionSerializer()
        {
            this.SerializationFolder = "VersionPruner";
        }

        /// <summary>
        /// Really this serializes the whole item (all versions). The file name contains the meta-data 
        /// of which versions we'll be removing and thus can be retrieved from this serialized file
        /// should the need arise.
        /// </summary>
        public void SerializeItemVersions(Item item, int[] versions)
        {
            Assert.ArgumentNotNull(item, "item");
            var now = DateTime.Now;

            var path = new StringBuilder(this.SerializationFolder + "/");
            path.Append(now.Year + "/");
            path.Append(now.Month + "/");
            path.Append(now.Day + "/");
            path.Append(item.Name + "_" + item.ID.Guid.ToString() + "/");
            path.Append("Versions_" + VersionArrayToString(versions.OrderBy(x => x).ToArray()));

            Log.Info(string.Format("Serializing {0} --> {1}", item.Paths.Path, path.ToString()), this);
            Manager.DumpItem(PathUtils.GetFilePath(path.ToString()), item);
        }

        /// <summary>
        /// Produces a string representation of the version number array that is supplied.
        ///     {1, 2, 3, 4, 5, 7, 8, 9, 13} ==> "1-5_7-9_13"
        /// </summary>
        private static string VersionArrayToString(int[] versions)
        {
            if (versions.Length == 0)
                return string.Empty;

            var ordered = versions.OrderBy(x => x).ToArray();
            var previous = ordered.First();
            var rangecount = 0;

            var output = new StringBuilder();

            for (var i = 0; i < ordered.Length; i++)
            {
                var current = ordered[i];
                if (current - previous > 1)
                {
                    if (rangecount > 1)
                    {
                        output.Append("-").Append(previous);
                        rangecount = 1;
                    }
                    output.Append("_").Append(current);
                }
                else if (i == 0 || i == ordered.Length - 1)
                {
                    if (i > 0 && i == ordered.Length - 1)
                        output.Append("-");
                    output.Append(current);
                    rangecount++;
                }
                else
                    rangecount++;
                previous = current;
            }
            return output.ToString();
        }
    }
}