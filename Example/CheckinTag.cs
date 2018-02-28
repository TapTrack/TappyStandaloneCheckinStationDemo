using System;
using FileHelpers;
using System.Collections.Generic;
using System.Linq;
using System.Data.Linq.Mapping;
using System.Text;
using System.Threading.Tasks;

namespace TapTrack.Demo
{
	[DelimitedRecord(","), IgnoreFirst]
	[Table(Name = "checkinTag")]
	public class CheckinTag
	{
		public CheckinTag()
		{			

		}

		public CheckinTag(string tagCode, string stringDescriptor)
		{
			TagCode = tagCode;
			StringDescriptor = stringDescriptor;
		}

		public CheckinTag(string tagCode, string stringDescriptor, string idNum)
		{
			TagCode = tagCode;
			StringDescriptor = stringDescriptor;
			IdString = idNum;
		}

		[Column(IsPrimaryKey = true)]
		public string TagCode { get; set; }

		[Column]
		public string StringDescriptor { get; set; }

		[Column(CanBeNull = true)]
		public string IdString { get; set; }
	}
}