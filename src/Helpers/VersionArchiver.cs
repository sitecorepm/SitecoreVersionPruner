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
    
    public class VersionArchiver
    {
        public string DatabaseName { get; set; }
        public string ArchiveName { get; set; }

        public VersionArchiver()
        {
            // Set defaults
            this.DatabaseName = "master";
            this.ArchiveName = "VersionPruner";
        }

        private Database _db = null;
        protected Database Database
        {
            get
            {
                if (_db == null)
                    _db = Factory.GetDatabase(this.DatabaseName);
                return _db;
            }

        }

        private SqlDataApi _sqlapi = null;
        protected SqlDataApi SqlApi
        {
            get
            {
                if (_sqlapi == null)
                {
                    return new SqlServerDataApi(Settings.GetConnectionString(this.Database.ConnectionStringName));
                }
                return _sqlapi;
            }
        }


        public void ArchiveItemVersions(Item[] versions)
        {
            var archivalId = this.GetArchivalId(versions[0]);
            this.DoArchiveItemVersions(versions, archivalId);
        }

        private Guid GetArchivalId(Item item)
        {
            Assert.ArgumentNotNull(item, "item");
            string sql = @"SELECT {0}ArchivalId{1} FROM {0}Archive{1} 
                           WHERE {0}ItemId{1} = {2}itemId{3} 
                                 AND {0}Name{1} = {2}name{3}";
            var reader = this.SqlApi.CreateReader(sql, new object[] { "itemId", item.ID, "name", this.ArchiveName });
            if (reader.Read())
                return this.SqlApi.GetGuid(0, reader);

            var archivalId = Guid.NewGuid();

            sql = @" INSERT INTO {0}Archive{1} ({0}ArchivalId{1}, {0}ItemId{1}, {0}ParentId{1}, {0}Name{1}, {0}OriginalLocation{1}, {0}ArchiveDate{1}, {0}ArchivedBy{1}, {0}ArchiveName{1})
                     VALUES ({2}archivalId{3}, {2}itemId{3}, {2}parentId{3}, {2}name{3}, {2}originalLocation{3}, {2}archiveDate{3}, {2}archivedBy{3}, {2}archiveName{3})";

            this.SqlApi.Execute(sql, new object[] { 
                "archivalId", archivalId, 
                "itemId", item.ID.ToGuid(), 
                "parentId", item.ParentID.ToGuid(), 
                "name", item.Name, 
                "originalLocation", item.Paths.Path, 
                "archiveDate", DateTime.UtcNow, 
                "archivedBy", Sitecore.Context.GetUserName(), 
                "archiveName", this.ArchiveName 
            });

            return archivalId;
        }
        private bool DoArchiveItemVersions(Item[] itemversions, Guid archivalId)
        {
            var itemAccessLock = this.Database.DataManager.DataEngine.ItemAccessLock;
            itemAccessLock.AcquireWriterLock(-1);
            try
            {
                using (DataProviderTransaction transaction = this.SqlApi.CreateTransaction())
                {
                    foreach (var iv in itemversions)
                    {
                        this.ArchiveVersion(iv, archivalId);
                    }
                    this.ArchiveItemData(itemversions[0], archivalId);
                    transaction.Complete();
                }
            }
            finally
            {
                itemAccessLock.ReleaseWriterLock();
            }
            return true;
        }

        private void ArchiveItemData(Item item, Guid archivalId)
        {
            string sql = "SELECT COUNT(*) FROM {0}ArchivedItems{1} WHERE {0}ItemId{1} = {2}itemId{3}";
            using (DataProviderReader reader = this.SqlApi.CreateReader(sql, new object[] { "itemId", item.ID }))
            {
                if (reader.Read() && (this.SqlApi.GetInt(0, reader) > 0))
                    return;
            }
            sql = @" INSERT INTO {0}ArchivedItems{1} ({0}RowId{1}, {0}ArchivalId{1}, {0}ItemId{1}, {0}Name{1}, {0}TemplateID{1}, {0}MasterID{1}, {0}ParentID{1}, {0}Created{1}, {0}Updated{1})
                     SELECT {2}new_id{3}, {2}archivalId{3}, {0}ID{1}, {0}Name{1}, {0}TemplateID{1}, {0}MasterID{1}, {0}ParentID{1}, {0}Created{1}, {0}Updated{1}
                     FROM {0}Items{1}
                     WHERE {0}ID{1} = {2}itemId{3}";

            this.SqlApi.Execute(sql, new object[] { 
                    "new_id", Guid.NewGuid(), 
                    "archivalId", archivalId, 
                    "itemId", item.ID.ToGuid() 
            });
        }
        private void ArchiveVersion(Item item, Guid archivalId)
        {
            Assert.ArgumentNotNull(item, "item");
            string sqlArchive = @"INSERT INTO {0}ArchivedFields{1} ({0}RowId{1}, {0}ArchivalId{1}, {0}SharingType{1}, {0}ItemId{1}, {0}Language{1}, {0}Version{1}, {0}FieldId{1}, {0}Value{1}, {0}Created{1}, {0}Updated{1})
                                  SELECT {0}Id{1}, {2}archivalId{3}, 'versioned', {0}ItemId{1}, {0}Language{1} , {0}Version{1}, {0}FieldId{1}, {0}Value{1}, {0}Created{1}, {0}Updated{1}
                                  FROM {0}VersionedFields{1}
                                  WHERE {0}ItemId{1} = {2}itemId{3} AND {0}Language{1} = {2}language{3} AND {0}Version{1} = {2}version{3}";
            
            this.SqlApi.Execute(sqlArchive, new object[] { 
                    "archivalId", archivalId, 
                    "language", item.Language.Name, 
                    "version", item.Version.Number, 
                    "itemId", item.ID });
        }

    }
}