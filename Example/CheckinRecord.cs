using System;
using FileHelpers;
using System.Collections.Generic;
using System.Linq;
using System.Data.Linq.Mapping;
using System.Text;
using System.Threading.Tasks;

namespace TapTrack.Demo
{
	[DelimitedRecord(","),IgnoreFirst]
	[Table(Name = "checkin")]
	public class CheckinRecord
	{
		public CheckinRecord()
		{
			//tagCode = new byte[7];		
		}

		[Column(IsPrimaryKey = true)]
		public Guid id { get; set; }

		[Column]
		public string tagCode { get; set; }

		[Column]
		public DateTime timestamp { get; set; }

		[Column]
		public string stationName { get; set; }

		[Column]
		public int stationCode { get; set; }
	}
}
