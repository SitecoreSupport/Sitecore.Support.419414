using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Pipelines;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace Sitecore.Support.Pipelines
{
    public class CatalogsPrefetchProcessor
    {
        private readonly ID _categoryTemplateID;

        private readonly ID _catalogTemplateID;

        private readonly List<ID> _catalogToPrefetchIds;

        private readonly Database _database;

        private readonly bool _mesureLoadTime;

        public CatalogsPrefetchProcessor(string categoryTemplateID, string catalogTemplateID, string catalogToPrefetchIds, string database, string mesureLoadTime)
        {
            _categoryTemplateID = ID.Parse(categoryTemplateID);
            _catalogTemplateID = ID.Parse(catalogTemplateID);
            _catalogToPrefetchIds = new List<ID>(catalogToPrefetchIds
                .Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => ID.Parse(x)));

            _database = Configuration.Factory.GetDatabase(database);
            _mesureLoadTime = bool.Parse(mesureLoadTime);
        }

        public void Process(PipelineArgs args)
        {
            string environmentRole;
            if (!IsCorrectPrefetchingEnvironment(out environmentRole))
            {
                Log.Warn($"[Sitecore.Support.419414] Catalogs prefetching isn't supported for '{environmentRole}' role", this);
                return;
            }

            if (_catalogTemplateID == ID.Null || _categoryTemplateID == ID.Null)
            {
                Log.Warn("[Sitecore.Support.419414] Couldn't resolve catalog or category template by ID", this);
                return;
            }

            for (int i = 0; i < _catalogToPrefetchIds.Count; i++)
            {
                var catalog = this._database.GetItem(_catalogToPrefetchIds[i]);
                if (catalog == null)
                {
                    Log.Warn($"[Sitecore.Support.419414] Couldn't load catalog with ID '{_catalogToPrefetchIds[i]}'", this);
                    continue;
                }

                DateTime now = DateTime.Now;
                this.RetreiveChildEntities(catalog);
                if (_mesureLoadTime)
                {
                    Log.Info($"[Sitecore.Support.419414] total load time for all categories in '{catalog.Name}': {(DateTime.Now - now).TotalSeconds} seconds", this);
                }
            }
        }

        private void RetreiveChildEntities(Item parent)
        {
            if (!parent.Template.ID.Equals(_categoryTemplateID) && !parent.Template.ID.Equals(_catalogTemplateID))
            {
                return;
            }

            DateTime now = DateTime.Now;
            var childItems = parent.GetChildren(Collections.ChildListOptions.SkipSorting);
            if (_mesureLoadTime)
            {
                Log.Info($"[Sitecore.Support.419414]: child items of '{parent.Name}' loaded in {(DateTime.Now - now).TotalSeconds} seconds", this);
            }

            for (int i = 0; i < childItems.Count(); i++)
            {
                this.RetreiveChildEntities(childItems[i]);
            }
        }

        private bool IsCorrectPrefetchingEnvironment(out string environmentRole)
        {
            environmentRole = ConfigurationManager.AppSettings["role:define"];
            if (!string.IsNullOrEmpty(environmentRole))
            {
                return environmentRole.Equals(SitecoreRole.ContentDelivery.Name, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
    }
}