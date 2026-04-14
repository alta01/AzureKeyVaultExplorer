namespace Microsoft.Vault.Explorer.Model.Collections
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using Microsoft.Vault.Explorer.Common;

    public class ObservableLifetimeActionsCollection : ObservableCustomCollection<LifetimeActionItem>
    {
        public ObservableLifetimeActionsCollection()
        {
        }

        public ObservableLifetimeActionsCollection(IEnumerable<LifetimeActionItem> collection) : base(collection)
        {
        }

        protected override PropertyDescriptor GetPropertyDescriptor(LifetimeActionItem item) =>
            new ReadOnlyPropertyDescriptor(item.ToString(), $"DaysBeforeExpiry={Utils.NullableIntToString(item.DaysBeforeExpiry)}, LifetimePercentage={Utils.NullableIntToString(item.LifetimePercentage)}");
    }
}