using System;
using NetMQ;
using NetMQ.zmq;

namespace ZST{
	public class ZstPoller : ThreadedJob
	{
		public NetMQ.Poller m_poller;  // arbitary job data
		public NetMQ.Poller poller{ get { return m_poller; }}

		public ZstPoller(){
			m_poller = new NetMQ.Poller();
		}

		protected override void ThreadFunction()
		{
			m_poller.PollTimeout = 5;
            Console.WriteLine("Staring poller...");
			m_poller.Start();
		}

		public override bool Update(){
			if (IsDone)
			{
				m_poller.Stop();
				OnFinished();
				return true;
			}
			return false;
		}
	}
}