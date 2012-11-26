using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sitecore.Rules;
using Sitecore.Rules.Actions;
using Sitecore.Diagnostics;

namespace Sitecore.SharedSource.VersionPruner.Rules.Actions
{
    public class MarkVersionForRemoval<T> : SetRuleContextParameter<T> where T : RuleContext
    {
        public MarkVersionForRemoval()
        {
            this.PropertyName = "MarkVersionForRemoval";
            this.PropertyValue = true;
        }
    }
}
