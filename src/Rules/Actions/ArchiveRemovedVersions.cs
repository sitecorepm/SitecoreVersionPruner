using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sitecore.Rules;
using Sitecore.Rules.Actions;
using Sitecore.Diagnostics;

namespace Sitecore.SharedSource.VersionPruner.Rules.Actions
{
    public class ArchiveRemovedVersions<T> : SetRuleContextParameter<T> where T : RuleContext
    {
        public string ArchiveName { get; set; }

        public ArchiveRemovedVersions()
        {
            this.PropertyName = "ArchiveRemovedVersions";
            this.PropertyValue = true;
        }

        public override void Apply(T ruleContext)
        {
            base.Apply(ruleContext);
            ruleContext.Parameters["ArchiveName"] = this.ArchiveName;
        }
    }
}
