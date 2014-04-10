using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sitecore.Rules;
using Sitecore.Rules.Actions;
using Sitecore.Diagnostics;

namespace Sitecore.SharedSource.VersionPruner.Rules.Actions
{
    public class SerializeThisVersion<T> : SetRuleContextParameter<T> where T : RuleContext
    {
        public string RootFolder { get; set; }

        public SerializeThisVersion()
        {
            this.PropertyName = "SerializeThisVersion";
            this.PropertyValue = true;
        }

        public override void Apply(T ruleContext)
        {
            base.Apply(ruleContext);
            ruleContext.Parameters["SerializeRootFolder"] = this.RootFolder;
        }
    }
}
