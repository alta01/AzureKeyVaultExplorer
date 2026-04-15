namespace Microsoft.Vault.Explorer.Dialogs.Subscriptions
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using Microsoft.Vault.Explorer.Common;
    using Microsoft.Vault.Explorer.Model.Collections;

    public class ObservableAccessPoliciesCollection : ObservableCustomCollection<AccessPolicyEntryItem>
    {
        public ObservableAccessPoliciesCollection()
        {
        }

        public ObservableAccessPoliciesCollection(IEnumerable<AccessPolicyEntryItem> collection) : base(collection)
        {
        }

        protected override PropertyDescriptor GetPropertyDescriptor(AccessPolicyEntryItem item) =>
            new ReadOnlyPropertyDescriptor($"[{item.Index}]", item);
    }
}