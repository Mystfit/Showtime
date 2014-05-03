using System;
using System.Collections.Generic;
using System.Linq;
using ZST;
using System.Text;
using System.Threading.Tasks;

namespace ZST{

    class ZstStage : ZstNode{
        public ZstStage(int port) : base("stage")
        {
            string address = "tcp://*:" + port;
            m_reply.Bind(address);
            Console.WriteLine("Stage active on address " + m_reply.Options.GetLastEndpoint);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            int port = 6000;
            ZstStage stage = new ZstStage(port);           
        }
    }
}
