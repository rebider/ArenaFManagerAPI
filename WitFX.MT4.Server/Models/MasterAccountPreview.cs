using System;

namespace WitFX.MT4.Server.Models
{
    public sealed class MasterAccountPreview
    {
        public int MasterLogin;
        public bool IsEnable;
        public DateTimeOffset RegDateTime;
        public DateTimeOffset ExpDateTime;
        public string LoginAlias;
        public string Email;
        public string PasswordHash;

        public bool IsValid {
            get {
                //we can also check expiry data //for now it not needed
                return IsEnable; //only checking enable flag
            }
        }
    }
}
