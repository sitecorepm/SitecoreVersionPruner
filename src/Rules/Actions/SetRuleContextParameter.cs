using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sitecore.Rules;
using Sitecore.Rules.Actions;
using Sitecore.Diagnostics;

namespace Sitecore.SharedSource.VersionPruner.Rules.Actions
{
    public class SetRuleContextParameter<T> : RuleAction<T> where T : RuleContext
    {
        public string PropertyName { get; set; }
        public object PropertyValue { get; set; }

        public override void Apply(T ruleContext)
        {
            Assert.ArgumentNotNull(ruleContext, "ruleContext");
            ruleContext.Parameters[this.PropertyName] = this.PropertyValue;
        }
    }
}
