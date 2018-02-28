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
	[Table(Name = "tagCount")]
	public class TagCodeCount
	{
		public TagCodeCount(string tagCode, int count)
		{
			TagCode = tagCode;
			Count = count;
		}

		[Column]
		public string TagCode { get; set; }

		[Column]
		public int Count { get; set; }
	}
}
