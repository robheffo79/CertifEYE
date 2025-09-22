using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace ServerTiming
{
    public class ServerTimingModule : IHttpModule
    {
		public void Init(HttpApplication context)
		{
			context.BeginRequest += OnBeginRequest;
			context.EndRequest += OnEndRequest;
		}

		private void OnBeginRequest(object sender, EventArgs e)
		{
			HttpContext.Current.Items["ServerTiming_BeginRequest"] = Stopwatch.StartNew();
		}

		private void OnEndRequest(object sender, EventArgs e)
		{
            Stopwatch stopwatch = HttpContext.Current.Items["ServerTiming_BeginRequest"] as Stopwatch;
			if (stopwatch != null)
			{
				stopwatch.Stop();
				Double milliseconds = (Double)stopwatch.ElapsedTicks / Stopwatch.Frequency * 1000;
				HttpContext.Current.Response.Headers.Add("Server-Timing", $"total;dur={milliseconds:0.00}");
			}
		}

		public void Dispose() { }
	}
}
