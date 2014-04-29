using System;
using System.Collections.Generic;
using NetMQ;
using NetMQ.zmq;

namespace ZST{
	public class ZstPoller : ThreadedJob
	{
		public NetMQ.Poller m_poller;  // arbitary job data
		public NetMQ.Poller poller{ get { return m_poller; }}
		protected List<NetMQSocket> m_sockets;

		public ZstPoller(){
			m_poller = new NetMQ.Poller();
			m_sockets = new List<NetMQSocket>();
		}

		public void AddSocket(NetMQSocket socket){
			m_sockets.Add(socket);
			m_poller.AddSocket(socket);
		}

		protected override void ThreadFunction()
		{
			m_poller.PollTimeout = 4;
            Console.WriteLine("Staring poller...");

            m_poller.Start();     
		}

		public override bool Update(){
			if (IsDone)
			{
				OnFinished();
				return true;
			}
			return false;
		}

		protected override void OnFinished ()
		{
			foreach(NetMQSocket socket in m_sockets){
				m_poller.RemoveSocket(socket);
				socket.Dispose();
			}
			m_sockets.Clear();

			m_poller.Stop(false);
			base.OnFinished ();
		}
	}
}