// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Dialogs.Certificates
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Azure.Security.KeyVault.Certificates;
    using Microsoft.Vault.Explorer.Common;
    using Microsoft.Vault.Explorer.Controls.MenuItems;
    using Microsoft.Vault.Explorer.Dialogs.Passwords;
    using Microsoft.Vault.Explorer.Model;
    using Microsoft.Vault.Explorer.Model.ContentTypes;
    using Microsoft.Vault.Explorer.Model.Files.Secrets;
    using Microsoft.Vault.Explorer.Model.PropObjects;

    public partial class CertificateDialog : ItemDialogBase //<PropertyObjectCertificate, CertificateBundle>
    {
        private CertificatePolicy _certificatePolicy; // There is one policy and multiple versions of kv certificate. A policy is a recipe to create a next version of the kv certificate.

        private CertificateDialog(ISession session, string title, ItemDialogBaseMode mode) : base(session, title, mode)
        {
            this.InitializeComponent();
        }

        /// <summary>
        ///     New certificate from file
        /// </summary>
        public CertificateDialog(ISession session, FileInfo fi) : this(session, "New certificate", ItemDialogBaseMode.New)
        {
            KeyVaultCertificate cb = null;
            X509Certificate2 cert = null;
            ContentType contentType = ContentTypeUtils.FromExtension(fi.Extension);
            switch (contentType)
            {
                case ContentType.Certificate:
                    cert = X509CertificateLoader.LoadCertificateFromFile(fi.FullName);
                    break;
                case ContentType.Pkcs12:
                    string password = null;
                    var pwdDlg = new PasswordDialog();
                    if (pwdDlg.ShowDialog() != DialogResult.OK)
                    {
                        this.DialogResult = DialogResult.Cancel;
                        return;
                    }

                    password = pwdDlg.Password;
                    cert = X509CertificateLoader.LoadPkcs12FromFile(fi.FullName, password, X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable, Pkcs12LoaderLimits.Defaults);
                    break;
                case ContentType.KeyVaultCertificate:
                    var kvcf = Utils.LoadFromJsonFile<KeyVaultCertificateFile>(fi.FullName);
                    var cfd = kvcf.Deserialize();
                    cert = X509CertificateLoader.LoadCertificate(cfd.Cer);
                    break;
                default:
                    throw new ArgumentException($"Unsupported ContentType {contentType}");
            }

            this.NewCertificate(cb, cert);
        }

        /// <summary>
        ///     New certificate from X509Certificate2
        /// </summary>
        public CertificateDialog(ISession session, X509Certificate2 cert) : this(session, "New certificate", ItemDialogBaseMode.New)
        {
            this.NewCertificate(null, cert);
        }

        /// <summary>
        ///     Edit certificate
        /// </summary>
        public CertificateDialog(ISession session, string name, IEnumerable<CertificateProperties> versions) : this(session, $"Edit certificate {name}", ItemDialogBaseMode.Edit)
        {
            this.uxTextBoxName.ReadOnly = true;
            int i = 0;
            this.uxMenuVersions.Items.AddRange((from v in versions orderby v.CreatedOn descending select new CertificateVersion(i++, v)).ToArray());
            this.uxMenuVersions_ItemClicked(null, new ToolStripItemClickedEventArgs(this.uxMenuVersions.Items[0])); // Pass sender as NULL so _changed will be set to false
        }

        private void NewCertificate(KeyVaultCertificate cb, X509Certificate2 cert)
        {
            this._certificatePolicy = this._certificatePolicy ?? new CertificatePolicy("Self")
            {
                Exportable = true,
                KeySize = 2048,
                KeyType = "RSA",
                ReuseKey = false,
                ContentType = CertificateContentType.Pkcs12,
            };
            this.RefreshCertificateObject(cb, this._certificatePolicy, cert);
            this.uxTextBoxName.Text = Utils.ConvertToValidSecretName(cert.GetNameInfo(X509NameType.SimpleName, false));
        }

        private void RefreshCertificateObject(KeyVaultCertificate cb, CertificatePolicy cp, X509Certificate2 certificate)
        {
            this.uxPropertyGridSecret.SelectedObject = this.PropertyObject = new PropertyObjectCertificate(cb, cp, certificate, this.SecretObject_PropertyChanged);
            this.uxTextBoxName.Text = this.PropertyObject.Name;
            this.uxToolTip.SetToolTip(this.uxLinkLabelSecretKind, this.PropertyObject.SecretKind.Description);
        }

        protected override void uxLinkLabelSecretKind_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var obj = (PropertyObjectCertificate)this.PropertyObject;
            X509Certificate2UI.DisplayCertificate(obj.Certificate);
        }

        private void SecretObject_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            this._changed = true;
            this.InvalidateOkButton();
        }

        protected override async Task<object> OnVersionChangeAsync(CustomVersion cv)
        {
            var cb = await this._session.CurrentVault.GetCertificateAsync(cv.Id.Name, cv.Index == 0 ? null : cv.Id.Version);
            var cert = await this._session.CurrentVault.GetCertificateWithExportableKeysAsync(cv.Id.Name, cv.Id.Version);
            if (this._certificatePolicy == null && cv.Index == 0) // Only fetch policy for current version
            {
                this._certificatePolicy = await this._session.CurrentVault.GetCertificatePolicyAsync(cv.Id.Name);
            }

            this.RefreshCertificateObject(cb, this._certificatePolicy, cert);
            return cb;
        }
    }
}
