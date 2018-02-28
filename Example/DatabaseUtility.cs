using System.Data.SqlClient;
using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Data.Linq;
using System.Linq;
using System.Data.Linq.Mapping;
using System.Net;
using System.IO;
using System.Data;
using System.Windows;

namespace TapTrack.Demo
{
	public class DatabaseUtility
	{
		private static DataContext dc = new DataContext(Properties.Settings.Default.CheckinDatabaseConnectionString);

		public static bool InsertCheckinRecord(List<CheckinRecord> records)
		{
			Table<CheckinRecord> checkinRecords = dc.GetTable<CheckinRecord>();

			foreach (CheckinRecord r in records)
			{
				checkinRecords.InsertOnSubmit(r);
			}

			dc.SubmitChanges();

			return true;
		
		}

		public static List<CheckinRecord> SelectDownloadedCheckins()
		{		

			Table<CheckinRecord> checkinRecords = dc.GetTable<CheckinRecord>();

			List<CheckinRecord> l = (from c in checkinRecords orderby c.timestamp descending select c).ToList();

			return l;
		}


		public static List<string> SelectDownloadedCheckins_TagCode()
		{

			Table<CheckinRecord> checkinRecords = dc.GetTable<CheckinRecord>();


			List<string> l = (from c in checkinRecords select c.tagCode).ToList();

			return l;
		}

		public static List<TagCodeCount> SelectTagCodeCount()
		{

			Table<CheckinRecord> checkinRecords = dc.GetTable<CheckinRecord>();

			//Dictionary<string, int> l = (from r in checkinRecords orderby r.tagCode group r by r.tagCode into grp select new { key = grp.Key, cnt = grp.Count() }).To
			var l = (from r in checkinRecords orderby r.tagCode group r by r.tagCode into grp select new { tagcode = grp.Key, cnt = grp.Count() }).ToList();			
			
			List<TagCodeCount>  list= new List<TagCodeCount>();
			TagCodeCount tagCodeCout;
			foreach (var v in l)
			{
				tagCodeCout = new TagCodeCount(v.tagcode, v.cnt);
				list.Add(tagCodeCout);
			}
			return list;
		}

		public static bool InsertCheckinTag(CheckinTag tag)
		{
			Table<CheckinTag> checkinTags = dc.GetTable<CheckinTag>();

			checkinTags.InsertOnSubmit(tag);			

			dc.SubmitChanges();

			return true;

		}

		public static List<CheckinTag> SelectCheckinTags()
		{

			Table<CheckinTag> checkinTags = dc.GetTable<CheckinTag>();

			List<CheckinTag> l = (from c in checkinTags orderby c.StringDescriptor descending select c).ToList();

			return l;
		}


		public static bool isOpen()
		{
			if (dc.Connection.State.HasFlag(System.Data.ConnectionState.Open))
				return true;
			else
				return false;
		}


		public static bool Connect()
		{
			try
			{
				dc.Connection.Open();
				return isOpen();
			}
			catch(Exception e)
			{
				return false;
			}


		}

		public static DataTable TagCodeCountWithCheckinTags()
		{
			DataTable result;

			string sqlCommand = @"select c.tagCode as TagCode, Coalesce(ct.stringDescriptor, '--') as StringDescriptor, 
									Coalesce(ct.IdString, '--') as IdString, Count(c.tagCode) as Count
									from checkin c 
									left join checkintag ct on c.tagCode = ct.tagCode 
									group by c.tagCode, ct.stringDescriptor, ct.IdString
									order by Count desc";
			using (SqlConnection connection = new SqlConnection(Properties.Settings.Default.CheckinDatabaseConnectionString))
			{
				SqlCommand command = new SqlCommand(sqlCommand, connection);
				SqlDataAdapter da = new SqlDataAdapter();
				da.SelectCommand = command;
				DataSet ds = new DataSet();
				command.Connection.Open();
				da.Fill(ds);
				result = ds.Tables[0];
				command.Connection.Close();
			}
				return result;
		}

        public static bool ClearCheckinTable()
        {
            Table<CheckinRecord> checkinRecords = dc.GetTable<CheckinRecord>();

            try
            {
                foreach (CheckinRecord r in checkinRecords)
                {
                    checkinRecords.DeleteOnSubmit(r);
                }

                dc.SubmitChanges();

                return true;
            }
            catch
            {
                return false;
            }

        }
	}


}
