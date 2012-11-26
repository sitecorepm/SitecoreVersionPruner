using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sitecore.Rules;
using Sitecore.Rules.Actions;
using Sitecore.Diagnostics;

namespace Sitecore.SharedSource.VersionPruner.Rules.Actions
{
    public class ItemValidForVersionRemoval<T> : SetRuleContextParameter<T> where T : RuleContext
    {
        public ItemValidForVersionRemoval()
        {
            this.PropertyName = "ItemValidForVersionRemoval";
            this.PropertyValue = true;
        }
    }
}
