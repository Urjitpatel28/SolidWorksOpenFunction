using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyApp.UI.Models
{
	public class VersionOption
	{
		public string Display { get; set; }     // e.g., "SOLIDWORKS 2023"
		public string VersionKey { get; set; }  // e.g., "2023"
		public bool IsCurrent { get; set; }
	}
}
