using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sitecore.Rules;
using Sitecore.Rules.Actions;
using Sitecore.Diagnostics;

namespace Sitecore.SharedSource.VersionPruner.Rules.Actions
{
    public class ArchiveThisVersion<T> : SetRuleContextParameter<T> where T : RuleContext
    {
        public ArchiveThisVersion()
        {
            this.PropertyName = "ArchiveThisVersion";
            this.PropertyValue = true;
        }
    }
}
