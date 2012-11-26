using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sitecore.Rules;
using Sitecore.Rules.Actions;
using Sitecore.Diagnostics;

namespace Sitecore.SharedSource.VersionPruner.Rules.Actions
{
    public class SerializeRemovedVersions<T> : SetRuleContextParameter<T> where T : RuleContext
    {
        public string RootFolder { get; set; }

        public SerializeRemovedVersions()
        {
            this.PropertyName = "SerializeRemovedVersions";
            this.PropertyValue = true;
        }

        public override void Apply(T ruleContext)
        {
            base.Apply(ruleContext);
            ruleContext.Parameters["SerializeRootFolder"] = this.RootFolder;
        }
    }
}
