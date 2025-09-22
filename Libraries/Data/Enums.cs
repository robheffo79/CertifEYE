using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnchorSafe.Data
{
	public enum DeviceReferenceType
	{
		None,
		Inspection,
		User
	}

	public enum ConnectionType
	{
		Unknown,
		Bluetooth,
		Cellular,
		Ethernet,
		WiFi
	}
}