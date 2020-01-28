﻿using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;

/******************************************************
 * 
 * Copyright (c) 2020 MyFlightbook LLC
 * Contact myflightbook-at-gmail.com for more information
 *
*******************************************************/

namespace MyFlightbook.Web.Sharing
{
    [Flags]
    internal enum SharePrivs {
        None = 0x0000,
        Flights = 0x0001,
        Currency = 0x0002,
        Totals = 0x0004,
    };

    [Serializable]
    public class ShareKey
    {

        #region properties
        /// <summary>
        /// Unique ID (Guid) for this share key
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// Displayname for this share key
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Owner of the share key
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// When - if ever - was this most recently used?
        /// </summary>
        public DateTime? LastAccess { get; set; }

        public string LastAccessDisplay
        {
            get { return LastAccess == null || !LastAccess.HasValue ? Resources.LocalizedText.ShareKeyLastAccessNever : LastAccess.Value.ToShortDateString(); }
        }

        private string m_baseLink = "~/Public/ViewSharedLogbook.aspx";
        /// <summary>
        /// The base URL for the link.  By default is "~/Public/ViewSharedLogbook.aspx"
        /// </summary>
        public string BaseLink
        {
            get { return m_baseLink; }
            set { m_baseLink = value ?? string.Empty; }
        }

        public string ShareLink
        {
            get { return String.Format(CultureInfo.InvariantCulture, "https://{0}/{1}{2}g={3}", Branding.CurrentBrand.HostName, System.Web.VirtualPathUtility.ToAbsolute(m_baseLink), (m_baseLink.Contains("?") ? "&" : "?"), ID); }
        }

        private UInt32 privFlags { get; set; }

        public bool CanViewFlights
        {
            get { return (privFlags & (UInt32)SharePrivs.Flights) != 0; }
            set { privFlags = value ? privFlags | (UInt32)SharePrivs.Flights : privFlags & ~(UInt32)SharePrivs.Flights; }
        }

        public bool CanViewTotals
        {
            get { return (privFlags & (UInt32)SharePrivs.Totals) != 0; }
            set { privFlags = value ? privFlags | (UInt32)SharePrivs.Totals : privFlags & ~(UInt32)SharePrivs.Totals; }
        }

        public bool CanViewCurrency
        {
            get { return (privFlags & (UInt32)SharePrivs.Currency) != 0; }
            set { privFlags = value ? privFlags | (UInt32)SharePrivs.Currency : privFlags & ~(UInt32)SharePrivs.Currency; }
        }
        #endregion

        #region Constructors
        public ShareKey()
        {
            ID = Name = Username = string.Empty;
            privFlags = 0;
            LastAccess = null;
        }

        public ShareKey(string szUser) : this()
        {
            Username = szUser;
            ID = Guid.NewGuid().ToString();
        }

        private ShareKey(MySqlDataReader dr) : this()
        {
            ID = (string)dr["GUID"];
            Username = (string)dr["username"];
            Name = util.ReadNullableString(dr, "name");
            privFlags = Convert.ToUInt32(dr["privileges"], CultureInfo.InvariantCulture);
            object o = dr["lastaccess"];
            if (o == null || o == System.DBNull.Value || o.ToString().Length == 0)
                LastAccess = null;
            else
                LastAccess = Convert.ToDateTime(o, CultureInfo.InvariantCulture);
        }
        #endregion

        #region database
        public bool FCommit()
        {
            if (String.IsNullOrEmpty(ID))
                throw new InvalidOperationException("ShareKey must have an ID to be saved");
            if (privFlags == 0)
                throw new MyFlightbookValidationException(Resources.LocalizedText.ShareKeyValidationNoPrivileges);
            DBHelper dbh = new DBHelper("REPLACE INTO shareKeys SET GUID=?guid, Username=?user, Name=?name, Privileges=?privs");
            dbh.DoNonQuery((comm) =>
            {
                comm.Parameters.AddWithValue("guid", ID);
                comm.Parameters.AddWithValue("user", Username);
                comm.Parameters.AddWithValue("name", Name);
                comm.Parameters.AddWithValue("privs", privFlags);
            });
            return String.IsNullOrEmpty(dbh.LastError);
        }

        /// <summary>
        /// Updates the sharekey to indicate that the link has been used.
        /// </summary>
        /// <returns></returns>
        public bool FUpdateAccess()
        {
            DBHelper dbh = new DBHelper("UPDATE shareKeys SET lastAccess=NOW() WHERE GUID=?guid");
            dbh.DoNonQuery((comm) => { comm.Parameters.AddWithValue("guid", ID); });
            return String.IsNullOrEmpty(dbh.LastError);
        }

        public bool FDelete()
        {
            DBHelper dbh = new DBHelper("DELETE FROM shareKeys WHERE GUID=?guid AND Username=?user");
            dbh.DoNonQuery((comm) =>
            {
                comm.Parameters.AddWithValue("guid", ID);
                comm.Parameters.AddWithValue("user", Username);
            });
            return String.IsNullOrEmpty(dbh.LastError);
        }

        /// <summary>
        /// Returns a sharekey with the specified ID (guid).  CAN RETURN NULL IF NOT FOUND
        /// </summary>
        /// <param name="szID">The id of the sharekey to find</param>
        /// <returns>The sharekey, or null</returns>
        public static ShareKey ShareKeyWithID(string szID)
        {
            DBHelper dbh = new DBHelper("SELECT * FROM sharekeys WHERE GUID=?guid");
            ShareKey sk = null;
            dbh.ReadRow(
                (comm) => { comm.Parameters.AddWithValue("guid", szID); },
                (dr) => { sk = new ShareKey(dr); });
            return sk;
        }

        /// <summary>
        /// Returns a set of sharekeys owned by the user
        /// </summary>
        /// <param name="szUser">Name of the user for whom sharekeys are requested</param>
        /// <returns>IEnumerable of matching keys.</returns>
        public static IEnumerable<ShareKey> ShareKeysForUser(string szUser)
        {
            if (szUser == null)
                throw new ArgumentNullException(nameof(szUser));

            List<ShareKey> lst = new List<ShareKey>();

            DBHelper dbh = new DBHelper("SELECT * FROM sharekeys WHERE username=?user");
            dbh.ReadRows(
                (comm) => { comm.Parameters.AddWithValue("user", szUser); },
                (dr) => { lst.Add(new ShareKey(dr)); }
                );

            return lst;
        }
        #endregion
    }
}